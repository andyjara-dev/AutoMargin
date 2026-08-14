using Remates.Domain.Damage;

namespace Remates.Infrastructure.Entities;

/// <summary>Un aviso de mercado usado como referencia. El precio es de lista, no de transacción.</summary>
public class MarketComparableEntity : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public decimal ListedPrice { get; set; }
    public int Year { get; set; }
    public int MileageKm { get; set; }

    public string? Source { get; set; }
    public string? Url { get; set; }
    public string? Region { get; set; }
    public string? Condition { get; set; }

    public DateTimeOffset ObservedAt { get; set; }

    /// <summary>Excluido del cálculo sin borrarlo: queda el registro de por qué se descartó.</summary>
    public bool IsOutlier { get; set; }
    public string? OutlierReason { get; set; }
}

public class DamageItemEntity : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public DamageCategory Category { get; set; }
    public DamageSeverity Severity { get; set; }

    public decimal CostMin { get; set; }
    public decimal CostExpected { get; set; }
    public decimal CostMax { get; set; }

    public string? Description { get; set; }
    public DamageSource Source { get; set; } = DamageSource.Manual;
    public decimal? Confidence { get; set; }

    /// <summary>
    /// Lo detectado por IA no entra al cálculo hasta que una persona lo confirma.
    /// Es la barrera que impide que un modelo mueva plata por su cuenta.
    /// </summary>
    public bool IsConfirmed { get; set; } = true;

    public long? AiAnalysisId { get; set; }
}
