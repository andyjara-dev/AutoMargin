using Remates.Domain.Common;
using Remates.Domain.Financial;
using Remates.Domain.Parameters;

namespace Remates.Domain.Bidding;

/// <summary>
/// Entradas de incertidumbre que determinan el margen de seguridad.
/// Cada una es un factor 0..1: cuanto menos sabemos, más conservadora es la puja.
/// </summary>
public sealed record UncertaintyInputs
{
    /// <summary>(reparación_max − reparación_min) / (2 × reparación_esperada).</summary>
    public required decimal RepairUncertainty { get; init; }

    /// <summary>Dispersión relativa de los comparables de mercado.</summary>
    public required decimal MarketDispersion { get; init; }

    /// <summary>Cantidad de comparables válidos. Menos datos, más margen.</summary>
    public required int ComparableCount { get; init; }

    /// <summary>Riesgo documental como factor 0..1.</summary>
    public required decimal DocumentRiskFactor { get; init; }
}

/// <summary>
/// Calcula la puja máxima. Es el cálculo central del sistema y es puramente algebraico.
///
/// Se despeja P de:  S = P(1+α)k + Fk + U
///   →  P_teórica = (S − U − F·k) / ((1+α)·k)
///
/// y luego se aplica un margen de seguridad que crece con la incertidumbre del vehículo,
/// porque ganar una subasta significa haber sido el más optimista de la sala.
/// </summary>
public static class MaxBidCalculator
{
    public static MaxBidResult Calculate(
        CostStructure structure,
        UncertaintyInputs uncertainty,
        AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(uncertainty);
        ArgumentNullException.ThrowIfNull(parameters);

        var breakeven = FinancialEngine.BreakevenBid(structure);
        var (requiredProfit, driver) = CalculateRequiredProfit(structure, breakeven, parameters);

        var denominator = (1m + structure.ProportionalRate) * structure.CapitalFactor;
        var theoretical = denominator > 0m
            ? (structure.NetSaleValue - requiredProfit - structure.FixedCosts * structure.CapitalFactor) / denominator
            : 0m;

        var (safetyMargin, breakdown) = CalculateSafetyMargin(uncertainty, parameters);

        var maxBid = theoretical > 0m
            ? MoneyMath.FloorToPeso(theoretical * (1m - safetyMargin))
            : MoneyMath.FloorToPeso(theoretical);

        return new MaxBidResult
        {
            BreakevenBid = breakeven,
            RequiredProfit = MoneyMath.RoundToPeso(requiredProfit),
            RequiredProfitDriver = driver,
            TheoreticalMaxBid = MoneyMath.FloorToPeso(theoretical),
            SafetyMarginPct = MoneyMath.RoundRate(safetyMargin),
            SafetyMarginBreakdown = breakdown,
            MaxBid = Math.Max(0m, maxBid),
            IsViable = maxBid > 0m
        };
    }

    /// <summary>
    /// Utilidad mínima exigida. Un monto fijo en pesos penaliza mal los extremos: en un auto de $3M
    /// exigir $1,5M es imposible, y en uno de $25M es demasiado poco. Por eso se toma el mayor entre
    /// el piso absoluto y lo que exige la rentabilidad anual objetivo sobre el capital comprometido.
    /// </summary>
    private static (decimal Amount, string Driver) CalculateRequiredProfit(
        CostStructure structure,
        decimal breakevenBid,
        AnalysisParameters parameters)
    {
        var estimatedCapital = breakevenBid * (1m + structure.ProportionalRate) + structure.FixedCosts;
        var byReturn = parameters.MinRoiAnnual * (structure.DaysToSell / 365m) * estimatedCapital;

        return byReturn > parameters.MinProfitAbs
            ? (byReturn, "roi_annual")
            : (parameters.MinProfitAbs, "min_profit_abs");
    }

    private static (decimal Margin, IReadOnlyList<SafetyMarginComponent> Breakdown) CalculateSafetyMargin(
        UncertaintyInputs uncertainty,
        AnalysisParameters parameters)
    {
        var repair = MoneyMath.Clamp01(uncertainty.RepairUncertainty);
        var market = MoneyMath.Clamp01(uncertainty.MarketDispersion);
        var data = MoneyMath.Clamp01(MoneyMath.SafeDivide(1m, 1m + uncertainty.ComparableCount, 1m));
        var document = MoneyMath.Clamp01(uncertainty.DocumentRiskFactor);

        var breakdown = new List<SafetyMarginComponent>
        {
            new() { Key = "base", Label = "Margen base", RawValue = 1m, Contribution = parameters.SafetyMarginBase },
            new() { Key = "repair", Label = "Incertidumbre de reparación", RawValue = repair, Contribution = 0.20m * repair },
            new() { Key = "market", Label = "Dispersión de mercado", RawValue = market, Contribution = 0.15m * market },
            new() { Key = "data", Label = "Escasez de comparables", RawValue = data, Contribution = 0.10m * data },
            new() { Key = "document", Label = "Riesgo documental", RawValue = document, Contribution = 0.10m * document }
        };

        var raw = breakdown.Sum(c => c.Contribution);
        var margin = MoneyMath.Clamp(raw, parameters.SafetyMarginMin, parameters.SafetyMarginMax);

        var rounded = breakdown
            .Select(c => c with
            {
                RawValue = MoneyMath.RoundRate(c.RawValue),
                Contribution = MoneyMath.RoundRate(c.Contribution)
            })
            .ToList();

        return (margin, rounded);
    }
}
