using Remates.Domain.Common;

namespace Remates.Domain.Inventory;

/// <summary>
/// Calcula el resultado real de una operación con los montos efectivamente pagados y cobrados.
/// Determinístico, como todo lo que toca dinero en este sistema.
/// </summary>
public static class RealPerformanceCalculator
{
    public const string EngineVersion = "1.0.0";

    public static RealPerformance Calculate(RealPerformanceInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var days = Math.Max(0, inputs.DaysInInventory);
        var cashInvested = inputs.HammerPrice + inputs.AuctionCosts + inputs.Expenses;

        // El capital corre desde la compra, se haya vendido o no.
        var capitalCost = cashInvested * inputs.CapitalCostMonthlyPct * (days / 30m);

        var isClosed = inputs.SalePrice > 0m;
        var netProceeds = isClosed ? inputs.SalePrice - inputs.SaleCosts : 0m;

        var profitCash = netProceeds - cashInvested;
        var profitEconomic = profitCash - capitalCost;

        var tax = profitCash > 0m ? profitCash * inputs.ProfitTaxPct : 0m;

        var roiCash = MoneyMath.SafeDivide(profitCash, cashInvested);
        var roiEconomic = MoneyMath.SafeDivide(profitEconomic, cashInvested + capitalCost);

        return new RealPerformance
        {
            TotalCashInvested = MoneyMath.RoundToPeso(cashInvested),
            CapitalCost = MoneyMath.RoundToPeso(capitalCost),
            NetSaleProceeds = MoneyMath.RoundToPeso(netProceeds),

            ProfitCash = MoneyMath.RoundToPeso(profitCash),
            ProfitEconomic = MoneyMath.RoundToPeso(profitEconomic),
            ProfitAfterTax = MoneyMath.RoundToPeso(profitCash - tax),

            RoiCash = MoneyMath.RoundRate(roiCash),
            RoiEconomic = MoneyMath.RoundRate(roiEconomic),
            // Anualizar una operación abierta no significa nada: todavía no hay retorno.
            RoiAnnualized = isClosed
                ? MoneyMath.RoundRate(MoneyMath.Annualize(roiEconomic, days))
                : 0m,

            MarginPct = MoneyMath.RoundRate(MoneyMath.SafeDivide(profitCash, inputs.SalePrice)),
            DaysInInventory = days,
            IsClosed = isClosed
        };
    }
}
