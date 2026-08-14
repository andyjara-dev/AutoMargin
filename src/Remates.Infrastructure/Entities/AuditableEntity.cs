namespace Remates.Infrastructure.Entities;

/// <summary>
/// Base de las entidades que llevan rastro de quién las creó y modificó.
/// Los campos los rellena el interceptor de auditoría, no el código de negocio.
/// </summary>
public abstract class AuditableEntity
{
    public long Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
