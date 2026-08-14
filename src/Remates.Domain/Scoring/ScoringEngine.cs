using Remates.Domain.Bidding;
using Remates.Domain.Common;
using Remates.Domain.Damage;
using Remates.Domain.Financial;
using Remates.Domain.Market;
using Remates.Domain.Parameters;

namespace Remates.Domain.Scoring;

public sealed record ScoringInputs
{
    public required decimal EvaluationBidPrice { get; init; }
    public required DealMetrics Metrics { get; init; }
    public required MaxBidResult MaxBid { get; init; }
    public required ValuationResult Valuation { get; init; }
    public required RepairEstimate Repair { get; init; }
    public required DocumentRiskLevel DocumentRisk { get; init; }
    public required IReadOnlyList<ScenarioResult> Scenarios { get; init; }

    /// <summary>Capital total del negocio. Si es 0 no se evalúa el gate de concentración.</summary>
    public decimal TotalCapital { get; init; }
}

/// <summary>
/// Motor de scoring y semáforo. Determinístico y explicable: cada punto del score tiene
/// un origen trazable y los gates son reglas duras, no ponderaciones.
///
/// El score NO decide por sí solo. El semáforo se ancla en la relación precio actual vs puja máxima,
/// que es una comparación de pesos; el score solo matiza.
/// </summary>
public static class ScoringEngine
{
    public const string EngineVersion = "1.0.0";

    public static ScoreResult Calculate(ScoringInputs inputs, AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(parameters);

        var weights = parameters.Weights;
        var totalWeight = weights.Total > 0m ? weights.Total : 1m;

        var raw = new List<(string Key, string Label, decimal Weight, decimal Normalized, string Explanation)>
        {
            BuildProfitability(inputs, parameters, weights),
            BuildBidHeadroom(inputs, weights),
            BuildLiquidity(inputs, weights),
            BuildMechanicalRisk(inputs, weights),
            BuildDocumentRisk(inputs, weights),
            BuildEstimateCertainty(inputs, weights),
            BuildEvidenceQuality(inputs, weights)
        };

        var components = raw
            .Select(c => new ScoreComponent
            {
                Key = c.Key,
                Label = c.Label,
                Weight = c.Weight,
                Normalized = Math.Round(c.Normalized, 1),
                Points = Math.Round(c.Weight * c.Normalized / totalWeight, 2),
                PointsLost = Math.Round(c.Weight * (100m - c.Normalized) / totalWeight, 2),
                Explanation = c.Explanation
            })
            .ToList();

        var score = Math.Round(components.Sum(c => c.Points), 0, MidpointRounding.AwayFromZero);
        var gates = EvaluateGates(inputs, parameters);
        var light = DetermineTrafficLight(score, gates, inputs, parameters);

        return new ScoreResult
        {
            Score = MoneyMath.Clamp(score, 0m, 100m),
            TrafficLight = light,
            Recommendation = light switch
            {
                TrafficLight.Green => "COMPRAR / PARTICIPAR",
                TrafficLight.Yellow => "EVALUAR CON CUIDADO",
                _ => "NO COMPRAR"
            },
            Components = components,
            Gates = gates,
            Strengths = components
                .Where(c => c.Points > 0m)
                .OrderByDescending(c => c.Points)
                .Take(3)
                .Select(c => c.Explanation)
                .ToList(),
            Weaknesses = components
                .Where(c => c.PointsLost > 0.5m)
                .OrderByDescending(c => c.PointsLost)
                .Take(3)
                .Select(c => c.Explanation)
                .ToList()
        };
    }

    // ---------- Componentes ----------

    private static (string, string, decimal, decimal, string) BuildProfitability(
        ScoringInputs inputs, AnalysisParameters parameters, ScoreWeights weights)
    {
        var target = parameters.MinRoiAnnual;
        var roi = inputs.Metrics.RoiAnnualized;

        var normalized = target > 0m
            ? MoneyMath.Clamp01(roi / (2m * target)) * 100m
            : (roi > 0m ? 100m : 0m);

        var explanation = roi <= 0m
            ? $"Rentabilidad anualizada negativa ({Clp.Percent(roi)}) al precio evaluado."
            : $"Rentabilidad anualizada de {Clp.AnnualizedPercent(roi)} frente a un objetivo de {Clp.Percent(target)}.";

        return ("profitability", "Rentabilidad", weights.Profitability, normalized, explanation);
    }

    private static (string, string, decimal, decimal, string) BuildBidHeadroom(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var maxBid = inputs.MaxBid.MaxBid;
        var headroom = maxBid > 0m ? (maxBid - inputs.EvaluationBidPrice) / maxBid : 0m;
        var normalized = MoneyMath.Clamp01(headroom * 4m) * 100m;

        var explanation = maxBid <= 0m
            ? "No hay puja máxima viable: la operación no deja utilidad a ningún precio."
            : headroom < 0m
                ? $"El precio evaluado ({Clp.Format(inputs.EvaluationBidPrice)}) supera la puja máxima ({Clp.Format(maxBid)})."
                : $"Queda {Clp.Percent(headroom)} de holgura entre el precio evaluado y la puja máxima de {Clp.Format(maxBid)}.";

        return ("bidHeadroom", "Holgura de puja", weights.BidHeadroom, normalized, explanation);
    }

    private static (string, string, decimal, decimal, string) BuildLiquidity(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var days = inputs.Metrics.DaysToSell;
        var normalized = MoneyMath.Clamp01(1m - (days - 15m) / 75m) * 100m;

        return ("liquidity", "Liquidez esperada", weights.Liquidity, normalized,
            $"Venta estimada en {days} días.");
    }

    private static (string, string, decimal, decimal, string) BuildMechanicalRisk(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var risk = MoneyMath.Clamp(inputs.Repair.MechanicalRiskScore, 0m, 100m);
        var normalized = 100m - risk;

        var explanation = inputs.Repair.HasStructuralDamage
            ? "Hay daño estructural registrado: el riesgo mecánico es alto y difícil de acotar."
            : $"Riesgo mecánico estimado en {risk:N0}/100 según daños registrados y nivel de inspección logrado.";

        return ("mechanicalRisk", "Riesgo mecánico", weights.MechanicalRisk, normalized, explanation);
    }

    private static (string, string, decimal, decimal, string) BuildDocumentRisk(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var factor = inputs.DocumentRisk.ToFactor();
        var normalized = 100m - factor * 100m;

        var explanation = inputs.DocumentRisk switch
        {
            DocumentRiskLevel.None => "Sin riesgo documental detectado.",
            DocumentRiskLevel.Low => "Riesgo documental bajo: trámites menores pendientes.",
            DocumentRiskLevel.Medium => "Riesgo documental medio: hay antecedentes por verificar antes de pujar.",
            _ => "Riesgo documental alto: gravámenes, encargo o limitaciones al dominio sin resolver."
        };

        return ("documentRisk", "Riesgo documental", weights.DocumentRisk, normalized, explanation);
    }

    private static (string, string, decimal, decimal, string) BuildEstimateCertainty(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var uncertainty = MoneyMath.Clamp01(inputs.Repair.UncertaintyRatio);
        var normalized = 100m - uncertainty * 100m;

        var explanation = $"El rango de reparación va de {Clp.Format(inputs.Repair.TotalMin)} a " +
                          $"{Clp.Format(inputs.Repair.TotalMax)} (incertidumbre {Clp.Percent(uncertainty)}).";

        return ("estimateCertainty", "Certeza de la estimación", weights.EstimateCertainty, normalized, explanation);
    }

    private static (string, string, decimal, decimal, string) BuildEvidenceQuality(
        ScoringInputs inputs, ScoreWeights weights)
    {
        var v = inputs.Valuation;

        var quantity = MoneyMath.Clamp01(v.ComparableCount / 8m);
        var freshness = MoneyMath.Clamp01(1m - v.AverageAgeDays / 90m);
        var consistency = MoneyMath.Clamp01(1m - MoneyMath.SafeDivide(v.DispersionPct, 0.40m, 1m));

        var normalized = (0.4m * quantity + 0.3m * freshness + 0.3m * consistency) * 100m;

        var explanation = v.ComparableCount == 0
            ? "No hay comparables de mercado cargados."
            : $"{v.ComparableCount} comparables, antigüedad promedio {v.AverageAgeDays:N0} días, " +
              $"dispersión {Clp.Percent(v.DispersionPct)}.";

        return ("evidenceQuality", "Calidad de la evidencia", weights.EvidenceQuality, normalized, explanation);
    }

    // ---------- Gates ----------

    private static IReadOnlyList<TriggeredGate> EvaluateGates(ScoringInputs inputs, AnalysisParameters parameters)
    {
        var gates = new List<TriggeredGate>();

        if (!inputs.MaxBid.IsViable)
        {
            gates.Add(new TriggeredGate
            {
                Code = GateCode.NotViable,
                Message = "La operación no alcanza la utilidad mínima exigida ni comprando a precio cero."
            });
        }
        else if (inputs.EvaluationBidPrice > inputs.MaxBid.MaxBid)
        {
            var excess = inputs.EvaluationBidPrice - inputs.MaxBid.MaxBid;
            gates.Add(new TriggeredGate
            {
                Code = GateCode.PriceAboveMaxBid,
                Message = $"El precio evaluado supera la puja máxima en {Clp.Format(excess)}."
            });
        }

        if (inputs.Metrics.RoiAnnualized < parameters.MinRoiAnnual)
        {
            gates.Add(new TriggeredGate
            {
                Code = GateCode.RoiBelowMinimum,
                Message = $"Rentabilidad anualizada de {Clp.Percent(inputs.Metrics.RoiAnnualized)}, " +
                          $"bajo el mínimo exigido de {Clp.Percent(parameters.MinRoiAnnual)}."
            });
        }

        if (!inputs.Valuation.HasEnoughEvidence)
        {
            gates.Add(new TriggeredGate
            {
                Code = GateCode.InsufficientMarketData,
                Message = $"Solo hay {inputs.Valuation.ComparableCount} comparables válidos; " +
                          $"se requieren al menos {parameters.MinComparables} para confiar en la valuación."
            });
        }

        if (inputs.DocumentRisk == DocumentRiskLevel.High)
        {
            gates.Add(new TriggeredGate
            {
                Code = GateCode.CriticalDocumentRisk,
                Message = "Riesgo documental crítico. No pujar hasta verificar el estado legal del vehículo."
            });
        }

        var pessimistic = inputs.Scenarios.FirstOrDefault(s => s.Kind == ScenarioKind.Pessimistic);
        if (pessimistic is not null)
        {
            var tolerated = pessimistic.Metrics.CashDeployed * parameters.MaxPessimisticLossPct;
            if (pessimistic.Metrics.Profit < -tolerated)
            {
                gates.Add(new TriggeredGate
                {
                    Code = GateCode.PessimisticLossExceedsLimit,
                    Message = $"En el escenario pesimista la pérdida sería de " +
                              $"{Clp.Format(Math.Abs(pessimistic.Metrics.Profit))}, sobre el máximo tolerado de " +
                              $"{Clp.Format(tolerated)}."
                });
            }
        }

        if (inputs.TotalCapital > 0m)
        {
            var limit = inputs.TotalCapital * parameters.MaxCapitalPerUnitPct;
            if (inputs.Metrics.CashDeployed > limit)
            {
                gates.Add(new TriggeredGate
                {
                    Code = GateCode.CapitalConcentration,
                    Message = $"La operación comprometería {Clp.Format(inputs.Metrics.CashDeployed)}, " +
                              $"sobre el límite de {Clp.Format(limit)} " +
                              $"({Clp.Percent(parameters.MaxCapitalPerUnitPct, 0)} del capital total)."
                });
            }
        }

        return gates;
    }

    private static TrafficLight DetermineTrafficLight(
        decimal score,
        IReadOnlyList<TriggeredGate> gates,
        ScoringInputs inputs,
        AnalysisParameters parameters)
    {
        if (gates.Count > 0) return TrafficLight.Red;
        if (inputs.EvaluationBidPrice > inputs.MaxBid.MaxBid) return TrafficLight.Red;
        if (score < parameters.YellowScoreThreshold) return TrafficLight.Red;

        var greenPriceCeiling = inputs.MaxBid.MaxBid * parameters.GreenPriceRatio;
        if (score >= parameters.GreenScoreThreshold && inputs.EvaluationBidPrice <= greenPriceCeiling)
            return TrafficLight.Green;

        return TrafficLight.Yellow;
    }
}
