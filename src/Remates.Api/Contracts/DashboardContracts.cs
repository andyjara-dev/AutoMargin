using System.ComponentModel.DataAnnotations;
using Remates.Domain.Alerts;
using Remates.Infrastructure.Entities;

namespace Remates.Api.Contracts;

public sealed class CashMovementRequest
{
    /// <summary>Solo aportes y retiros se registran a mano; el resto los genera el propio ciclo.</summary>
    public CashMovementType Type { get; set; } = CashMovementType.Contribution;

    [Range(1, 999_999_999_999)]
    public decimal Amount { get; set; }

    public DateTimeOffset? MovementDate { get; set; }

    [MaxLength(300)]
    public string? Note { get; set; }
}

public sealed record CapitalSummary(
    decimal TotalContributed,
    decimal Available,
    decimal Immobilized,
    decimal ImmobilizedPct);

/// <summary>
/// Estado del inventario. El valor esperado y la utilidad potencial solo consideran los
/// vehículos que tienen una valuación: contar en cero los que no la tienen haría aparecer una
/// pérdida potencial inexistente. <see cref="UnvaluedCount"/> dice cuántos quedaron fuera.
/// </summary>
public sealed record InventorySummary(
    int InInventory,
    int Sold,
    int Analyzing,
    decimal InventoryCostValue,
    decimal InventoryExpectedValue,
    decimal PotentialProfit,
    decimal AverageDaysInInventory,
    int UnvaluedCount,
    decimal UnvaluedCost);

public sealed record ProfitSummary(
    decimal RealizedProfitCash,
    decimal RealizedProfitEconomic,
    decimal AverageRoiEconomic,
    decimal AverageMarginPct,
    decimal AverageDaysToSell,
    decimal ProfitLast30Days,
    int SalesLast30Days);

/// <summary>Rendimiento agregado por modelo. Con pocas ventas es anecdótico y se advierte.</summary>
public sealed record ModelPerformance(
    string Model,
    int Sales,
    decimal TotalProfit,
    decimal AverageProfit,
    decimal AverageRoi,
    decimal AverageDays);

public sealed record OpportunityRow(
    long VehicleId,
    string Label,
    decimal MaxBid,
    decimal CurrentPrice,
    decimal Headroom,
    decimal Score,
    string TrafficLight,
    decimal RoiAnnualized,
    int EstimatedDays,
    DateTimeOffset AnalyzedAt);

public sealed record DashboardSummary(
    CapitalSummary Capital,
    InventorySummary Inventory,
    ProfitSummary Profit,
    IReadOnlyList<ModelPerformance> BestModels,
    IReadOnlyList<ModelPerformance> WorstModels,
    IReadOnlyList<OpportunityRow> Opportunities,
    IReadOnlyList<Alert> Alerts,
    /// <summary>Ventas acumuladas. Bajo unas pocas decenas, los promedios no son concluyentes.</summary>
    int ClosedOperations);
