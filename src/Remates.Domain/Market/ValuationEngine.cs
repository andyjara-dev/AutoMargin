using Remates.Domain.Common;
using Remates.Domain.Parameters;

namespace Remates.Domain.Market;

/// <summary>
/// Calcula el valor de mercado a partir de comparables, normalizándolos al kilometraje y año
/// del vehículo objetivo y resumiéndolos por percentiles.
///
/// Determinístico y sin dependencias: mismo input, mismo output, siempre.
/// </summary>
public static class ValuationEngine
{
    public static ValuationResult Calculate(
        IReadOnlyList<MarketComparable> comparables,
        int targetYear,
        int targetMileageKm,
        AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(comparables);
        ArgumentNullException.ThrowIfNull(parameters);

        var usable = comparables
            .Where(c => !c.IsOutlier && c.ListedPrice > 0m)
            .ToList();

        var excluded = comparables.Count - usable.Count;

        if (usable.Count == 0)
            return ValuationResult.Empty(parameters.NegotiationDiscountPct) with { ExcludedCount = excluded };

        var adjusted = usable
            .Select(c => Adjust(c, targetYear, targetMileageKm, parameters))
            .ToList();

        var sortedPrices = adjusted
            .Select(a => a.AdjustedPrice)
            .OrderBy(p => p)
            .ToList();

        var p25 = MoneyMath.Percentile(sortedPrices, 0.25m);
        var p50 = MoneyMath.Percentile(sortedPrices, 0.50m);
        var p75 = MoneyMath.Percentile(sortedPrices, 0.75m);

        var conservative = p25 * (1m - parameters.NegotiationDiscountPct);

        return new ValuationResult
        {
            Optimistic = MoneyMath.RoundToPeso(p75),
            Expected = MoneyMath.RoundToPeso(p50),
            Conservative = MoneyMath.RoundToPeso(conservative),
            ConservativeBeforeDiscount = MoneyMath.RoundToPeso(p25),
            DispersionPct = MoneyMath.RoundRate(MoneyMath.SafeDivide(p75 - p25, p50)),
            ComparableCount = adjusted.Count,
            ExcludedCount = excluded,
            AverageAgeDays = Math.Round(adjusted.Average(a => (decimal)a.Source.AgeDays), 1),
            NegotiationDiscountPct = parameters.NegotiationDiscountPct,
            HasEnoughEvidence = adjusted.Count >= parameters.MinComparables,
            Adjusted = adjusted
        };
    }

    /// <summary>
    /// Normaliza el precio de un comparable al vehículo objetivo.
    /// Si el comparable tiene más kilómetros que el objetivo, vale menos, así que su precio se ajusta
    /// hacia arriba para representar lo que valdría con el kilometraje del objetivo. Lo mismo con el año.
    /// </summary>
    private static AdjustedComparable Adjust(
        MarketComparable comparable,
        int targetYear,
        int targetMileageKm,
        AnalysisParameters parameters)
    {
        var mileageDeltaThousands = (comparable.MileageKm - targetMileageKm) / 1000m;
        var mileageAdjustment = mileageDeltaThousands * parameters.MileageAdjustPctPer1000Km;

        var yearDelta = targetYear - comparable.Year;
        var yearAdjustment = yearDelta * parameters.YearAdjustPct;

        var rawTotal = mileageAdjustment + yearAdjustment;
        var cap = parameters.MaxComparableAdjustmentPct;
        var total = MoneyMath.Clamp(rawTotal, -cap, cap);

        return new AdjustedComparable
        {
            Source = comparable,
            MileageAdjustment = MoneyMath.RoundRate(mileageAdjustment),
            YearAdjustment = MoneyMath.RoundRate(yearAdjustment),
            TotalAdjustment = MoneyMath.RoundRate(total),
            AdjustedPrice = MoneyMath.RoundToPeso(comparable.ListedPrice * (1m + total)),
            AdjustmentWasCapped = rawTotal != total
        };
    }
}
