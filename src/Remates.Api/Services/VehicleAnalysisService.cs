using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Domain.Analysis;
using Remates.Domain.Damage;
using Remates.Domain.Financial;
using Remates.Domain.Market;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Arma el análisis de un vehículo persistido y guarda la fotografía del resultado.
///
/// El cálculo lo hace <see cref="DealAnalyzer"/>; acá solo se juntan los datos y se persiste
/// lo que salió, con la versión de los motores y el conjunto de parámetros usados.
/// </summary>
public sealed class VehicleAnalysisService(
    RematesDbContext db,
    ParameterProvider parameters,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public async Task<(DealAnalysisResult Result, DealAnalysisSnapshot Snapshot)> AnalyzeAndSaveAsync(
        long vehicleId,
        AnalyzeVehicleRequest request,
        CancellationToken ct)
    {
        var vehicle = await db.Vehicles
            .Include(v => v.Comparables)
            .Include(v => v.Damages)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new KeyNotFoundException($"No existe el vehículo {vehicleId}.");

        var (parameterSet, analysisParameters) = await parameters.GetActiveAsync(ct);
        var now = timeProvider.GetUtcNow();

        var domainRequest = new DealAnalysisRequest
        {
            Year = vehicle.Year,
            MileageKm = vehicle.MileageKm,
            Comparables = vehicle.Comparables.Select(c => new MarketComparable
            {
                ListedPrice = c.ListedPrice,
                Year = c.Year,
                MileageKm = c.MileageKm,
                AgeDays = Math.Max(0, (int)(now - c.ObservedAt).TotalDays),
                Source = c.Source,
                Url = c.Url,
                IsOutlier = c.IsOutlier
            }).ToList(),
            // Solo entra al cálculo lo que una persona confirmó: la IA propone, no decide.
            Damages = vehicle.Damages.Where(d => d.IsConfirmed).Select(d => new DamageItem
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
            ManualValuation = request.ManualValuation is null
                ? null
                : new ManualValuation
                {
                    Conservative = request.ManualValuation.Conservative,
                    Expected = request.ManualValuation.Expected,
                    Optimistic = request.ManualValuation.Optimistic
                },
            InspectionLevel = vehicle.InspectionLevel,
            DocumentRisk = vehicle.DocumentRisk,
            Transport = request.Transport,
            Detailing = request.Detailing,
            OtherFixedCosts = request.OtherFixedCosts,
            EstimatedDaysToSell = request.EstimatedDaysToSell,
            CurrentAuctionPrice = request.CurrentAuctionPrice,
            TotalCapital = request.TotalCapital,
            Parameters = analysisParameters
        };

        var result = DealAnalyzer.Analyze(domainRequest);

        var snapshot = new DealAnalysisSnapshot
        {
            VehicleId = vehicle.Id,
            AuctionLotId = request.AuctionLotId,
            ParameterSetId = parameterSet.Id,
            FinancialEngineVersion = result.FinancialEngineVersion,
            ScoringEngineVersion = result.ScoringEngineVersion,
            ComputedAt = now,

            SaleValueOptimistic = result.Valuation.Optimistic,
            SaleValueExpected = result.Valuation.Expected,
            SaleValueConservative = result.Valuation.Conservative,
            ComparableCount = result.Valuation.ComparableCount,

            NetSaleValue = result.CostStructure.NetSaleValue,
            TotalFixedCosts = result.CostStructure.FixedCosts,
            ProportionalRate = result.CostStructure.ProportionalRate,
            CapitalFactor = result.CostStructure.CapitalFactor,
            RepairExpected = result.Repair.TotalExpected,

            BreakevenBid = result.BreakevenBid,
            TheoreticalMaxBid = result.MaxBid.TheoreticalMaxBid,
            SafetyMarginPct = result.MaxBid.SafetyMarginPct,
            MaxBid = result.MaxBid.MaxBid,
            RequiredProfit = result.MaxBid.RequiredProfit,
            CurrentAuctionPrice = result.CurrentAuctionPrice,
            Headroom = result.Headroom,

            ExpectedProfit = result.MetricsAtCurrentPrice.Profit,
            RoiSimple = result.MetricsAtCurrentPrice.RoiSimple,
            RoiAnnualized = result.MetricsAtCurrentPrice.RoiAnnualized,
            MarginPct = result.MetricsAtCurrentPrice.MarginPct,
            EstimatedDaysToSell = result.CostStructure.DaysToSell,

            Score = result.Score.Score,
            TrafficLight = result.Score.TrafficLight,

            GatesJson = JsonSerializer.Serialize(result.Score.Gates, Json),
            ScoreBreakdownJson = JsonSerializer.Serialize(result.Score.Components, Json),
            CostBreakdownJson = JsonSerializer.Serialize(new
            {
                fixedCostLines = result.CostStructure.FixedCostLines,
                saleDeductionLines = result.CostStructure.SaleDeductionLines,
                safetyMargin = result.MaxBid.SafetyMarginBreakdown
            }, Json),
            ScenariosJson = JsonSerializer.Serialize(result.Scenarios, Json),
            InputsJson = JsonSerializer.Serialize(request, Json)
        };

        db.DealAnalyses.Add(snapshot);

        // Registrar el análisis mueve el vehículo a «analizando» si aún estaba solo detectado.
        if (vehicle.Status == VehicleStatus.Detected)
        {
            vehicle.Status = VehicleStatus.Analyzing;
            db.VehicleStatusHistory.Add(new VehicleStatusHistory
            {
                VehicleId = vehicle.Id,
                FromStatus = VehicleStatus.Detected,
                ToStatus = VehicleStatus.Analyzing,
                ChangedAt = now,
                Note = "Primer análisis registrado."
            });
        }

        await db.SaveChangesAsync(ct);

        return (result, snapshot);
    }
}
