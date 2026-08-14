using Remates.Domain.Alerts;
using Remates.Domain.Parameters;

namespace Remates.Domain.Tests;

public class AlertEngineTests
{
    private static readonly AnalysisParameters Parameters = AnalysisParameters.Default;

    private static InventorySnapshot Vehicle(
        long id = 1,
        decimal cashInvested = 7_000_000m,
        int days = 20,
        int daysListed = 0,
        bool isSold = false,
        bool hasAnalysis = true,
        decimal expectedSaleValue = 11_000_000m,
        decimal repairBudgeted = 0m,
        decimal repairActual = 0m) => new()
        {
            VehicleId = id,
            Label = $"Vehículo {id}",
            CashInvested = cashInvested,
            DaysInInventory = days,
            DaysListed = daysListed,
            IsSold = isSold,
            HasAnalysis = hasAnalysis,
            ExpectedSaleValue = expectedSaleValue,
            RepairBudgeted = repairBudgeted,
            RepairActual = repairActual
        };

    private static AlertContext Context(
        IEnumerable<InventorySnapshot>? inventory = null,
        decimal totalCapital = 40_000_000m,
        decimal availableCapital = 30_000_000m) => new()
        {
            Inventory = (inventory ?? [Vehicle()]).ToList(),
            TotalCapital = totalCapital,
            AvailableCapital = availableCapital
        };

    [Fact]
    public void Un_inventario_sano_no_genera_alertas()
    {
        var alerts = AlertEngine.Evaluate(Context(), Parameters);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Avisa_cuando_un_vehiculo_lleva_demasiados_dias()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(days: 75)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.StaleInventory);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Contains("75 días", alert.Message);
    }

    [Fact]
    public void Al_doble_del_limite_el_inventario_estancado_pasa_a_critico()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(days: 130)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.StaleInventory);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Avisa_cuando_lleva_mucho_publicado_sin_venderse()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(days: 40, daysListed: 35)]), Parameters);

        Assert.Contains(alerts, a => a.Type == AlertType.PriceNeedsAdjustment);
    }

    /// <summary>
    /// Un vehículo estancado y además publicado hace mucho no debe generar dos alertas que
    /// dicen lo mismo: la lista pierde utilidad si se llena de repeticiones.
    /// </summary>
    [Fact]
    public void No_duplica_el_aviso_de_precio_si_ya_avisa_por_inventario_estancado()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(days: 90, daysListed: 60)]), Parameters);

        Assert.Contains(alerts, a => a.Type == AlertType.StaleInventory);
        Assert.DoesNotContain(alerts, a => a.Type == AlertType.PriceNeedsAdjustment);
    }

    [Fact]
    public void Avisa_cuando_el_margen_proyectado_queda_bajo_el_minimo()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(cashInvested: 10_200_000m, expectedSaleValue: 11_000_000m)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.LowMargin);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void Si_lo_invertido_supera_el_valor_de_venta_la_alerta_es_critica()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(cashInvested: 12_000_000m, expectedSaleValue: 11_000_000m)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.LowMargin);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Avisa_cuando_la_reparacion_supera_el_presupuesto()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(repairBudgeted: 920_000m, repairActual: 1_230_000m)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.RepairOverBudget);
        Assert.Equal(310_000m, alert.Magnitude);
    }

    [Fact]
    public void Una_desviacion_dentro_de_la_tolerancia_no_genera_alerta()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(repairBudgeted: 1_000_000m, repairActual: 1_080_000m)]), Parameters);

        Assert.DoesNotContain(alerts, a => a.Type == AlertType.RepairOverBudget);
    }

    /// <summary>
    /// El sobrecosto de reparación importa también en los vendidos: de ahí sale el aprendizaje
    /// sobre qué tipo de daño se subestima.
    /// </summary>
    [Fact]
    public void El_sobrecosto_de_reparacion_se_reporta_aunque_el_vehiculo_ya_se_haya_vendido()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(isSold: true, repairBudgeted: 900_000m, repairActual: 1_500_000m)]),
            Parameters);

        Assert.Contains(alerts, a => a.Type == AlertType.RepairOverBudget);
    }

    [Fact]
    public void Un_vehiculo_vendido_no_genera_alertas_de_inventario()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(isSold: true, days: 200)]), Parameters);

        Assert.DoesNotContain(alerts, a => a.Type == AlertType.StaleInventory);
    }

    [Fact]
    public void Avisa_cuando_un_vehiculo_concentra_demasiado_capital()
    {
        var alerts = AlertEngine.Evaluate(
            Context([Vehicle(cashInvested: 16_000_000m)], totalCapital: 40_000_000m), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.CapitalConcentration);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Avisa_cuando_se_compro_sin_analisis_previo()
    {
        var alerts = AlertEngine.Evaluate(Context([Vehicle(hasAnalysis: false)]), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.PurchasedWithoutAnalysis);
        Assert.Equal(AlertSeverity.Info, alert.Severity);
    }

    [Fact]
    public void Avisa_cuando_queda_poco_capital_disponible()
    {
        var alerts = AlertEngine.Evaluate(
            Context(totalCapital: 40_000_000m, availableCapital: 3_000_000m), Parameters);

        Assert.Contains(alerts, a => a.Type == AlertType.LowAvailableCapital);
    }

    [Fact]
    public void Un_capital_disponible_negativo_es_critico()
    {
        var alerts = AlertEngine.Evaluate(
            Context(totalCapital: 40_000_000m, availableCapital: -2_000_000m), Parameters);

        var alert = Assert.Single(alerts, a => a.Type == AlertType.LowAvailableCapital);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Las_alertas_salen_ordenadas_por_gravedad_y_luego_por_monto()
    {
        var alerts = AlertEngine.Evaluate(
            Context(
                [
                    Vehicle(id: 1, days: 75, cashInvested: 5_000_000m),
                    Vehicle(id: 2, cashInvested: 16_000_000m),
                    Vehicle(id: 3, days: 80, cashInvested: 9_000_000m)
                ],
                totalCapital: 40_000_000m),
            Parameters);

        Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);

        var warnings = alerts.Where(a => a.Severity == AlertSeverity.Warning).ToList();
        Assert.True(warnings[0].Magnitude >= warnings[^1].Magnitude);
    }

    [Fact]
    public void Toda_alerta_trae_una_accion_sugerida()
    {
        var alerts = AlertEngine.Evaluate(
            Context(
                [
                    Vehicle(id: 1, days: 130, hasAnalysis: false),
                    Vehicle(id: 2, cashInvested: 16_000_000m, repairBudgeted: 500_000m, repairActual: 900_000m)
                ],
                availableCapital: 1_000_000m),
            Parameters);

        Assert.NotEmpty(alerts);
        Assert.All(alerts, a => Assert.False(string.IsNullOrWhiteSpace(a.Suggestion)));
        Assert.All(alerts, a => Assert.False(string.IsNullOrWhiteSpace(a.Message)));
    }

    [Fact]
    public void Sin_inventario_no_revienta()
    {
        var alerts = AlertEngine.Evaluate(
            new AlertContext { Inventory = [], TotalCapital = 0m, AvailableCapital = 0m },
            Parameters);

        Assert.Empty(alerts);
    }
}
