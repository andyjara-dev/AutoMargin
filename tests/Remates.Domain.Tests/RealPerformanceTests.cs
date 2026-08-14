using Remates.Domain.Inventory;

namespace Remates.Domain.Tests;

public class RealPerformanceTests
{
    private static RealPerformanceInputs Sold(
        decimal hammer = 5_400_000m,
        decimal auctionCosts = 723_600m,
        decimal expenses = 1_100_000m,
        decimal salePrice = 11_000_000m,
        decimal saleCosts = 250_000m,
        int days = 40,
        decimal capitalMonthly = 0.015m,
        decimal taxPct = 0m) => new()
        {
            HammerPrice = hammer,
            AuctionCosts = auctionCosts,
            Expenses = expenses,
            SalePrice = salePrice,
            SaleCosts = saleCosts,
            DaysInInventory = days,
            CapitalCostMonthlyPct = capitalMonthly,
            ProfitTaxPct = taxPct
        };

    [Fact]
    public void La_utilidad_de_caja_es_la_venta_neta_menos_todo_lo_desembolsado()
    {
        var result = RealPerformanceCalculator.Calculate(Sold());

        // 5.400.000 + 723.600 + 1.100.000 = 7.223.600 desembolsados
        Assert.Equal(7_223_600m, result.TotalCashInvested);
        // 11.000.000 − 250.000 = 10.750.000 recibidos
        Assert.Equal(10_750_000m, result.NetSaleProceeds);
        Assert.Equal(3_526_400m, result.ProfitCash);
        Assert.True(result.IsClosed);
    }

    /// <summary>
    /// La distinción que evita creer que el negocio es mejor de lo que es: la utilidad económica
    /// descuenta el capital inmovilizado, y es la única comparable con lo que proyectó el análisis.
    /// </summary>
    [Fact]
    public void La_utilidad_economica_descuenta_el_costo_del_capital()
    {
        var result = RealPerformanceCalculator.Calculate(Sold());

        Assert.True(result.CapitalCost > 0m);
        Assert.Equal(result.ProfitCash - result.CapitalCost, result.ProfitEconomic);
        Assert.True(result.ProfitEconomic < result.ProfitCash);
    }

    [Fact]
    public void Dos_operaciones_con_igual_utilidad_de_caja_no_valen_lo_mismo_si_una_tarda_el_doble()
    {
        var fast = RealPerformanceCalculator.Calculate(Sold(days: 30));
        var slow = RealPerformanceCalculator.Calculate(Sold(days: 120));

        Assert.Equal(fast.ProfitCash, slow.ProfitCash);
        Assert.True(slow.CapitalCost > fast.CapitalCost);
        Assert.True(slow.ProfitEconomic < fast.ProfitEconomic);
        Assert.True(fast.RoiAnnualized > slow.RoiAnnualized);
    }

    [Fact]
    public void Un_vehiculo_sin_vender_queda_abierto_y_sin_rentabilidad_anualizada()
    {
        var result = RealPerformanceCalculator.Calculate(Sold(salePrice: 0m, saleCosts: 0m, days: 55));

        Assert.False(result.IsClosed);
        Assert.Equal(0m, result.NetSaleProceeds);
        Assert.Equal(0m, result.RoiAnnualized);
        // El capital sigue corriendo aunque no se haya vendido.
        Assert.True(result.CapitalCost > 0m);
        Assert.True(result.ProfitCash < 0m);
    }

    [Fact]
    public void Una_operacion_con_perdida_no_paga_impuesto()
    {
        var result = RealPerformanceCalculator.Calculate(Sold(salePrice: 6_000_000m, taxPct: 0.25m));

        Assert.True(result.ProfitCash < 0m);
        Assert.Equal(result.ProfitCash, result.ProfitAfterTax);
    }

    [Fact]
    public void Una_operacion_con_ganancia_si_descuenta_impuesto()
    {
        var result = RealPerformanceCalculator.Calculate(Sold(taxPct: 0.25m));

        Assert.True(result.ProfitAfterTax < result.ProfitCash);
    }

    [Fact]
    public void Sin_datos_no_divide_por_cero()
    {
        var result = RealPerformanceCalculator.Calculate(new RealPerformanceInputs
        {
            HammerPrice = 0m,
            DaysInInventory = 0
        });

        Assert.Equal(0m, result.RoiCash);
        Assert.Equal(0m, result.RoiEconomic);
        Assert.Equal(0m, result.MarginPct);
        Assert.Equal(0m, result.RoiAnnualized);
    }

    [Fact]
    public void Los_dias_negativos_se_tratan_como_cero()
    {
        var result = RealPerformanceCalculator.Calculate(Sold(days: -10));

        Assert.Equal(0, result.DaysInInventory);
        Assert.Equal(0m, result.CapitalCost);
    }
}

public class PredictionAccuracyTests
{
    private static PredictionAccuracyInputs Base(
        decimal predictedRepair = 920_000m,
        decimal actualRepair = 920_000m,
        int predictedDays = 35,
        int actualDays = 35,
        decimal predictedProfit = 3_300_000m,
        decimal actualProfit = 3_300_000m,
        decimal predictedSale = 11_439_000m,
        decimal actualSale = 11_439_000m) => new()
        {
            PredictedSaleValue = predictedSale,
            ActualSaleValue = actualSale,
            PredictedRepairCost = predictedRepair,
            ActualRepairCost = actualRepair,
            PredictedDays = predictedDays,
            ActualDays = actualDays,
            PredictedProfit = predictedProfit,
            ActualProfit = actualProfit
        };

    [Fact]
    public void Una_prediccion_exacta_no_tiene_error()
    {
        var result = PredictionAccuracyCalculator.Calculate(Base());

        Assert.Equal(0m, result.SaleValueErrorPct);
        Assert.Equal(0m, result.RepairCostErrorPct);
        Assert.Equal(0m, result.DaysErrorPct);
        Assert.Equal(0m, result.ProfitErrorPct);
        Assert.False(result.UnderPerformed);
    }

    /// <summary>
    /// El caso que el sistema debe poder detectar: subestimamos sistemáticamente la reparación.
    /// El signo positivo significa «costó más de lo estimado».
    /// </summary>
    [Fact]
    public void Subestimar_la_reparacion_da_error_positivo()
    {
        var result = PredictionAccuracyCalculator.Calculate(
            Base(predictedRepair: 1_000_000m, actualRepair: 1_210_000m));

        Assert.Equal(0.21m, result.RepairCostErrorPct);
    }

    [Fact]
    public void Tardar_mas_en_vender_da_error_positivo_en_dias()
    {
        var result = PredictionAccuracyCalculator.Calculate(Base(predictedDays: 30, actualDays: 60));

        Assert.Equal(1m, result.DaysErrorPct);
    }

    [Fact]
    public void Vender_por_debajo_de_lo_proyectado_da_error_negativo()
    {
        var result = PredictionAccuracyCalculator.Calculate(
            Base(predictedSale: 12_000_000m, actualSale: 11_400_000m));

        Assert.Equal(-0.05m, result.SaleValueErrorPct);
    }

    [Fact]
    public void Se_marca_cuando_la_utilidad_real_queda_bajo_la_proyectada()
    {
        var result = PredictionAccuracyCalculator.Calculate(
            Base(predictedProfit: 3_000_000m, actualProfit: 2_100_000m));

        Assert.True(result.UnderPerformed);
        Assert.Equal(-900_000m, result.ProfitDelta);
        Assert.Equal(-0.30m, result.ProfitErrorPct);
    }

    /// <summary>
    /// Con una utilidad proyectada negativa el signo del error debe seguir significando lo mismo:
    /// positivo es mejor de lo esperado.
    /// </summary>
    [Fact]
    public void El_signo_del_error_no_se_invierte_con_proyecciones_negativas()
    {
        var result = PredictionAccuracyCalculator.Calculate(
            Base(predictedProfit: -1_000_000m, actualProfit: -500_000m));

        Assert.Equal(0.5m, result.ProfitErrorPct);
        Assert.False(result.UnderPerformed);
    }

    [Fact]
    public void Sin_proyeccion_no_hay_error_que_medir()
    {
        var result = PredictionAccuracyCalculator.Calculate(
            Base(predictedRepair: 0m, actualRepair: 500_000m));

        Assert.Equal(0m, result.RepairCostErrorPct);
    }
}
