namespace Remates.Domain.Financial;

public sealed record CostLine
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required decimal Amount { get; init; }

    /// <summary>True si el monto depende del precio de adjudicación (comisión, impuesto de transferencia).</summary>
    public bool IsProportionalToBid { get; init; }
}

/// <summary>
/// Estructura de costos de una operación, separada en la parte que NO depende del precio de martillo
/// (<see cref="FixedCosts"/>) y la que sí (<see cref="ProportionalRate"/>).
///
/// Esa separación es lo que permite despejar la puja máxima algebraicamente en lugar de tantear.
/// </summary>
public sealed record CostStructure
{
    /// <summary>Valor de mercado conservador, antes de descontar costos proporcionales a la venta.</summary>
    public required decimal GrossSaleValue { get; init; }

    /// <summary>Venta neta (S): lo que realmente queda del precio de venta. Alimenta todas las fórmulas.</summary>
    public required decimal NetSaleValue { get; init; }

    /// <summary>F: costos posteriores a la compra que no dependen del precio de martillo.</summary>
    public required decimal FixedCosts { get; init; }

    /// <summary>α: tasa total proporcional al precio de martillo.</summary>
    public required decimal ProportionalRate { get; init; }

    /// <summary>k = 1 + costo_capital_mensual × (días/30).</summary>
    public required decimal CapitalFactor { get; init; }

    public required int DaysToSell { get; init; }
    public required decimal ProfitTaxPct { get; init; }

    public required IReadOnlyList<CostLine> FixedCostLines { get; init; }
    public required IReadOnlyList<CostLine> SaleDeductionLines { get; init; }
}
