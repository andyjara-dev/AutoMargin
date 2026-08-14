using Remates.Domain.Common;
using Remates.Domain.Financial;
using Remates.Domain.Parameters;

namespace Remates.Domain.Tests;

public class FinancialEngineTests
{
    [Fact]
    public void El_punto_de_equilibrio_deja_utilidad_cero()
    {
        var parameters = AnalysisParameters.Default;
        var structure = FinancialEngine.BuildCostStructure(
            11_500_000m, 850_000m, 150_000m, 150_000m, 300_000m, 45, parameters);

        var breakeven = FinancialEngine.BreakevenBid(structure);
        var metrics = FinancialEngine.Evaluate(structure, breakeven);

        // El precio se trunca al peso, así que la utilidad queda en cero o apenas sobre cero.
        Assert.InRange(metrics.Profit, 0m, 3m);
    }

    [Fact]
    public void La_contingencia_solo_se_aplica_sobre_los_costos_estimados()
    {
        var parameters = TestParameters.Neutral with { ContingencyPct = 0.10m, TransferFixed = 25_000m };

        var structure = FinancialEngine.BuildCostStructure(
            grossSaleValue: 10_000_000m,
            repairExpected: 1_000_000m,
            transport: 100_000m,
            detailing: 100_000m,
            otherFixedCosts: 500_000m,
            daysToSell: 30,
            parameters);

        // controlables = 1.200.000 → contingencia 120.000; los otros gastos y la transferencia no la llevan.
        Assert.Equal(1_000_000m + 100_000m + 100_000m + 120_000m + 25_000m + 500_000m, structure.FixedCosts);
    }

    [Fact]
    public void El_costo_de_capital_crece_con_los_dias_en_inventario()
    {
        var parameters = AnalysisParameters.Default with { CapitalCostMonthlyPct = 0.015m };

        var fast = FinancialEngine.BuildCostStructure(10_000_000m, 500_000m, 0m, 0m, 0m, 30, parameters);
        var slow = FinancialEngine.BuildCostStructure(10_000_000m, 500_000m, 0m, 0m, 0m, 120, parameters);

        var fastMetrics = FinancialEngine.Evaluate(fast, 7_000_000m);
        var slowMetrics = FinancialEngine.Evaluate(slow, 7_000_000m);

        Assert.True(slowMetrics.CapitalCost > fastMetrics.CapitalCost);
        Assert.True(slowMetrics.Profit < fastMetrics.Profit);
    }

    /// <summary>
    /// El punto que motivó incluir el tiempo en el MVP: la misma rentabilidad simple en distinto
    /// plazo son negocios muy distintos.
    /// </summary>
    [Fact]
    public void La_misma_rentabilidad_simple_en_menos_dias_anualiza_mucho_mas()
    {
        var fast = MoneyMath.Annualize(0.15m, 20);
        var slow = MoneyMath.Annualize(0.15m, 120);

        Assert.True(fast > slow * 3m,
            $"15% en 20 días ({fast:P0}) debe superar ampliamente 15% en 120 días ({slow:P0}).");
    }

    [Fact]
    public void Sin_dias_de_venta_se_usa_el_valor_por_defecto_de_los_parametros()
    {
        var parameters = AnalysisParameters.Default with { DefaultDaysToSell = 45 };
        var structure = FinancialEngine.BuildCostStructure(10_000_000m, 0m, 0m, 0m, 0m, 0, parameters);

        Assert.Equal(45, structure.DaysToSell);
    }

    [Fact]
    public void Una_operacion_completamente_vacia_no_provoca_division_por_cero()
    {
        var structure = FinancialEngine.BuildCostStructure(0m, 0m, 0m, 0m, 0m, 30, TestParameters.Neutral);

        var metrics = FinancialEngine.Evaluate(structure, 0m);

        Assert.Equal(0m, metrics.MarginPct);
        Assert.Equal(0m, metrics.RoiSimple);
        Assert.Equal(0m, metrics.RoiAnnualized);
        Assert.Equal(0m, FinancialEngine.BreakevenBid(structure));
    }

    /// <summary>
    /// Con costos reales y venta cero se pierde todo lo desembolsado: -100% es el resultado correcto,
    /// no un error de cálculo.
    /// </summary>
    [Fact]
    public void Vender_en_cero_habiendo_gastado_da_perdida_total()
    {
        var structure = FinancialEngine.BuildCostStructure(0m, 0m, 0m, 0m, 0m, 30, AnalysisParameters.Default);

        var metrics = FinancialEngine.Evaluate(structure, 0m);

        Assert.True(metrics.TotalCost > 0m);
        Assert.Equal(-1m, metrics.RoiSimple);
        Assert.Equal(0m, FinancialEngine.BreakevenBid(structure));
    }

    [Fact]
    public void El_impuesto_a_la_utilidad_no_se_aplica_cuando_hay_perdida()
    {
        var parameters = AnalysisParameters.Default with { ProfitTaxPct = 0.25m };
        var structure = FinancialEngine.BuildCostStructure(5_000_000m, 0m, 0m, 0m, 0m, 30, parameters);

        var loss = FinancialEngine.Evaluate(structure, 8_000_000m);
        var gain = FinancialEngine.Evaluate(structure, 1_000_000m);

        Assert.True(loss.Profit < 0m);
        Assert.Equal(loss.Profit, loss.ProfitAfterTax);
        Assert.True(gain.ProfitAfterTax < gain.Profit);
    }

    [Fact]
    public void La_venta_neta_descuenta_garantia_y_marketing()
    {
        var parameters = TestParameters.Neutral with
        {
            WarrantyProvisionPct = 0.02m,
            MarketingPct = 0.005m
        };

        var structure = FinancialEngine.BuildCostStructure(10_000_000m, 0m, 0m, 0m, 0m, 30, parameters);

        Assert.Equal(10_000_000m, structure.GrossSaleValue);
        Assert.Equal(9_750_000m, structure.NetSaleValue);
    }
}
