using Remates.Domain.Parameters;

namespace Remates.Domain.Financial;

public enum ScenarioKind
{
    Optimistic = 1,
    Expected = 2,
    Pessimistic = 3
}

public sealed record ScenarioResult
{
    public required ScenarioKind Kind { get; init; }
    public required string Label { get; init; }

    /// <summary>Supuesto de precio de venta usado en este escenario.</summary>
    public required decimal SaleValue { get; init; }

    /// <summary>Supuesto de costo de reparación usado en este escenario.</summary>
    public required decimal RepairCost { get; init; }

    public required int DaysToSell { get; init; }
    public required DealMetrics Metrics { get; init; }
}

public sealed record ScenarioInputs
{
    public required decimal ValuationExpected { get; init; }
    public required decimal ValuationConservative { get; init; }
    public required decimal RepairMin { get; init; }
    public required decimal RepairExpected { get; init; }
    public required decimal RepairMax { get; init; }
    public required decimal Transport { get; init; }
    public required decimal Detailing { get; init; }
    public required decimal OtherFixedCosts { get; init; }
    public required int BaseDaysToSell { get; init; }

    /// <summary>Precio de adjudicación que se está evaluando en los tres escenarios.</summary>
    public required decimal BidPrice { get; init; }
}

/// <summary>
/// Construye los tres escenarios. El pesimista no es decorativo: es el que dispara el gate de
/// pérdida máxima tolerada, porque en un remate el caso malo ocurre con frecuencia real.
/// </summary>
public static class ScenarioBuilder
{
    public static IReadOnlyList<ScenarioResult> Build(ScenarioInputs inputs, AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(parameters);

        return
        [
            Build(ScenarioKind.Optimistic, "Optimista",
                inputs.ValuationExpected,
                inputs.RepairMin,
                ScaleDays(inputs.BaseDaysToSell, parameters.OptimisticDaysFactor),
                inputs, parameters),

            Build(ScenarioKind.Expected, "Esperado",
                inputs.ValuationConservative,
                inputs.RepairExpected,
                inputs.BaseDaysToSell,
                inputs, parameters),

            Build(ScenarioKind.Pessimistic, "Pesimista",
                inputs.ValuationConservative * parameters.PessimisticSaleFactor,
                inputs.RepairMax,
                ScaleDays(inputs.BaseDaysToSell, parameters.PessimisticDaysFactor),
                inputs, parameters)
        ];
    }

    private static ScenarioResult Build(
        ScenarioKind kind,
        string label,
        decimal saleValue,
        decimal repairCost,
        int days,
        ScenarioInputs inputs,
        AnalysisParameters parameters)
    {
        var structure = FinancialEngine.BuildCostStructure(
            saleValue,
            repairCost,
            inputs.Transport,
            inputs.Detailing,
            inputs.OtherFixedCosts,
            days,
            parameters);

        return new ScenarioResult
        {
            Kind = kind,
            Label = label,
            SaleValue = structure.GrossSaleValue,
            RepairCost = repairCost,
            DaysToSell = days,
            Metrics = FinancialEngine.Evaluate(structure, inputs.BidPrice)
        };
    }

    private static int ScaleDays(int baseDays, decimal factor)
        => Math.Max(1, (int)Math.Round(baseDays * factor, MidpointRounding.AwayFromZero));
}
