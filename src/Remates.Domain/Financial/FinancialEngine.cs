using Remates.Domain.Common;
using Remates.Domain.Parameters;

namespace Remates.Domain.Financial;

/// <summary>
/// Motor financiero. Todo el dinero del sistema se calcula aquí, de forma determinística.
/// Ningún modelo de lenguaje participa en estos números.
///
/// Modelo:
///   Costo_total(P) = P(1+α)k + Fk
///   Utilidad(P)    = S − Costo_total(P)
/// donde S = venta neta, F = costos fijos post-compra, α = tasa proporcional al martillo,
/// k = factor de costo de capital por el tiempo estimado en inventario.
/// </summary>
public static class FinancialEngine
{
    public const string EngineVersion = "1.0.0";

    /// <summary>
    /// Construye la estructura de costos de la operación. Es el paso previo a evaluar cualquier precio.
    /// </summary>
    /// <param name="grossSaleValue">Valor de mercado conservador.</param>
    /// <param name="repairExpected">Reparación esperada (valor central del rango).</param>
    /// <param name="transport">Traslado desde el recinto del remate.</param>
    /// <param name="detailing">Preparación estética previa a la publicación.</param>
    /// <param name="otherFixedCosts">Bodegaje, peritajes, gastos varios ya conocidos.</param>
    /// <param name="daysToSell">Días estimados hasta la venta. Determina el costo de capital.</param>
    public static CostStructure BuildCostStructure(
        decimal grossSaleValue,
        decimal repairExpected,
        decimal transport,
        decimal detailing,
        decimal otherFixedCosts,
        int daysToSell,
        AnalysisParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var days = daysToSell > 0 ? daysToSell : parameters.DefaultDaysToSell;

        // Deducciones proporcionales al precio de venta.
        var warranty = grossSaleValue * parameters.WarrantyProvisionPct;
        var marketing = grossSaleValue * parameters.MarketingPct;
        var netSaleValue = grossSaleValue - warranty - marketing;

        // La contingencia solo se aplica sobre los costos que estimamos nosotros, no sobre los ya conocidos.
        var controllable = repairExpected + transport + detailing;
        var contingency = controllable * parameters.ContingencyPct;

        var fixedCosts = controllable
                       + contingency
                       + parameters.TransferFixed
                       + parameters.AdminFeeFixed
                       + otherFixedCosts;

        var capitalFactor = 1m + parameters.CapitalCostMonthlyPct * (days / 30m);

        return new CostStructure
        {
            GrossSaleValue = MoneyMath.RoundToPeso(grossSaleValue),
            NetSaleValue = MoneyMath.RoundToPeso(netSaleValue),
            FixedCosts = MoneyMath.RoundToPeso(fixedCosts),
            ProportionalRate = MoneyMath.RoundRate(parameters.ProportionalRate),
            CapitalFactor = Math.Round(capitalFactor, 6),
            DaysToSell = days,
            ProfitTaxPct = parameters.ProfitTaxPct,
            FixedCostLines =
            [
                new CostLine { Key = "repair", Label = "Reparaciones (esperado)", Amount = MoneyMath.RoundToPeso(repairExpected) },
                new CostLine { Key = "transport", Label = "Transporte", Amount = MoneyMath.RoundToPeso(transport) },
                new CostLine { Key = "detailing", Label = "Detailing", Amount = MoneyMath.RoundToPeso(detailing) },
                new CostLine { Key = "contingency", Label = $"Imprevistos ({parameters.ContingencyPct:P1})", Amount = MoneyMath.RoundToPeso(contingency) },
                new CostLine { Key = "transferFixed", Label = "Transferencia (trámite)", Amount = MoneyMath.RoundToPeso(parameters.TransferFixed) },
                new CostLine { Key = "adminFixed", Label = "Gastos administrativos remate", Amount = MoneyMath.RoundToPeso(parameters.AdminFeeFixed) },
                new CostLine { Key = "other", Label = "Otros gastos", Amount = MoneyMath.RoundToPeso(otherFixedCosts) }
            ],
            SaleDeductionLines =
            [
                new CostLine { Key = "warranty", Label = $"Provisión garantía ({parameters.WarrantyProvisionPct:P1})", Amount = MoneyMath.RoundToPeso(warranty) },
                new CostLine { Key = "marketing", Label = $"Publicación y marketing ({parameters.MarketingPct:P1})", Amount = MoneyMath.RoundToPeso(marketing) }
            ]
        };
    }

    /// <summary>Evalúa la operación a un precio de adjudicación concreto.</summary>
    public static DealMetrics Evaluate(CostStructure structure, decimal bidPrice)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var proportionalCosts = bidPrice * structure.ProportionalRate;
        var cashDeployed = bidPrice + proportionalCosts + structure.FixedCosts;
        var totalCost = cashDeployed * structure.CapitalFactor;
        var capitalCost = totalCost - cashDeployed;

        var profit = structure.NetSaleValue - totalCost;
        var tax = profit > 0m ? profit * structure.ProfitTaxPct : 0m;

        var roi = MoneyMath.SafeDivide(profit, totalCost);

        return new DealMetrics
        {
            BidPrice = MoneyMath.RoundToPeso(bidPrice),
            ProportionalCosts = MoneyMath.RoundToPeso(proportionalCosts),
            CashDeployed = MoneyMath.RoundToPeso(cashDeployed),
            CapitalCost = MoneyMath.RoundToPeso(capitalCost),
            TotalCost = MoneyMath.RoundToPeso(totalCost),
            Profit = MoneyMath.RoundToPeso(profit),
            ProfitAfterTax = MoneyMath.RoundToPeso(profit - tax),
            RoiSimple = MoneyMath.RoundRate(roi),
            RoiAnnualized = MoneyMath.RoundRate(MoneyMath.Annualize(roi, structure.DaysToSell)),
            MarginPct = MoneyMath.RoundRate(MoneyMath.SafeDivide(profit, structure.GrossSaleValue)),
            DaysToSell = structure.DaysToSell
        };
    }

    /// <summary>
    /// Precio de adjudicación en el que la utilidad es exactamente cero: P_be = (S/k − F) / (1+α).
    /// Sobre este número no hay negocio, solo pérdida.
    /// </summary>
    public static decimal BreakevenBid(CostStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);

        var denominator = 1m + structure.ProportionalRate;
        if (denominator <= 0m || structure.CapitalFactor <= 0m) return 0m;

        var value = (structure.NetSaleValue / structure.CapitalFactor - structure.FixedCosts) / denominator;
        return MoneyMath.FloorToPeso(Math.Max(0m, value));
    }
}
