using Microsoft.AspNetCore.Identity;

namespace Remates.Infrastructure.Entities;

public class AppUser : IdentityUser<long>
{
    public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public class AppRole : IdentityRole<long>
{
    public string? Description { get; set; }
}

/// <summary>Roles previstos. Se siembran todos aunque el MVP solo use Admin.</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Vendedor = "Vendedor";
    public const string Mecanico = "Mecanico";
    public const string Analista = "Analista";

    public static readonly (string Name, string Description)[] All =
    [
        (Admin, "Acceso total, incluida la edición de parámetros."),
        (Vendedor, "Gestiona inventario, publicaciones y ventas."),
        (Mecanico, "Registra daños, reparaciones y gastos de taller."),
        (Analista, "Analiza oportunidades y consulta reportes.")
    ];
}

public class RefreshToken
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public AppUser? User { get; set; }

    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}

public enum AuditAction { Created = 1, Updated = 2, Deleted = 3 }

/// <summary>Rastro de cambios sobre las entidades relevantes. Lo escribe un interceptor de EF.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public AuditAction Action { get; set; }

    /// <summary>Diccionario jsonb con los valores antes y después, solo de lo que cambió.</summary>
    public string? ChangesJson { get; set; }

    public long? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
