using System.ComponentModel.DataAnnotations;
using Remates.Domain.Damage;
using Remates.Infrastructure.Entities;

namespace Remates.Api.Contracts;

public sealed class VehicleUpsertRequest
{
    public long? MakeId { get; set; }
    public long? ModelId { get; set; }
    public long? TrimId { get; set; }

    [MaxLength(160)]
    public string? DisplayName { get; set; }

    [Range(1900, 2100)]
    public int Year { get; set; }

    [Range(0, 2_000_000)]
    public int MileageKm { get; set; }

    public Transmission? Transmission { get; set; }
    public FuelType? Fuel { get; set; }

    [MaxLength(40)] public string? BodyType { get; set; }
    [MaxLength(12)] public string? Plate { get; set; }
    [MaxLength(32)] public string? Vin { get; set; }
    [MaxLength(40)] public string? Color { get; set; }
    [MaxLength(80)] public string? Region { get; set; }
    [MaxLength(80)] public string? Comuna { get; set; }

    public string? ConditionNotes { get; set; }

    public MechanicalInspectionLevel InspectionLevel { get; set; } = MechanicalInspectionLevel.VisualOnly;
    public DocumentRiskLevel DocumentRisk { get; set; } = DocumentRiskLevel.None;

    [MaxLength(40)] public string? SourceType { get; set; }
    [MaxLength(600)] public string? Url { get; set; }
}

public sealed class ChangeStatusRequest
{
    public VehicleStatus Status { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public sealed record VehicleSummary(
    long Id,
    string Label,
    int Year,
    int MileageKm,
    VehicleStatus Status,
    string? Region,
    int ComparableCount,
    int DamageCount,
    decimal? LastMaxBid,
    decimal? LastScore,
    string? LastTrafficLight,
    DateTimeOffset? LastAnalyzedAt);

public sealed record VehicleDetail(
    long Id,
    long? MakeId,
    string? MakeName,
    long? ModelId,
    string? ModelName,
    long? TrimId,
    string? TrimName,
    string? DisplayName,
    string Label,
    int Year,
    int MileageKm,
    Transmission? Transmission,
    FuelType? Fuel,
    string? BodyType,
    string? Plate,
    string? Vin,
    string? Color,
    string? Region,
    string? Comuna,
    string? ConditionNotes,
    MechanicalInspectionLevel InspectionLevel,
    DocumentRiskLevel DocumentRisk,
    VehicleStatus Status,
    string? SourceType,
    string? Url,
    DateTimeOffset CreatedAt);

public sealed class ComparableUpsertRequest
{
    [Range(1, 999_999_999)] public decimal ListedPrice { get; set; }
    [Range(1900, 2100)] public int Year { get; set; }
    [Range(0, 2_000_000)] public int MileageKm { get; set; }

    [MaxLength(80)] public string? Source { get; set; }
    [MaxLength(600)] public string? Url { get; set; }
    [MaxLength(80)] public string? Region { get; set; }

    public DateTimeOffset? ObservedAt { get; set; }
    public bool IsOutlier { get; set; }
    [MaxLength(240)] public string? OutlierReason { get; set; }
}

public sealed class DamageUpsertRequest
{
    public DamageCategory Category { get; set; }
    public DamageSeverity Severity { get; set; }

    [Range(0, 999_999_999)] public decimal CostMin { get; set; }
    [Range(0, 999_999_999)] public decimal CostExpected { get; set; }
    [Range(0, 999_999_999)] public decimal CostMax { get; set; }

    [MaxLength(400)] public string? Description { get; set; }
    public DamageSource Source { get; set; } = DamageSource.Manual;
    [Range(0, 1)] public decimal? Confidence { get; set; }
}

/// <summary>Datos del remate que no viven en el vehículo pero hacen falta para analizarlo.</summary>
public sealed class AnalyzeVehicleRequest
{
    public long? AuctionLotId { get; set; }

    [Range(0, 999_999_999)] public decimal CurrentAuctionPrice { get; set; }
    [Range(0, 999_999_999)] public decimal? Transport { get; set; }
    [Range(0, 999_999_999)] public decimal? Detailing { get; set; }
    [Range(0, 999_999_999)] public decimal OtherFixedCosts { get; set; }
    [Range(1, 3650)] public int? EstimatedDaysToSell { get; set; }
    [Range(0, 999_999_999_999)] public decimal TotalCapital { get; set; }

    /// <summary>Valor conservador a mano, para cuando aún no hay comparables cargados.</summary>
    public ManualValuationDto? ManualValuation { get; set; }
}
