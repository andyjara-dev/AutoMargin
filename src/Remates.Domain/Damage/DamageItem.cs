namespace Remates.Domain.Damage;

/// <summary>
/// Un daño estimado. El costo es siempre un <b>rango</b>: en un remate no se conoce el estado real
/// y un valor puntual da una falsa sensación de certeza.
/// </summary>
public sealed record DamageItem
{
    public required DamageCategory Category { get; init; }
    public required DamageSeverity Severity { get; init; }

    public required decimal CostMin { get; init; }
    public required decimal CostExpected { get; init; }
    public required decimal CostMax { get; init; }

    public string? Description { get; init; }
    public DamageSource Source { get; init; } = DamageSource.Manual;

    /// <summary>Confianza 0..1 de la estimación. Relevante sobre todo para lo detectado por IA.</summary>
    public decimal? Confidence { get; init; }
}
