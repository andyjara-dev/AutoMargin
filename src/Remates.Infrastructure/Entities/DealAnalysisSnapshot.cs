using Remates.Domain.Scoring;

namespace Remates.Infrastructure.Entities;

/// <summary>
/// Fotografía inmutable de un análisis. No se recalcula nunca: guarda los números tal como se
/// vieron al decidir, junto con la versión de los motores y el conjunto de parámetros usado.
///
/// Sin esto no se puede auditar una compra pasada ni medir después si la predicción fue buena.
/// </summary>
public class DealAnalysisSnapshot : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public long? AuctionLotId { get; set; }
    public AuctionLot? AuctionLot { get; set; }

    public long ParameterSetId { get; set; }
    public ParameterSet? ParameterSet { get; set; }

    public required string FinancialEngineVersion { get; set; }
    public required string ScoringEngineVersion { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    // ---- Mercado ----
    public decimal SaleValueOptimistic { get; set; }
    public decimal SaleValueExpected { get; set; }
    public decimal SaleValueConservative { get; set; }
    public int ComparableCount { get; set; }

    // ---- Costos ----
    public decimal NetSaleValue { get; set; }
    public decimal TotalFixedCosts { get; set; }
    public decimal ProportionalRate { get; set; }
    public decimal CapitalFactor { get; set; }
    public decimal RepairExpected { get; set; }

    // ---- Decisión ----
    public decimal BreakevenBid { get; set; }
    public decimal TheoreticalMaxBid { get; set; }
    public decimal SafetyMarginPct { get; set; }
    public decimal MaxBid { get; set; }
    public decimal RequiredProfit { get; set; }
    public decimal CurrentAuctionPrice { get; set; }
    public decimal Headroom { get; set; }

    // ---- Resultado al precio evaluado ----
    public decimal ExpectedProfit { get; set; }
    public decimal RoiSimple { get; set; }
    public decimal RoiAnnualized { get; set; }
    public decimal MarginPct { get; set; }
    public int EstimatedDaysToSell { get; set; }

    // ---- Veredicto ----
    public decimal Score { get; set; }
    public TrafficLight TrafficLight { get; set; }

    // ---- Detalle completo, para poder reconstruir la pantalla tal cual se vio ----
    public string? GatesJson { get; set; }
    public string? ScoreBreakdownJson { get; set; }
    public string? CostBreakdownJson { get; set; }
    public string? ScenariosJson { get; set; }
    public string? InputsJson { get; set; }
}
