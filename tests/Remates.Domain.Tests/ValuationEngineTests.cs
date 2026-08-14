using Remates.Domain.Common;
using Remates.Domain.Market;
using Remates.Domain.Parameters;

namespace Remates.Domain.Tests;

public class ValuationEngineTests
{
    private static MarketComparable Comp(decimal price, int year = 2018, int km = 80_000, int ageDays = 5, bool outlier = false)
        => new() { ListedPrice = price, Year = year, MileageKm = km, AgeDays = ageDays, IsOutlier = outlier };

    [Fact]
    public void Ordena_los_percentiles_y_aplica_la_brecha_de_negociacion()
    {
        var parameters = AnalysisParameters.Default with
        {
            NegotiationDiscountPct = 0.10m,
            MileageAdjustPctPer1000Km = 0m,
            YearAdjustPct = 0m
        };

        var comparables = new[]
        {
            Comp(9_000_000m), Comp(10_000_000m), Comp(11_000_000m), Comp(12_000_000m), Comp(13_000_000m)
        };

        var result = ValuationEngine.Calculate(comparables, 2018, 80_000, parameters);

        Assert.Equal(11_000_000m, result.Expected);   // mediana
        Assert.Equal(12_000_000m, result.Optimistic); // P75
        Assert.Equal(10_000_000m, result.ConservativeBeforeDiscount);
        Assert.Equal(9_000_000m, result.Conservative); // P25 con 10% de brecha
        Assert.True(result.HasEnoughEvidence);
    }

    [Fact]
    public void Un_comparable_con_mas_kilometros_se_ajusta_al_alza()
    {
        var parameters = AnalysisParameters.Default with
        {
            MileageAdjustPctPer1000Km = 0.004m,
            YearAdjustPct = 0m,
            NegotiationDiscountPct = 0m
        };

        // El comparable tiene 20.000 km más que el objetivo → vale menos → su precio sube 8% al normalizar.
        var result = ValuationEngine.Calculate([Comp(10_000_000m, km: 100_000)], 2018, 80_000, parameters);

        Assert.Equal(10_800_000m, result.Expected);
        Assert.Equal(0.08m, result.Adjusted[0].TotalAdjustment);
    }

    [Fact]
    public void Un_comparable_mas_antiguo_se_ajusta_al_alza_frente_a_un_objetivo_mas_nuevo()
    {
        var parameters = AnalysisParameters.Default with
        {
            MileageAdjustPctPer1000Km = 0m,
            YearAdjustPct = 0.05m,
            NegotiationDiscountPct = 0m
        };

        var result = ValuationEngine.Calculate([Comp(10_000_000m, year: 2016)], 2018, 80_000, parameters);

        Assert.Equal(11_000_000m, result.Expected); // dos años × 5%
    }

    [Fact]
    public void El_ajuste_de_un_comparable_lejano_queda_topado()
    {
        var parameters = AnalysisParameters.Default with
        {
            MaxComparableAdjustmentPct = 0.35m,
            YearAdjustPct = 0.05m,
            MileageAdjustPctPer1000Km = 0m,
            NegotiationDiscountPct = 0m
        };

        // 15 años de diferencia darían 75%: se topa en 35%.
        var result = ValuationEngine.Calculate([Comp(10_000_000m, year: 2003)], 2018, 80_000, parameters);

        Assert.Equal(0.35m, result.Adjusted[0].TotalAdjustment);
        Assert.True(result.Adjusted[0].AdjustmentWasCapped);
    }

    [Fact]
    public void Los_comparables_marcados_como_atipicos_quedan_fuera()
    {
        var parameters = AnalysisParameters.Default with
        {
            MileageAdjustPctPer1000Km = 0m, YearAdjustPct = 0m, NegotiationDiscountPct = 0m
        };

        var comparables = new[]
        {
            Comp(10_000_000m), Comp(10_500_000m), Comp(11_000_000m), Comp(40_000_000m, outlier: true)
        };

        var result = ValuationEngine.Calculate(comparables, 2018, 80_000, parameters);

        Assert.Equal(3, result.ComparableCount);
        Assert.Equal(1, result.ExcludedCount);
        Assert.Equal(10_500_000m, result.Expected);
    }

    [Fact]
    public void Con_menos_comparables_que_el_minimo_la_evidencia_es_insuficiente()
    {
        var parameters = AnalysisParameters.Default with { MinComparables = 3 };

        var result = ValuationEngine.Calculate([Comp(10_000_000m), Comp(11_000_000m)], 2018, 80_000, parameters);

        Assert.False(result.HasEnoughEvidence);
        Assert.Equal(2, result.ComparableCount);
    }

    [Fact]
    public void Sin_comparables_devuelve_una_valuacion_vacia_sin_reventar()
    {
        var result = ValuationEngine.Calculate([], 2018, 80_000, AnalysisParameters.Default);

        Assert.Equal(0m, result.Expected);
        Assert.Equal(0m, result.DispersionPct);
        Assert.False(result.HasEnoughEvidence);
        Assert.Empty(result.Adjusted);
    }

    [Theory]
    [InlineData(0.0, 10)]
    [InlineData(0.5, 30)]
    [InlineData(1.0, 50)]
    [InlineData(0.25, 20)]
    public void El_percentil_interpola_linealmente(double percentile, double expected)
    {
        var values = new[] { 10m, 20m, 30m, 40m, 50m };

        Assert.Equal((decimal)expected, MoneyMath.Percentile(values, (decimal)percentile));
    }
}
