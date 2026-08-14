using Remates.Domain.Analysis;
using Remates.Domain.Damage;
using Remates.Domain.Market;

namespace Remates.Domain.Tests;

internal static class DealFixtures
{
    /// <summary>
    /// Oportunidad sana: buen mercado, daño acotado, prueba de manejo hecha y papeles en orden.
    /// El precio se define aparte en cada test.
    /// </summary>
    public static DealAnalysisRequest HealthyDeal(decimal currentPrice) => new()
    {
        Year = 2018,
        MileageKm = 80_000,
        Comparables =
        [
            Comparable(12_300_000m),
            Comparable(12_400_000m),
            Comparable(12_400_000m),
            Comparable(12_500_000m),
            Comparable(12_400_000m)
        ],
        Damages =
        [
            new DamageItem
            {
                Category = DamageCategory.Bodywork,
                Severity = DamageSeverity.Minor,
                CostMin = 500_000m, CostExpected = 550_000m, CostMax = 600_000m
            },
            new DamageItem
            {
                Category = DamageCategory.Tires,
                Severity = DamageSeverity.Moderate,
                CostMin = 250_000m, CostExpected = 300_000m, CostMax = 350_000m
            }
        ],
        InspectionLevel = MechanicalInspectionLevel.TestDrive,
        DocumentRisk = DocumentRiskLevel.None,
        Transport = 150_000m,
        Detailing = 150_000m,
        EstimatedDaysToSell = 30,
        CurrentAuctionPrice = currentPrice,
        TotalCapital = 50_000_000m
    };

    public static MarketComparable Comparable(decimal price) => new()
    {
        ListedPrice = price,
        Year = 2018,
        MileageKm = 80_000,
        AgeDays = 5
    };

    /// <summary>Ejecuta el análisis a precio cero solo para conocer la puja máxima, y luego reevalúa al precio pedido.</summary>
    public static DealAnalysisResult AnalyzeAtFractionOfMaxBid(DealAnalysisRequest baseRequest, decimal fraction)
    {
        var probe = DealAnalyzer.Analyze(baseRequest with { CurrentAuctionPrice = 0m });
        var price = decimal.Round(probe.MaxBid.MaxBid * fraction, 0);

        return DealAnalyzer.Analyze(baseRequest with { CurrentAuctionPrice = price });
    }
}
