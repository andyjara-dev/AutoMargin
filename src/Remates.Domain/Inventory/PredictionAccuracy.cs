using Remates.Domain.Common;

namespace Remates.Domain.Inventory;

public sealed record PredictionAccuracyInputs
{
    public required decimal PredictedSaleValue { get; init; }
    public required decimal ActualSaleValue { get; init; }

    public required decimal PredictedRepairCost { get; init; }
    public required decimal ActualRepairCost { get; init; }

    public required int PredictedDays { get; init; }
    public required int ActualDays { get; init; }

    public required decimal PredictedProfit { get; init; }
    public required decimal ActualProfit { get; init; }
}

/// <summary>
/// Qué tan bien acertó el análisis. Es el insumo del aprendizaje: sin medir el error no hay
/// nada que calibrar ni con qué entrenar un modelo más adelante.
/// </summary>
public sealed record PredictionAccuracy
{
    /// <summary>
    /// Error relativo del precio de venta. Negativo significa que se vendió por debajo
    /// de lo proyectado.
    /// </summary>
    public required decimal SaleValueErrorPct { get; init; }

    /// <summary>
    /// Error relativo de la reparación. <b>Positivo significa que costó más de lo estimado</b>,
    /// que es el sesgo habitual y el que conviene vigilar.
    /// </summary>
    public required decimal RepairCostErrorPct { get; init; }

    /// <summary>Error relativo del tiempo de venta. Positivo significa que tardó más.</summary>
    public required decimal DaysErrorPct { get; init; }

    public required decimal ProfitErrorPct { get; init; }

    /// <summary>Diferencia en pesos entre la utilidad proyectada y la real.</summary>
    public required decimal ProfitDelta { get; init; }

    /// <summary>True si el resultado real fue peor que el proyectado en utilidad.</summary>
    public required bool UnderPerformed { get; init; }
}

public static class PredictionAccuracyCalculator
{
    public static PredictionAccuracy Calculate(PredictionAccuracyInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        return new PredictionAccuracy
        {
            SaleValueErrorPct = RelativeError(inputs.ActualSaleValue, inputs.PredictedSaleValue),
            RepairCostErrorPct = RelativeError(inputs.ActualRepairCost, inputs.PredictedRepairCost),
            DaysErrorPct = RelativeError(inputs.ActualDays, inputs.PredictedDays),
            ProfitErrorPct = RelativeError(inputs.ActualProfit, inputs.PredictedProfit),
            ProfitDelta = MoneyMath.RoundToPeso(inputs.ActualProfit - inputs.PredictedProfit),
            UnderPerformed = inputs.ActualProfit < inputs.PredictedProfit
        };
    }

    /// <summary>
    /// (real − proyectado) / |proyectado|. Se usa el valor absoluto en el denominador para que
    /// el signo del error signifique siempre lo mismo, incluso cuando lo proyectado fue negativo.
    /// Sin proyección no hay error que medir.
    /// </summary>
    private static decimal RelativeError(decimal actual, decimal predicted)
        => predicted == 0m
            ? 0m
            : MoneyMath.RoundRate((actual - predicted) / Math.Abs(predicted));
}
