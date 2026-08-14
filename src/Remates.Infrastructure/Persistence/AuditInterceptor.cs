using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence;

/// <summary>Provee la identidad del usuario actual al interceptor, sin acoplar Infrastructure a HTTP.</summary>
public interface ICurrentUser
{
    long? UserId { get; }
    string? UserName { get; }
}

/// <summary>
/// Rellena las marcas de auditoría y escribe el rastro de cambios en cada SaveChanges.
///
/// Se hace en un interceptor y no en los endpoints a propósito: si depende de que alguien se
/// acuerde de llamarlo, tarde o temprano hay un cambio sin registrar.
/// </summary>
public sealed class AuditInterceptor(ICurrentUser currentUser, TimeProvider timeProvider)
    : SaveChangesInterceptor
{
    /// <summary>Entidades cuyo cambio no aporta valor de auditoría y solo generaría ruido.</summary>
    private static readonly HashSet<string> Excluded =
    [
        nameof(AuditLog),
        nameof(RefreshToken),
        nameof(VehicleStatusHistory)
    ];

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var userName = currentUser.UserName;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                {
                    auditable.CreatedAt = now;
                    auditable.CreatedBy = userName;
                }
                else if (entry.State == EntityState.Modified)
                {
                    auditable.UpdatedAt = now;
                    auditable.UpdatedBy = userName;
                }
            }

            var log = BuildLog(entry, now);
            if (log is not null) logs.Add(log);
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private AuditLog? BuildLog(EntityEntry entry, DateTimeOffset now)
    {
        var entityName = entry.Metadata.ClrType.Name;

        if (Excluded.Contains(entityName)) return null;

        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Modified => AuditAction.Updated,
            EntityState.Deleted => AuditAction.Deleted,
            _ => (AuditAction?)null
        };

        if (action is null) return null;

        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsPrimaryKey()) continue;

            // Nunca registrar material sensible en el rastro de auditoría.
            var name = property.Metadata.Name;
            if (name is "PasswordHash" or "SecurityStamp" or "ConcurrencyStamp" or "TokenHash") continue;

            switch (action)
            {
                case AuditAction.Created when property.CurrentValue is not null:
                    changes[name] = property.CurrentValue;
                    break;

                case AuditAction.Updated when property.IsModified
                                              && !Equals(property.OriginalValue, property.CurrentValue):
                    changes[name] = new { from = property.OriginalValue, to = property.CurrentValue };
                    break;

                case AuditAction.Deleted when property.OriginalValue is not null:
                    changes[name] = property.OriginalValue;
                    break;
            }
        }

        // Una modificación que no cambió nada no merece una fila.
        if (action == AuditAction.Updated && changes.Count == 0) return null;

        return new AuditLog
        {
            EntityName = entityName,
            EntityId = PrimaryKeyOf(entry),
            Action = action.Value,
            ChangesJson = JsonSerializer.Serialize(changes),
            UserId = currentUser.UserId,
            UserName = currentUser.UserName,
            OccurredAt = now
        };
    }

    private static string PrimaryKeyOf(EntityEntry entry)
    {
        var keys = entry.Properties
            .Where(p => p.Metadata.IsPrimaryKey())
            .Select(p => p.CurrentValue?.ToString() ?? "?")
            .ToList();

        return keys.Count > 0 ? string.Join(":", keys) : "?";
    }
}
