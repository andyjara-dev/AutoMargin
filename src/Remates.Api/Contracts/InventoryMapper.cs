using Remates.Infrastructure.Entities;

namespace Remates.Api.Contracts;

public static class InventoryMapper
{
    public static PurchaseResponse ToResponse(this Purchase p) => new(
        p.Id, p.VehicleId, p.AuctionLotId, p.DealAnalysisId,
        p.HammerPrice, p.CommissionPaid, p.PurchaseDate, p.InvoiceRef, p.Note);

    public static ExpenseResponse ToResponse(this Expense e) => new(
        e.Id, e.VehicleId, e.Category, e.Amount, e.ExpenseDate,
        e.Description, e.Supplier, e.DocumentRef, e.BudgetedAmount);

    public static ListingResponse ToResponse(this Listing l) => new(
        l.Id, l.VehicleId, l.Channel, l.ListPrice, l.PublishedAt, l.UnpublishedAt, l.Url);

    public static PriceChangeResponse ToResponse(this PriceChange c) => new(
        c.Id, c.ListingId, c.OldPrice, c.NewPrice, c.ChangedAt, c.Reason);

    public static SaleResponse ToResponse(this Sale s) => new(
        s.Id, s.VehicleId, s.SalePrice, s.SaleCosts, s.SaleDate,
        s.BuyerName, s.PaymentMethod, s.DaysInInventory,
        s.TotalCashInvested, s.CapitalCost,
        s.RealProfitCash, s.RealProfitEconomic,
        s.RealRoiCash, s.RealRoiEconomic, s.RealRoiAnnualized, s.RealMarginPct);
}
