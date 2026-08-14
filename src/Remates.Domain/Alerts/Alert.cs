namespace Remates.Domain.Alerts;

public enum AlertType
{
    /// <summary>El vehículo lleva demasiado tiempo sin venderse.</summary>
    StaleInventory = 1,

    /// <summary>Publicado hace tiempo y sin vender: probablemente el precio está alto.</summary>
    PriceNeedsAdjustment = 2,

    /// <summary>La reparación real superó lo presupuestado.</summary>
    RepairOverBudget = 3,

    /// <summary>El margen proyectado no compensa el riesgo.</summary>
    LowMargin = 4,

    /// <summary>Demasiado capital comprometido en un solo vehículo.</summary>
    CapitalConcentration = 5,

    /// <summary>Se compró sin análisis previo: no habrá nada que comparar al vender.</summary>
    PurchasedWithoutAnalysis = 6,

    /// <summary>Queda poco capital disponible para seguir comprando.</summary>
    LowAvailableCapital = 7
}

public enum AlertSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}

public sealed record Alert
{
    public required AlertType Type { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string Message { get; init; }

    /// <summary>Qué hacer al respecto. Una alerta sin acción sugerida es solo ruido.</summary>
    public required string Suggestion { get; init; }

    public long? VehicleId { get; init; }
    public string? VehicleLabel { get; init; }

    /// <summary>Monto o cantidad que originó la alerta, para poder ordenarlas por impacto.</summary>
    public decimal Magnitude { get; init; }
}
