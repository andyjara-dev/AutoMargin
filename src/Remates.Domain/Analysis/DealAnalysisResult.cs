using Remates.Domain.Bidding;
using Remates.Domain.Damage;
using Remates.Domain.Financial;
using Remates.Domain.Market;
using Remates.Domain.Scoring;

namespace Remates.Domain.Analysis;

public sealed record DealAnalysisResult
{
    public required string FinancialEngineVersion { get; init; }
    public required string ScoringEngineVersion { get; init; }

    public required ValuationResult Valuation { get; init; }
    public required RepairEstimate Repair { get; init; }
    public required CostStructure CostStructure { get; init; }
    public required MaxBidResult MaxBid { get; init; }

    /// <summary>Resultado al precio actual del lote (o al precio que se está evaluando).</summary>
    public required DealMetrics MetricsAtCurrentPrice { get; init; }

    /// <summary>Resultado si se comprara exactamente en la puja máxima: es el piso de rentabilidad aceptable.</summary>
    public required DealMetrics MetricsAtMaxBid { get; init; }

    public required IReadOnlyList<ScenarioResult> Scenarios { get; init; }
    public required ScoreResult Score { get; init; }

    /// <summary>La terna que resume la decisión: dónde se pierde, dónde es el techo, dónde está el mercado hoy.</summary>
    public required decimal BreakevenBid { get; init; }
    public required decimal CurrentAuctionPrice { get; init; }

    /// <summary>Diferencia entre la puja máxima y el precio actual. Negativa significa que ya está caro.</summary>
    public required decimal Headroom { get; init; }

    public required IReadOnlyList<string> Disclaimers { get; init; }
}
