namespace Remates.Domain.Financial;

/// <summary>Resultado económico de la operación evaluada a un precio de martillo concreto.</summary>
public sealed record DealMetrics
{
    /// <summary>Precio de adjudicación evaluado (P).</summary>
    public required decimal BidPrice { get; init; }

    /// <summary>Comisión del martillero + IVA + impuesto de transferencia: P × α.</summary>
    public required decimal ProportionalCosts { get; init; }

    /// <summary>Efectivo total desembolsado antes del costo de capital: P(1+α) + F.</summary>
    public required decimal CashDeployed { get; init; }

    /// <summary>Costo del capital inmovilizado durante los días estimados de venta.</summary>
    public required decimal CapitalCost { get; init; }

    /// <summary>Costo total de la operación: P(1+α)k + Fk.</summary>
    public required decimal TotalCost { get; init; }

    public required decimal Profit { get; init; }
    public required decimal ProfitAfterTax { get; init; }

    /// <summary>Utilidad / costo total.</summary>
    public required decimal RoiSimple { get; init; }

    /// <summary>(1 + ROI)^(365/días) − 1. La métrica que realmente compara oportunidades entre sí.</summary>
    public required decimal RoiAnnualized { get; init; }

    /// <summary>Utilidad / precio de venta bruto.</summary>
    public required decimal MarginPct { get; init; }

    public required int DaysToSell { get; init; }
}
