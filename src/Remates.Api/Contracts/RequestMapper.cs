using Remates.Domain.Analysis;
using Remates.Domain.Damage;
using Remates.Domain.Market;
using Remates.Domain.Parameters;

namespace Remates.Api.Contracts;

/// <summary>Traduce el contrato HTTP al modelo de dominio. Sin lógica de negocio.</summary>
public static class RequestMapper
{
    public static DealAnalysisRequest ToDomain(this SimulateAnalysisRequest dto) => new()
    {
        Year = dto.Year,
        MileageKm = dto.MileageKm,
        Comparables = dto.Comparables.Select(c => new MarketComparable
        {
            ListedPrice = c.ListedPrice,
            Year = c.Year,
            MileageKm = c.MileageKm,
            AgeDays = c.AgeDays,
            Source = c.Source,
            Url = c.Url,
            IsOutlier = c.IsOutlier
        }).ToList(),
        ManualValuation = dto.ManualValuation is null
            ? null
            : new ManualValuation
            {
                Conservative = dto.ManualValuation.Conservative,
                Expected = dto.ManualValuation.Expected,
                Optimistic = dto.ManualValuation.Optimistic
            },
        Damages = dto.Damages.Select(d => new DamageItem
        {
            Category = d.Category,
            Severity = d.Severity,
            CostMin = d.CostMin,
            CostExpected = d.CostExpected,
            CostMax = d.CostMax,
            Description = d.Description,
            Source = d.Source,
            Confidence = d.Confidence
        }).ToList(),
        InspectionLevel = dto.InspectionLevel,
        DocumentRisk = dto.DocumentRisk,
        Transport = dto.Transport,
        Detailing = dto.Detailing,
        OtherFixedCosts = dto.OtherFixedCosts,
        EstimatedDaysToSell = dto.EstimatedDaysToSell,
        CurrentAuctionPrice = dto.CurrentAuctionPrice,
        TotalCapital = dto.TotalCapital,
        Parameters = dto.Parameters ?? AnalysisParameters.Default
    };
}
