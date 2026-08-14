using Remates.Domain.Damage;

namespace Remates.Domain.Tests;

public class RepairEstimatorTests
{
    private static DamageItem Item(
        DamageCategory category,
        DamageSeverity severity,
        decimal min,
        decimal expected,
        decimal max) => new()
        {
            Category = category,
            Severity = severity,
            CostMin = min,
            CostExpected = expected,
            CostMax = max
        };

    [Fact]
    public void Agrupa_los_costos_por_categoria_y_los_ordena_por_impacto()
    {
        var items = new[]
        {
            Item(DamageCategory.Paint, DamageSeverity.Minor, 200_000m, 250_000m, 300_000m),
            Item(DamageCategory.Bodywork, DamageSeverity.Moderate, 350_000m, 450_000m, 500_000m),
            Item(DamageCategory.Bodywork, DamageSeverity.Minor, 100_000m, 120_000m, 150_000m)
        };

        var estimate = RepairEstimator.Calculate(items, MechanicalInspectionLevel.VisualOnly);

        Assert.Equal(650_000m, estimate.TotalMin);
        Assert.Equal(820_000m, estimate.TotalExpected);
        Assert.Equal(950_000m, estimate.TotalMax);

        Assert.Equal(DamageCategory.Bodywork, estimate.ByCategory[0].Category);
        Assert.Equal(570_000m, estimate.ByCategory[0].Expected);
        Assert.Equal(2, estimate.ByCategory[0].ItemCount);
    }

    [Fact]
    public void La_incertidumbre_refleja_el_ancho_del_rango()
    {
        var tight = RepairEstimator.Calculate(
            [Item(DamageCategory.Paint, DamageSeverity.Minor, 480_000m, 500_000m, 520_000m)],
            MechanicalInspectionLevel.TestDrive);

        var wide = RepairEstimator.Calculate(
            [Item(DamageCategory.Paint, DamageSeverity.Minor, 200_000m, 500_000m, 1_200_000m)],
            MechanicalInspectionLevel.TestDrive);

        Assert.True(wide.UncertaintyRatio > tight.UncertaintyRatio);
        Assert.Equal(0.04m, tight.UncertaintyRatio);
    }

    [Fact]
    public void Sin_costo_esperado_la_incertidumbre_es_cero_y_no_divide_por_cero()
    {
        var estimate = RepairEstimator.Calculate([], MechanicalInspectionLevel.TestDrive);

        Assert.Equal(0m, estimate.UncertaintyRatio);
        Assert.Equal(0m, estimate.TotalExpected);
    }

    /// <summary>
    /// No haber podido encender el vehículo es riesgo real aunque no se registre ningún daño.
    /// Es la situación normal en un remate.
    /// </summary>
    [Theory]
    [InlineData(MechanicalInspectionLevel.None, 60)]
    [InlineData(MechanicalInspectionLevel.VisualOnly, 40)]
    [InlineData(MechanicalInspectionLevel.EngineRun, 25)]
    [InlineData(MechanicalInspectionLevel.TestDrive, 15)]
    [InlineData(MechanicalInspectionLevel.WorkshopReport, 5)]
    public void Sin_danos_registrados_el_riesgo_mecanico_lo_fija_el_nivel_de_inspeccion(
        MechanicalInspectionLevel level, int expected)
    {
        var estimate = RepairEstimator.Calculate([], level);

        Assert.Equal(expected, estimate.MechanicalRiskScore);
    }

    [Fact]
    public void Un_dano_mecanico_grave_supera_el_piso_del_nivel_de_inspeccion()
    {
        var estimate = RepairEstimator.Calculate(
            [Item(DamageCategory.Mechanical, DamageSeverity.Severe, 500_000m, 800_000m, 1_200_000m)],
            MechanicalInspectionLevel.TestDrive);

        Assert.Equal(70m, estimate.MechanicalRiskScore);
    }

    [Fact]
    public void Los_danos_no_mecanicos_no_elevan_el_riesgo_mecanico()
    {
        var estimate = RepairEstimator.Calculate(
            [Item(DamageCategory.Paint, DamageSeverity.Critical, 500_000m, 800_000m, 1_200_000m)],
            MechanicalInspectionLevel.WorkshopReport);

        Assert.Equal(5m, estimate.MechanicalRiskScore);
    }

    [Fact]
    public void Se_marcan_los_danos_estructurales_y_de_airbag()
    {
        var estimate = RepairEstimator.Calculate(
            [
                Item(DamageCategory.Structural, DamageSeverity.Severe, 1_000_000m, 1_500_000m, 2_500_000m),
                Item(DamageCategory.Airbags, DamageSeverity.Critical, 800_000m, 1_200_000m, 1_800_000m)
            ],
            MechanicalInspectionLevel.VisualOnly);

        Assert.True(estimate.HasStructuralDamage);
        Assert.True(estimate.HasAirbagDamage);
        Assert.Equal(100m, estimate.MechanicalRiskScore);
    }
}
