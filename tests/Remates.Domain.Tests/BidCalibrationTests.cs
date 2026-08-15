using Remates.Domain.Learning;

namespace Remates.Domain.Tests;

public class BidCalibrationTests
{
    private static BidOutcome Won(decimal maxBid, decimal? winningPrice = null) =>
        new() { MaxBidAuthorized = maxBid, WinningPrice = winningPrice ?? maxBid, Won = true };

    private static BidOutcome Lost(decimal maxBid, decimal? winningPrice) =>
        new() { MaxBidAuthorized = maxBid, WinningPrice = winningPrice, Won = false };

    private static List<BidOutcome> Repeat(BidOutcome outcome, int times) =>
        Enumerable.Repeat(outcome, times).ToList();

    [Fact]
    public void Sin_remates_cerrados_no_dice_nada()
    {
        var report = CalibrationCalculator.Analyze([]);

        Assert.Equal(CalibrationVerdict.Insufficient, report.Verdict);
        Assert.False(report.IsConclusive);
        Assert.Equal(0, report.Total);
    }

    /// <summary>
    /// Con pocos remates cualquier proporción parece una tendencia. Actuar sobre ruido y bajar
    /// la utilidad mínima porque se perdieron dos seguidos es exactamente cómo se sobrepaga.
    /// </summary>
    [Fact]
    public void Con_muestra_corta_se_abstiene_aunque_el_resultado_sea_extremo()
    {
        var report = CalibrationCalculator.Analyze(Repeat(Won(5_000_000m), 3));

        Assert.Equal(1m, report.WinRate);
        Assert.Equal(CalibrationVerdict.Insufficient, report.Verdict);
        Assert.False(report.IsConclusive);
    }

    [Fact]
    public void Ganar_casi_todo_indica_que_se_esta_pagando_de_mas()
    {
        var outcomes = Repeat(Won(5_000_000m), 9)
            .Concat(Repeat(Lost(5_000_000m, 6_000_000m), 1))
            .ToList();

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(CalibrationVerdict.TooAggressive, report.Verdict);
        Assert.True(report.IsConclusive);
        Assert.Contains("por encima de lo que el mercado pide", report.Explanation);
    }

    [Fact]
    public void No_ganar_casi_nada_indica_que_la_puja_va_corta()
    {
        var outcomes = Repeat(Lost(5_000_000m, 5_600_000m), 10).ToList();

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(CalibrationVerdict.TooConservative, report.Verdict);
        Assert.Equal(0m, report.WinRate);
        Assert.Contains("va corta", report.Explanation);
    }

    [Fact]
    public void Ganar_algunos_y_perder_otros_es_la_senal_de_estar_bien_puesto()
    {
        var outcomes = Repeat(Won(5_000_000m), 4)
            .Concat(Repeat(Lost(5_000_000m, 5_800_000m), 6))
            .ToList();

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(CalibrationVerdict.Balanced, report.Verdict);
        Assert.Equal(0.4m, report.WinRate);
    }

    [Fact]
    public void Mide_cuanto_falto_para_ganar_los_perdidos()
    {
        var outcomes = new List<BidOutcome>
        {
            Lost(5_000_000m, 5_500_000m),   // faltaron 500.000, un 10%
            Lost(5_000_000m, 5_900_000m)    // faltaron 900.000, un 18%
        };

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(700_000m, report.AverageGapWhenLost);
        Assert.Equal(0.14m, report.AverageGapPctWhenLost);
    }

    /// <summary>
    /// Promediar las proporciones y no dividir un promedio por otro. Si no, el auto caro domina
    /// la medición y un remate perdido por poco en un auto barato se vuelve invisible.
    /// </summary>
    [Fact]
    public void Un_auto_caro_no_pesa_mas_que_uno_barato_al_medir_la_cercania()
    {
        var outcomes = new List<BidOutcome>
        {
            Lost(2_000_000m, 2_200_000m),    // 10%
            Lost(20_000_000m, 26_000_000m)   // 30%
        };

        var report = CalibrationCalculator.Analyze(outcomes);

        // El promedio simple de las proporciones es 20%. Dividir la suma de brechas por la suma
        // de pujas daría 28,2%, dominado por el auto caro.
        Assert.Equal(0.2m, report.AverageGapPctWhenLost);
    }

    /// <summary>
    /// Perder un lote que se adjudicó por debajo de nuestro propio techo no es un problema de
    /// cálculo: es no haber ofrecido lo que ya estaba autorizado. Confundirlos llevaría a subir
    /// la puja máxima cuando el problema estaba en la sala.
    /// </summary>
    [Fact]
    public void Distingue_perder_por_no_ofrecer_de_perder_por_ir_corto()
    {
        var outcomes = new List<BidOutcome>
        {
            Lost(5_000_000m, 4_600_000m),   // se adjudicó bajo nuestro techo
            Lost(5_000_000m, 5_900_000m)    // aquí sí nos superaron
        };

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(1, report.LostBelowOwnLimit);
        Assert.Contains("faltó ofrecer lo que ya tenías autorizado", report.Explanation);
    }

    [Fact]
    public void Los_perdidos_sin_precio_anotado_se_cuentan_aparte()
    {
        var outcomes = new List<BidOutcome>
        {
            Lost(5_000_000m, null),
            Lost(5_000_000m, 5_400_000m)
        };

        var report = CalibrationCalculator.Analyze(outcomes);

        Assert.Equal(1, report.LostWithoutPrice);
        // El que no tiene precio no ensucia el promedio.
        Assert.Equal(400_000m, report.AverageGapWhenLost);
    }

    [Fact]
    public void Sin_ningun_precio_de_adjudicacion_no_hay_brecha_que_informar()
    {
        var report = CalibrationCalculator.Analyze([Lost(5_000_000m, null)]);

        Assert.Null(report.AverageGapWhenLost);
        Assert.Null(report.AverageGapPctWhenLost);
    }
}
