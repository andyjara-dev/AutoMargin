using Remates.Domain.Bidding;
using Remates.Domain.Common;
using Remates.Domain.Damage;
using Remates.Domain.Financial;
using Remates.Domain.Market;
using Remates.Domain.Scoring;

namespace Remates.Domain.Analysis;

/// <summary>
/// Orquesta el análisis completo de una oportunidad: valuación → reparación → costos →
/// puja máxima → escenarios → score y semáforo.
///
/// Es una función pura: mismo request, mismo resultado. No accede a base de datos, red ni reloj.
/// </summary>
public static class DealAnalyzer
{
    public static DealAnalysisResult Analyze(DealAnalysisRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = request.Parameters;

        // 1. Valor de mercado
        var valuation = BuildValuation(request);

        // 2. Costo de reparación
        var repair = RepairEstimator.Calculate(request.Damages, request.InspectionLevel);

        // 3. Estructura de costos sobre el valor conservador
        var transport = request.Transport ?? parameters.TransportDefault;
        var detailing = request.Detailing ?? parameters.DetailingDefault;
        var days = request.EstimatedDaysToSell ?? parameters.DefaultDaysToSell;

        var structure = FinancialEngine.BuildCostStructure(
            valuation.Conservative,
            repair.TotalExpected,
            transport,
            detailing,
            request.OtherFixedCosts,
            days,
            parameters);

        // 4. Puja máxima
        var uncertainty = new UncertaintyInputs
        {
            RepairUncertainty = repair.UncertaintyRatio,
            MarketDispersion = valuation.DispersionPct,
            ComparableCount = valuation.ComparableCount,
            DocumentRiskFactor = request.DocumentRisk.ToFactor()
        };

        var maxBid = MaxBidCalculator.Calculate(structure, uncertainty, parameters);

        // 5. Métricas al precio actual y al techo
        var metricsAtCurrent = FinancialEngine.Evaluate(structure, request.CurrentAuctionPrice);
        var metricsAtMaxBid = FinancialEngine.Evaluate(structure, maxBid.MaxBid);

        // 6. Escenarios sobre el precio que realmente se está evaluando
        var scenarios = ScenarioBuilder.Build(
            new ScenarioInputs
            {
                ValuationExpected = valuation.Expected,
                ValuationConservative = valuation.Conservative,
                RepairMin = repair.TotalMin,
                RepairExpected = repair.TotalExpected,
                RepairMax = repair.TotalMax,
                Transport = transport,
                Detailing = detailing,
                OtherFixedCosts = request.OtherFixedCosts,
                BaseDaysToSell = days,
                BidPrice = request.CurrentAuctionPrice
            },
            parameters);

        // 7. Score y semáforo
        var score = ScoringEngine.Calculate(
            new ScoringInputs
            {
                EvaluationBidPrice = request.CurrentAuctionPrice,
                Metrics = metricsAtCurrent,
                MaxBid = maxBid,
                Valuation = valuation,
                Repair = repair,
                DocumentRisk = request.DocumentRisk,
                Scenarios = scenarios,
                TotalCapital = request.TotalCapital
            },
            parameters);

        return new DealAnalysisResult
        {
            FinancialEngineVersion = FinancialEngine.EngineVersion,
            ScoringEngineVersion = ScoringEngine.EngineVersion,
            Valuation = valuation,
            Repair = repair,
            CostStructure = structure,
            MaxBid = maxBid,
            MetricsAtCurrentPrice = metricsAtCurrent,
            MetricsAtMaxBid = metricsAtMaxBid,
            Scenarios = scenarios,
            Score = score,
            BreakevenBid = maxBid.BreakevenBid,
            CurrentAuctionPrice = MoneyMath.RoundToPeso(request.CurrentAuctionPrice),
            Headroom = MoneyMath.RoundToPeso(maxBid.MaxBid - request.CurrentAuctionPrice),
            Disclaimers = BuildDisclaimers(repair, valuation)
        };
    }

    /// <summary>
    /// Usa los comparables si existen; si no, el valor ingresado a mano.
    /// El valor manual cuenta como evidencia suficiente para no bloquear el análisis, pero
    /// el componente de calidad de evidencia del score lo castiga igual.
    /// </summary>
    private static ValuationResult BuildValuation(DealAnalysisRequest request)
    {
        var fromComparables = ValuationEngine.Calculate(
            request.Comparables,
            request.Year,
            request.MileageKm,
            request.Parameters);

        if (fromComparables.HasEnoughEvidence || request.ManualValuation is null)
            return fromComparables;

        var manual = request.ManualValuation;
        var expected = manual.Expected ?? manual.Conservative;
        var optimistic = manual.Optimistic ?? expected;

        return fromComparables with
        {
            Conservative = MoneyMath.RoundToPeso(manual.Conservative),
            ConservativeBeforeDiscount = MoneyMath.RoundToPeso(manual.Conservative),
            Expected = MoneyMath.RoundToPeso(expected),
            Optimistic = MoneyMath.RoundToPeso(optimistic),
            DispersionPct = MoneyMath.RoundRate(
                MoneyMath.SafeDivide(optimistic - manual.Conservative, expected)),
            HasEnoughEvidence = true
        };
    }

    private static IReadOnlyList<string> BuildDisclaimers(RepairEstimate repair, ValuationResult valuation)
    {
        var disclaimers = new List<string> { RepairEstimate.Disclaimer };

        if (repair.ContainsUnconfirmedAiEstimates)
        {
            disclaimers.Add(
                "Parte de los daños provienen de análisis automático de imágenes y no han sido confirmados " +
                "por una persona. Verificar antes de pujar.");
        }

        if (valuation.ComparableCount < 3)
        {
            disclaimers.Add(
                "La valuación se apoya en poca evidencia de mercado. Cargar más comparables reduce " +
                "el margen de seguridad y sube la puja máxima.");
        }

        disclaimers.Add(
            "Los resultados no constituyen asesoría legal ni tributaria. El estado documental del vehículo " +
            "debe verificarse en el Registro Civil antes de participar en el remate.");

        return disclaimers;
    }
}
