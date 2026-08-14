using Remates.Domain.Analysis;
using Remates.Domain.Damage;
using Remates.Domain.Parameters;
using Remates.Domain.Scoring;

namespace Remates.Domain.Tests;

public class DealAnalyzerTests
{
    [Fact]
    public void Una_oportunidad_sana_muy_por_debajo_de_la_puja_maxima_sale_en_verde()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.70m);

        Assert.Equal(TrafficLight.Green, result.Score.TrafficLight);
        Assert.Empty(result.Score.Gates);
        Assert.True(result.Score.Score >= 70m, $"Score obtenido: {result.Score.Score}");
        Assert.True(result.Headroom > 0m);
    }

    [Fact]
    public void Rozando_la_puja_maxima_la_recomendacion_baja_a_amarillo()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.97m);

        Assert.Equal(TrafficLight.Yellow, result.Score.TrafficLight);
        Assert.Empty(result.Score.Gates);
    }

    [Fact]
    public void Sobre_la_puja_maxima_la_recomendacion_es_roja_con_el_motivo_explicito()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 1.20m);

        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
        Assert.Contains(result.Score.Gates, g => g.Code == GateCode.PriceAboveMaxBid);
        Assert.True(result.Headroom < 0m);
    }

    /// <summary>
    /// Un vehículo barato no es, por sí solo, una oportunidad. Esta es la regla fundamental del sistema.
    /// </summary>
    [Fact]
    public void Estar_barato_no_alcanza_si_la_reparacion_se_come_el_margen()
    {
        var request = DealFixtures.HealthyDeal(2_000_000m) with
        {
            Damages =
            [
                new DamageItem
                {
                    Category = DamageCategory.Structural,
                    Severity = DamageSeverity.Critical,
                    CostMin = 6_000_000m, CostExpected = 8_000_000m, CostMax = 11_000_000m
                }
            ],
            InspectionLevel = MechanicalInspectionLevel.None
        };

        var result = DealAnalyzer.Analyze(request);

        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
        Assert.True(result.MetricsAtCurrentPrice.Profit < 0m);
    }

    [Fact]
    public void El_riesgo_documental_alto_bloquea_aunque_los_numeros_sean_excelentes()
    {
        var baseRequest = DealFixtures.HealthyDeal(0m) with { DocumentRisk = DocumentRiskLevel.High };
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(baseRequest, 0.50m);

        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
        Assert.Contains(result.Score.Gates, g => g.Code == GateCode.CriticalDocumentRisk);
    }

    [Fact]
    public void Sin_comparables_suficientes_se_bloquea_el_analisis()
    {
        var request = DealFixtures.HealthyDeal(5_000_000m) with
        {
            Comparables = [DealFixtures.Comparable(12_400_000m), DealFixtures.Comparable(12_500_000m)]
        };

        var result = DealAnalyzer.Analyze(request);

        Assert.Contains(result.Score.Gates, g => g.Code == GateCode.InsufficientMarketData);
        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
    }

    [Fact]
    public void Un_valor_de_mercado_ingresado_a_mano_permite_analizar_pero_castiga_la_evidencia()
    {
        var withComparables = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.70m);

        var manualRequest = DealFixtures.HealthyDeal(0m) with
        {
            Comparables = [],
            ManualValuation = new ManualValuation { Conservative = withComparables.Valuation.Conservative }
        };

        var manual = DealFixtures.AnalyzeAtFractionOfMaxBid(manualRequest, 0.70m);

        Assert.DoesNotContain(manual.Score.Gates, g => g.Code == GateCode.InsufficientMarketData);

        var evidenceManual = manual.Score.Components.Single(c => c.Key == "evidenceQuality").Normalized;
        var evidenceComparables = withComparables.Score.Components.Single(c => c.Key == "evidenceQuality").Normalized;

        Assert.True(evidenceManual < evidenceComparables);
        // Menos evidencia también significa margen de seguridad mayor y por lo tanto puja máxima menor.
        Assert.True(manual.MaxBid.MaxBid < withComparables.MaxBid.MaxBid);
    }

    [Fact]
    public void La_concentracion_excesiva_de_capital_bloquea_la_operacion()
    {
        var baseRequest = DealFixtures.HealthyDeal(0m) with { TotalCapital = 8_000_000m };
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(baseRequest, 0.70m);

        Assert.Contains(result.Score.Gates, g => g.Code == GateCode.CapitalConcentration);
        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
    }

    [Fact]
    public void El_escenario_pesimista_con_perdida_grande_bloquea_la_operacion()
    {
        var baseRequest = DealFixtures.HealthyDeal(0m) with
        {
            Damages =
            [
                new DamageItem
                {
                    Category = DamageCategory.Mechanical,
                    Severity = DamageSeverity.Severe,
                    // Rango enorme: el caso malo destruye el negocio aunque el esperado luzca bien.
                    CostMin = 200_000m, CostExpected = 900_000m, CostMax = 7_500_000m
                }
            ]
        };

        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(baseRequest, 0.95m);
        var pessimistic = result.Scenarios.Single(s => s.Kind == Domain.Financial.ScenarioKind.Pessimistic);

        Assert.True(pessimistic.Metrics.Profit < 0m);
        Assert.Contains(result.Score.Gates, g => g.Code == GateCode.PessimisticLossExceedsLimit);
    }

    [Fact]
    public void Los_tres_escenarios_quedan_ordenados_de_mejor_a_peor()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.70m);

        var optimistic = result.Scenarios.Single(s => s.Kind == Domain.Financial.ScenarioKind.Optimistic);
        var expected = result.Scenarios.Single(s => s.Kind == Domain.Financial.ScenarioKind.Expected);
        var pessimistic = result.Scenarios.Single(s => s.Kind == Domain.Financial.ScenarioKind.Pessimistic);

        Assert.True(optimistic.Metrics.Profit > expected.Metrics.Profit);
        Assert.True(expected.Metrics.Profit > pessimistic.Metrics.Profit);
    }

    [Fact]
    public void Comprar_exactamente_en_la_puja_maxima_deja_al_menos_la_utilidad_minima()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 1m);

        Assert.True(result.MetricsAtMaxBid.Profit >= result.MaxBid.RequiredProfit,
            $"Utilidad en la puja máxima: {result.MetricsAtMaxBid.Profit}, mínima exigida: {result.MaxBid.RequiredProfit}");
    }

    [Fact]
    public void La_terna_de_precios_mantiene_su_orden()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.70m);

        Assert.True(result.CurrentAuctionPrice < result.MaxBid.MaxBid);
        Assert.True(result.MaxBid.MaxBid < result.BreakevenBid);
    }

    [Fact]
    public void Cada_componente_del_score_trae_su_explicacion()
    {
        var result = DealFixtures.AnalyzeAtFractionOfMaxBid(DealFixtures.HealthyDeal(0m), 0.70m);

        Assert.Equal(7, result.Score.Components.Count);
        Assert.All(result.Score.Components, c => Assert.False(string.IsNullOrWhiteSpace(c.Explanation)));
        Assert.NotEmpty(result.Score.Strengths);
    }

    [Fact]
    public void El_analisis_es_determinista()
    {
        var request = DealFixtures.HealthyDeal(5_000_000m);

        var first = DealAnalyzer.Analyze(request);
        var second = DealAnalyzer.Analyze(request);

        Assert.Equal(first.MaxBid.MaxBid, second.MaxBid.MaxBid);
        Assert.Equal(first.Score.Score, second.Score.Score);
        Assert.Equal(first.MetricsAtCurrentPrice.Profit, second.MetricsAtCurrentPrice.Profit);
    }

    [Fact]
    public void Siempre_se_entrega_el_descargo_sobre_la_inspeccion_mecanica()
    {
        var result = DealAnalyzer.Analyze(DealFixtures.HealthyDeal(5_000_000m));

        Assert.Contains(result.Disclaimers, d => d.Contains("inspección mecánica"));
    }

    [Fact]
    public void Una_estimacion_de_dano_hecha_por_IA_agrega_su_propio_descargo()
    {
        var request = DealFixtures.HealthyDeal(5_000_000m) with
        {
            Damages =
            [
                new DamageItem
                {
                    Category = DamageCategory.Bodywork,
                    Severity = DamageSeverity.Moderate,
                    CostMin = 400_000m, CostExpected = 500_000m, CostMax = 700_000m,
                    Source = DamageSource.Ai,
                    Confidence = 0.7m
                }
            ]
        };

        var result = DealAnalyzer.Analyze(request);

        Assert.True(result.Repair.ContainsUnconfirmedAiEstimates);
        Assert.Contains(result.Disclaimers, d => d.Contains("análisis automático"));
    }

    [Fact]
    public void Un_vehiculo_sin_datos_no_revienta_el_motor()
    {
        var result = DealAnalyzer.Analyze(new DealAnalysisRequest
        {
            Year = 2015,
            MileageKm = 0,
            Parameters = AnalysisParameters.Default
        });

        Assert.Equal(0m, result.Valuation.Conservative);
        Assert.False(result.MaxBid.IsViable);
        Assert.Equal(TrafficLight.Red, result.Score.TrafficLight);
    }
}
