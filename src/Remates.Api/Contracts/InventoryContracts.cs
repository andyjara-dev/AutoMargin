using System.ComponentModel.DataAnnotations;
using Remates.Domain.Inventory;
using Remates.Infrastructure.Entities;

namespace Remates.Api.Contracts;

public sealed class RegisterPurchaseRequest
{
    [Range(1, 999_999_999)] public decimal HammerPrice { get; set; }
    [Range(0, 999_999_999)] public decimal CommissionPaid { get; set; }

    public DateTimeOffset? PurchaseDate { get; set; }
    public long? AuctionLotId { get; set; }

    [MaxLength(80)] public string? InvoiceRef { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}

public sealed class RegisterExpenseRequest
{
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Other;

    [Range(0, 999_999_999)] public decimal Amount { get; set; }
    public DateTimeOffset? ExpenseDate { get; set; }

    [MaxLength(300)] public string? Description { get; set; }
    [MaxLength(160)] public string? Supplier { get; set; }
    [MaxLength(80)] public string? DocumentRef { get; set; }
}

public sealed class PublishListingRequest
{
    [Required, MaxLength(80)] public string Channel { get; set; } = string.Empty;
    [Range(1, 999_999_999)] public decimal ListPrice { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    [MaxLength(600)] public string? Url { get; set; }
}

public sealed class ChangePriceRequest
{
    [Range(1, 999_999_999)] public decimal NewPrice { get; set; }
    [MaxLength(300)] public string? Reason { get; set; }
}

public sealed class RegisterSaleRequest
{
    [Range(1, 999_999_999)] public decimal SalePrice { get; set; }
    [Range(0, 999_999_999)] public decimal SaleCosts { get; set; }

    public DateTimeOffset? SaleDate { get; set; }

    [MaxLength(160)] public string? BuyerName { get; set; }
    [MaxLength(80)] public string? PaymentMethod { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}

// ---------- Respuestas ----------
// Se devuelven DTOs y no entidades de EF a propósito: una entidad rastreada arrastra sus
// navegaciones y el serializador entra en ciclo (vehículo → historial → vehículo → …).
// Además evita exponer columnas internas por accidente.

public sealed record PurchaseResponse(
    long Id, long VehicleId, long? AuctionLotId, long? DealAnalysisId,
    decimal HammerPrice, decimal CommissionPaid, DateTimeOffset PurchaseDate,
    string? InvoiceRef, string? Note);

public sealed record ExpenseResponse(
    long Id, long VehicleId, ExpenseCategory Category, decimal Amount,
    DateTimeOffset ExpenseDate, string? Description, string? Supplier,
    string? DocumentRef, decimal? BudgetedAmount);

public sealed record ListingResponse(
    long Id, long VehicleId, string Channel, decimal ListPrice,
    DateTimeOffset PublishedAt, DateTimeOffset? UnpublishedAt, string? Url);

public sealed record PriceChangeResponse(
    long Id, long ListingId, decimal OldPrice, decimal NewPrice,
    DateTimeOffset ChangedAt, string? Reason);

public sealed record SaleResponse(
    long Id, long VehicleId, decimal SalePrice, decimal SaleCosts, DateTimeOffset SaleDate,
    string? BuyerName, string? PaymentMethod, int DaysInInventory,
    decimal TotalCashInvested, decimal CapitalCost,
    decimal RealProfitCash, decimal RealProfitEconomic,
    decimal RealRoiCash, decimal RealRoiEconomic, decimal RealRoiAnnualized, decimal RealMarginPct);

/// <summary>
/// Gasto real de una categoría contra lo presupuestado. El presupuesto es nulo cuando el
/// análisis no estima esa categoría por separado: mostrar 0 haría parecer que todo lo gastado
/// es sobrecosto.
/// </summary>
public sealed record ExpenseByCategory(
    ExpenseCategory Category,
    decimal Actual,
    decimal? Budgeted,
    decimal? Variance,
    decimal? VariancePct,
    int Count);

/// <summary>
/// Reparación agrupada. El análisis la estima como una sola cifra, pero el gasto real se
/// registra separado en repuestos y mano de obra. Compararlos por separado haría creer que
/// la reparación va bajo presupuesto cuando no lo está.
/// </summary>
public sealed record RepairSummary(
    decimal Budgeted,
    decimal Actual,
    decimal Variance,
    decimal? VariancePct,
    bool OverBudget);

/// <summary>Estado económico de un vehículo comprado, esté vendido o no.</summary>
public sealed record VehicleFinancials(
    long VehicleId,
    string Label,
    VehicleStatus Status,
    decimal? HammerPrice,
    decimal? CommissionPaid,
    decimal TotalExpenses,
    decimal TotalBudgeted,
    IReadOnlyList<ExpenseByCategory> ExpensesByCategory,
    RepairSummary Repair,
    decimal? ListPrice,
    decimal? SalePrice,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? SaleDate,
    RealPerformance Performance,
    PredictionComparison? Comparison);

public sealed record PredictionComparison(
    decimal PredictedSaleValue,
    decimal ActualSaleValue,
    decimal PredictedRepairCost,
    decimal ActualRepairCost,
    int PredictedDays,
    int ActualDays,
    decimal PredictedProfit,
    decimal ActualProfit,
    decimal SaleValueErrorPct,
    decimal RepairCostErrorPct,
    decimal DaysErrorPct,
    decimal ProfitErrorPct,
    bool UnderPerformed);
