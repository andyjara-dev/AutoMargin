namespace Remates.Domain.Market;

/// <summary>Resultado de la valuación de mercado a partir de comparables.</summary>
public sealed record ValuationResult
{
    /// <summary>P75 de los precios ajustados.</summary>
    public required decimal Optimistic { get; init; }

    /// <summary>P50 (mediana) de los precios ajustados.</summary>
    public required decimal Expected { get; init; }

    /// <summary>P25 ajustado por la brecha de negociación. Es el valor que alimenta la puja máxima.</summary>
    public required decimal Conservative { get; init; }

    /// <summary>P25 antes de aplicar la brecha de negociación, para trazabilidad.</summary>
    public required decimal ConservativeBeforeDiscount { get; init; }

    /// <summary>Dispersión relativa (P75-P25)/P50. Mide cuán ruidoso es el mercado de este modelo.</summary>
    public required decimal DispersionPct { get; init; }

    public required int ComparableCount { get; init; }
    public required int ExcludedCount { get; init; }
    public required decimal AverageAgeDays { get; init; }
    public required decimal NegotiationDiscountPct { get; init; }

    /// <summary>False cuando no hay comparables suficientes: el análisis debe marcar el gate correspondiente.</summary>
    public required bool HasEnoughEvidence { get; init; }

    public required IReadOnlyList<AdjustedComparable> Adjusted { get; init; }

    public static ValuationResult Empty(decimal negotiationDiscountPct) => new()
    {
        Optimistic = 0m,
        Expected = 0m,
        Conservative = 0m,
        ConservativeBeforeDiscount = 0m,
        DispersionPct = 0m,
        ComparableCount = 0,
        ExcludedCount = 0,
        AverageAgeDays = 0m,
        NegotiationDiscountPct = negotiationDiscountPct,
        HasEnoughEvidence = false,
        Adjusted = []
    };
}
