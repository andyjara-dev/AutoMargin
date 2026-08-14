using Microsoft.EntityFrameworkCore;
using Remates.Domain.Parameters;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Acceso al conjunto de parámetros activo y creación de versiones nuevas.
///
/// Cada análisis persistido registra con qué conjunto se calculó, así que los parámetros nunca
/// se modifican en sitio: cambiarlos crea una versión nueva y desactiva la anterior. De otro modo,
/// ajustar hoy el ROI objetivo alteraría retroactivamente decisiones ya tomadas.
/// </summary>
public sealed class ParameterProvider(RematesDbContext db, TimeProvider timeProvider)
{
    public async Task<(ParameterSet Set, AnalysisParameters Parameters)> GetActiveAsync(CancellationToken ct)
    {
        var set = await LoadActiveAsync(ct)
            ?? throw new InvalidOperationException(
                "No hay un conjunto de parámetros activo. Revisa el sembrado inicial de la base.");

        return (set, ParameterSetMapper.ToParameters(set.Values));
    }

    public Task<ParameterSet?> LoadActiveAsync(CancellationToken ct)
        => db.ParameterSets.Include(s => s.Values).FirstOrDefaultAsync(s => s.IsActive, ct);

    /// <summary>
    /// Crea una versión nueva a partir de los valores recibidos y la deja activa.
    ///
    /// Se hace en dos pasos dentro de una transacción porque la base garantiza que solo puede
    /// haber un conjunto activo: hay que desactivar el anterior antes de insertar el nuevo.
    /// </summary>
    public async Task<ParameterSet> CreateVersionAsync(
        AnalysisParameters parameters, string? name, string? note, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var now = timeProvider.GetUtcNow();

        // La estrategia de reintentos de EF no admite transacciones abiertas a mano: hay que
        // entregarle la unidad completa para que pueda repetirla entera si falla a medio camino.
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var current = await db.ParameterSets.FirstOrDefaultAsync(s => s.IsActive, ct);
            if (current is not null)
            {
                current.IsActive = false;
                await db.SaveChangesAsync(ct);
            }

            var version = await db.ParameterSets.CountAsync(ct) + 1;

            var created = new ParameterSet
            {
                Name = string.IsNullOrWhiteSpace(name) ? $"Versión {version}" : name.Trim(),
                IsActive = true,
                ValidFrom = now,
                Note = note
            };

            foreach (var value in ParameterSetMapper.ToValues(parameters))
                created.Values.Add(value);

            db.ParameterSets.Add(created);
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return created;
        });
    }

    /// <summary>Historial de versiones, para poder auditar con qué parámetros se decidió cada compra.</summary>
    public Task<List<ParameterSet>> HistoryAsync(CancellationToken ct)
        => db.ParameterSets.AsNoTracking()
            .OrderByDescending(s => s.ValidFrom)
            .Take(50)
            .ToListAsync(ct);
}
