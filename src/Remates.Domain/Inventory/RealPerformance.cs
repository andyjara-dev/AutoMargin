namespace Remates.Domain.Inventory;

/// <summary>Categorías de gasto real. Se corresponden con las líneas del costo estimado.</summary>
public enum ExpenseCategory
{
    AuctionFee = 1,
    Transport = 2,
    Repair = 3,
    Parts = 4,
    Labor = 5,
    Detailing = 6,
    Transfer = 7,
    Storage = 8,
    Marketing = 9,
    Warranty = 10,
    Other = 99
}

public sealed record RealPerformanceInputs
{
    /// <summary>Precio de adjudicación efectivamente pagado.</summary>
    public required decimal HammerPrice { get; init; }

    /// <summary>Comisión del martillero y gastos del remate realmente pagados.</summary>
    public decimal AuctionCosts { get; init; }

    /// <summary>Suma de todos los gastos posteriores registrados.</summary>
    public decimal Expenses { get; init; }

    /// <summary>Precio al que se vendió. En 0 el vehículo aún está en inventario.</summary>
    public decimal SalePrice { get; init; }

    /// <summary>Comisiones de venta, publicación pagada, provisión de garantía consumida.</summary>
    public decimal SaleCosts { get; init; }

    /// <summary>Días transcurridos entre la compra y la venta, o hasta hoy si no se ha vendido.</summary>
    public required int DaysInInventory { get; init; }

    /// <summary>Costo mensual del capital, para calcular la utilidad económica.</summary>
    public decimal CapitalCostMonthlyPct { get; init; }

    /// <summary>Impuesto sobre la utilidad. Se informa aparte; no altera la utilidad de caja.</summary>
    public decimal ProfitTaxPct { get; init; }
}

/// <summary>
/// Resultado real de una operación cerrada o en curso.
///
/// Se reportan dos utilidades a propósito, porque responden preguntas distintas:
///
///  - <see cref="ProfitCash"/> es lo que efectivamente entró al bolsillo. Es el número del
///    contador y el que cuadra con la cuenta corriente.
///  - <see cref="ProfitEconomic"/> descuenta además el costo del capital inmovilizado durante
///    los días que el vehículo estuvo en inventario. Es el único comparable con la utilidad
///    que proyectó el análisis, porque aquella también lo descontaba.
///
/// Confundirlas hace que el negocio parezca mejor de lo que es: dos operaciones con la misma
/// utilidad de caja pero una del doble de duración no valen lo mismo.
/// </summary>
public sealed record RealPerformance
{
    /// <summary>Efectivo total desembolsado: compra + costos de remate + gastos.</summary>
    public required decimal TotalCashInvested { get; init; }

    /// <summary>Costo del capital inmovilizado durante los días en inventario.</summary>
    public required decimal CapitalCost { get; init; }

    /// <summary>Ingreso neto de la venta, ya descontados los costos de vender.</summary>
    public required decimal NetSaleProceeds { get; init; }

    public required decimal ProfitCash { get; init; }
    public required decimal ProfitEconomic { get; init; }
    public required decimal ProfitAfterTax { get; init; }

    public required decimal RoiCash { get; init; }
    public required decimal RoiEconomic { get; init; }

    /// <summary>ROI económico anualizado. Es lo que permite comparar operaciones entre sí.</summary>
    public required decimal RoiAnnualized { get; init; }

    /// <summary>Utilidad de caja sobre el precio de venta.</summary>
    public required decimal MarginPct { get; init; }

    public required int DaysInInventory { get; init; }

    /// <summary>False mientras el vehículo no se haya vendido: las cifras son provisionales.</summary>
    public required bool IsClosed { get; init; }
}
