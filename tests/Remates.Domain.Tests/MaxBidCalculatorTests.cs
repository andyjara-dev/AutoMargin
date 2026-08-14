using Remates.Domain.Bidding;
using Remates.Domain.Financial;
using Remates.Domain.Parameters;

namespace Remates.Domain.Tests;

public class MaxBidCalculatorTests
{
    private static readonly UncertaintyInputs NoUncertainty = new()
    {
        RepairUncertainty = 0m,
        MarketDispersion = 0m,
        ComparableCount = 1000,
        DocumentRiskFactor = 0m
    };

    /// <summary>
    /// El ejemplo del enunciado, con los parámetros neutralizados para que el modelo se reduzca
    /// a la resta simple:
    ///   11.500.000 − 1.900.000 de costos − 1.500.000 de utilidad mínima = 8.100.000
    /// </summary>
    [Fact]
    public void Reproduce_el_ejemplo_manual_del_usuario()
    {
        var parameters = TestParameters.Neutral with
        {
            TransferFixed = 200_000m,   // transferencia
            AdminFeeFixed = 250_000m,   // comisión del remate, tomada como monto fijo
            MinProfitAbs = 1_500_000m
        };

        var structure = FinancialEngine.BuildCostStructure(
            grossSaleValue: 11_500_000m,
            repairExpected: 850_000m,
            transport: 150_000m,
            detailing: 150_000m,
            otherFixedCosts: 300_000m,
            daysToSell: 30,
            parameters);

        Assert.Equal(1_900_000m, structure.FixedCosts);

        var result = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);

        Assert.Equal(8_100_000m, result.MaxBid);
        Assert.Equal(1_500_000m, result.RequiredProfit);
        Assert.True(result.IsViable);
    }

    /// <summary>
    /// Invariante central: comprar exactamente en la puja máxima teórica deja exactamente
    /// la utilidad mínima exigida. Si esto falla, la fórmula está mal despejada.
    /// </summary>
    [Theory]
    [InlineData(0.10, 0.19, 30)]
    [InlineData(0.12, 0.19, 60)]
    [InlineData(0.00, 0.00, 15)]
    [InlineData(0.08, 0.19, 120)]
    public void Comprar_en_la_puja_teorica_deja_exactamente_la_utilidad_minima(
        double commission, double vat, int days)
    {
        var parameters = AnalysisParameters.Default with
        {
            CommissionPct = (decimal)commission,
            VatPct = (decimal)vat,
            SafetyMarginBase = 0m,
            SafetyMarginMin = 0m,
            SafetyMarginMax = 0m
        };

        var structure = FinancialEngine.BuildCostStructure(
            grossSaleValue: 12_000_000m,
            repairExpected: 900_000m,
            transport: 150_000m,
            detailing: 150_000m,
            otherFixedCosts: 0m,
            daysToSell: days,
            parameters);

        var result = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);
        var metrics = FinancialEngine.Evaluate(structure, result.TheoreticalMaxBid);

        // Tolerancia de un peso por el truncamiento del precio.
        Assert.InRange(metrics.Profit, result.RequiredProfit - 2m, result.RequiredProfit + 2m);
    }

    /// <summary>
    /// La comisión del martillero es proporcional al martillo, no un monto fijo. Restarla como
    /// constante sobrestima la puja: este test fija esa diferencia.
    /// </summary>
    [Fact]
    public void La_comision_proporcional_baja_mas_la_puja_que_tratarla_como_costo_fijo()
    {
        var proportional = AnalysisParameters.Default with
        {
            CommissionPct = 0.10m,
            SafetyMarginBase = 0m, SafetyMarginMin = 0m, SafetyMarginMax = 0m
        };

        // Mismo negocio, pero con la comisión estimada "a ojo" como $250.000 fijos.
        var asFixed = proportional with { CommissionPct = 0m, AdminFeeFixed = 250_000m };

        var structureProportional = FinancialEngine.BuildCostStructure(
            15_000_000m, 900_000m, 150_000m, 150_000m, 0m, 45, proportional);

        var structureFixed = FinancialEngine.BuildCostStructure(
            15_000_000m, 900_000m, 150_000m, 150_000m, 0m, 45, asFixed);

        var bidProportional = MaxBidCalculator.Calculate(structureProportional, NoUncertainty, proportional).MaxBid;
        var bidFixed = MaxBidCalculator.Calculate(structureFixed, NoUncertainty, asFixed).MaxBid;

        Assert.True(bidProportional < bidFixed,
            $"La puja con comisión proporcional ({bidProportional}) debe ser menor que la calculada con comisión fija ({bidFixed}).");
    }

    [Fact]
    public void La_puja_maxima_nunca_supera_el_punto_de_equilibrio()
    {
        var parameters = AnalysisParameters.Default;

        var structure = FinancialEngine.BuildCostStructure(
            11_000_000m, 800_000m, 150_000m, 150_000m, 0m, 45, parameters);

        var result = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);

        Assert.True(result.MaxBid < result.BreakevenBid);
        Assert.True(result.TheoreticalMaxBid <= result.BreakevenBid);
    }

    [Fact]
    public void Mas_dias_estimados_de_venta_bajan_la_puja_maxima()
    {
        var parameters = AnalysisParameters.Default;

        decimal MaxBidFor(int days)
        {
            var structure = FinancialEngine.BuildCostStructure(
                12_000_000m, 900_000m, 150_000m, 150_000m, 0m, days, parameters);
            return MaxBidCalculator.Calculate(structure, NoUncertainty, parameters).MaxBid;
        }

        var fast = MaxBidFor(20);
        var slow = MaxBidFor(120);

        Assert.True(fast > slow,
            $"Vender en 20 días debe permitir pagar más ({fast}) que vender en 120 ({slow}).");
    }

    [Fact]
    public void Mayor_incertidumbre_de_reparacion_baja_la_puja_maxima()
    {
        var parameters = AnalysisParameters.Default;
        var structure = FinancialEngine.BuildCostStructure(
            12_000_000m, 900_000m, 150_000m, 150_000m, 0m, 45, parameters);

        var certain = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);
        var uncertain = MaxBidCalculator.Calculate(structure, NoUncertainty with { RepairUncertainty = 0.9m }, parameters);

        Assert.True(uncertain.SafetyMarginPct > certain.SafetyMarginPct);
        Assert.True(uncertain.MaxBid < certain.MaxBid);
    }

    [Fact]
    public void El_margen_de_seguridad_queda_acotado_entre_el_minimo_y_el_maximo()
    {
        var parameters = AnalysisParameters.Default;
        var structure = FinancialEngine.BuildCostStructure(
            12_000_000m, 900_000m, 150_000m, 150_000m, 0m, 45, parameters);

        var everythingUncertain = new UncertaintyInputs
        {
            RepairUncertainty = 1m,
            MarketDispersion = 1m,
            ComparableCount = 0,
            DocumentRiskFactor = 1m
        };

        var high = MaxBidCalculator.Calculate(structure, everythingUncertain, parameters);
        var low = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);

        Assert.Equal(parameters.SafetyMarginMax, high.SafetyMarginPct);
        Assert.InRange(low.SafetyMarginPct, parameters.SafetyMarginMin, parameters.SafetyMarginMax);
    }

    [Fact]
    public void Un_vehiculo_sin_margen_queda_marcado_como_no_viable()
    {
        var parameters = AnalysisParameters.Default;

        // Valor de venta bajo y reparación enorme: no hay precio de compra que rescate la operación.
        var structure = FinancialEngine.BuildCostStructure(
            grossSaleValue: 3_000_000m,
            repairExpected: 4_000_000m,
            transport: 150_000m,
            detailing: 150_000m,
            otherFixedCosts: 0m,
            daysToSell: 45,
            parameters);

        var result = MaxBidCalculator.Calculate(structure, NoUncertainty, parameters);

        Assert.False(result.IsViable);
        Assert.Equal(0m, result.MaxBid);
    }

    /// <summary>
    /// La utilidad mínima no puede ser un monto fijo: en un auto barato ese piso es el criterio
    /// correcto, pero en uno caro exigir lo mismo sería regalar el capital. Manda el mayor de los dos.
    /// </summary>
    [Fact]
    public void La_utilidad_minima_escala_con_el_capital_comprometido()
    {
        var parameters = AnalysisParameters.Default;

        var cheap = FinancialEngine.BuildCostStructure(
            5_000_000m, 300_000m, 150_000m, 150_000m, 0m, 90, parameters);
        var expensive = FinancialEngine.BuildCostStructure(
            35_000_000m, 900_000m, 150_000m, 150_000m, 0m, 90, parameters);

        var cheapResult = MaxBidCalculator.Calculate(cheap, NoUncertainty, parameters);
        var expensiveResult = MaxBidCalculator.Calculate(expensive, NoUncertainty, parameters);

        Assert.Equal("min_profit_abs", cheapResult.RequiredProfitDriver);
        Assert.Equal("roi_annual", expensiveResult.RequiredProfitDriver);
        Assert.True(expensiveResult.RequiredProfit > parameters.MinProfitAbs);
    }
}
