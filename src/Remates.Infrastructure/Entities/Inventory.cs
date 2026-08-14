using Remates.Domain.Inventory;

namespace Remates.Infrastructure.Entities;

public class Purchase : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public long? AuctionLotId { get; set; }
    public AuctionLot? AuctionLot { get; set; }

    /// <summary>Precio de adjudicación efectivamente pagado.</summary>
    public decimal HammerPrice { get; set; }

    /// <summary>Comisión del martillero y gastos del remate. Se registran aparte del precio.</summary>
    public decimal CommissionPaid { get; set; }

    public DateTimeOffset PurchaseDate { get; set; }
    public string? InvoiceRef { get; set; }
    public string? Note { get; set; }

    /// <summary>
    /// Análisis vigente al momento de comprar. Es el que después se compara contra el
    /// resultado real; sin esta referencia no se puede medir si la proyección fue buena.
    /// </summary>
    public long? DealAnalysisId { get; set; }
    public DealAnalysisSnapshot? DealAnalysis { get; set; }
}

public class Expense : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset ExpenseDate { get; set; }

    public string? Description { get; set; }
    public string? Supplier { get; set; }
    public string? DocumentRef { get; set; }

    /// <summary>
    /// Lo que el análisis había estimado para esta categoría. Guardarlo aquí permite comparar
    /// presupuesto contra real sin recalcular nada después.
    /// </summary>
    public decimal? BudgetedAmount { get; set; }
}

public class Listing : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public required string Channel { get; set; }
    public decimal ListPrice { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public DateTimeOffset? UnpublishedAt { get; set; }
    public string? Url { get; set; }

    public ICollection<PriceChange> PriceChanges { get; set; } = [];
}

/// <summary>
/// Cada ajuste de precio publicado. Se conserva el historial porque cuántas veces hubo que
/// bajar el precio antes de vender es una señal de que la valuación inicial estaba alta.
/// </summary>
public class PriceChange : AuditableEntity
{
    public long ListingId { get; set; }
    public Listing? Listing { get; set; }

    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Reason { get; set; }
}

public class Sale : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public decimal SalePrice { get; set; }

    /// <summary>Comisiones de venta, transferencia asumida, garantía consumida.</summary>
    public decimal SaleCosts { get; set; }

    public DateTimeOffset SaleDate { get; set; }
    public string? BuyerName { get; set; }
    public string? PaymentMethod { get; set; }
    public string? Note { get; set; }

    // ---- Resultado real, calculado y congelado al cerrar la venta ----
    public int DaysInInventory { get; set; }
    public decimal TotalCashInvested { get; set; }
    public decimal CapitalCost { get; set; }
    public decimal RealProfitCash { get; set; }
    public decimal RealProfitEconomic { get; set; }
    public decimal RealRoiCash { get; set; }
    public decimal RealRoiEconomic { get; set; }
    public decimal RealRoiAnnualized { get; set; }
    public decimal RealMarginPct { get; set; }
}

public enum CashMovementType
{
    Contribution = 1,
    Withdrawal = 2,
    Purchase = 3,
    Expense = 4,
    SaleIncome = 5
}

/// <summary>
/// Movimientos de caja del negocio. Permiten responder cuánto capital está disponible y
/// cuánto inmovilizado sin tener que reconstruirlo sumando tablas.
/// </summary>
public class CashMovement : AuditableEntity
{
    public CashMovementType Type { get; set; }

    /// <summary>Positivo entra, negativo sale. El signo lo fija quien registra el movimiento.</summary>
    public decimal Amount { get; set; }

    public DateTimeOffset MovementDate { get; set; }

    public long? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public string? Note { get; set; }
}

/// <summary>
/// Predicción contra realidad de una operación cerrada.
///
/// Es la tabla más importante del sistema a largo plazo: sin ella no hay forma de saber si el
/// análisis acierta, ni datos con los que entrenar nada más adelante. Se llena sola al vender.
/// </summary>
public class PredictionOutcome : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public long DealAnalysisId { get; set; }
    public DealAnalysisSnapshot? DealAnalysis { get; set; }

    public long SaleId { get; set; }
    public Sale? Sale { get; set; }

    public decimal PredictedSaleValue { get; set; }
    public decimal ActualSaleValue { get; set; }

    public decimal PredictedRepairCost { get; set; }
    public decimal ActualRepairCost { get; set; }

    public int PredictedDays { get; set; }
    public int ActualDays { get; set; }

    public decimal PredictedProfit { get; set; }
    public decimal ActualProfit { get; set; }

    public decimal SaleValueErrorPct { get; set; }
    public decimal RepairCostErrorPct { get; set; }
    public decimal DaysErrorPct { get; set; }
    public decimal ProfitErrorPct { get; set; }
    public bool UnderPerformed { get; set; }

    public DateTimeOffset ClosedAt { get; set; }
}
