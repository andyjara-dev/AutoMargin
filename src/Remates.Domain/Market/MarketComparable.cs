namespace Remates.Domain.Market;

/// <summary>
/// Un aviso de mercado usado como referencia. El precio es de <b>lista</b>, no de transacción:
/// la brecha de negociación se aplica en <see cref="ValuationEngine"/>, no aquí.
/// </summary>
public sealed record MarketComparable
{
    public required decimal ListedPrice { get; init; }
    public required int Year { get; init; }
    public required int MileageKm { get; init; }

    /// <summary>Días transcurridos desde que se observó el aviso. Afecta la calidad de la evidencia.</summary>
    public int AgeDays { get; init; }

    public string? Source { get; init; }
    public string? Url { get; init; }

    /// <summary>Marcado manualmente como atípico: queda excluido del cálculo pero se conserva el registro.</summary>
    public bool IsOutlier { get; init; }
}

/// <summary>Comparable con su precio normalizado al kilometraje y año del vehículo objetivo.</summary>
public sealed record AdjustedComparable
{
    public required MarketComparable Source { get; init; }
    public required decimal MileageAdjustment { get; init; }
    public required decimal YearAdjustment { get; init; }
    public required decimal TotalAdjustment { get; init; }
    public required decimal AdjustedPrice { get; init; }
    public required bool AdjustmentWasCapped { get; init; }
}
