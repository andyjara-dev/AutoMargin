using Remates.Domain.Damage;

namespace Remates.Infrastructure.Entities;

/// <summary>
/// Estados del ciclo de vida. `Lost` y `Discarded` existen a propósito: sin registrar las pujas
/// que se pierden no hay forma de saber si la puja máxima está mal calibrada.
/// </summary>
public enum VehicleStatus
{
    Detected = 1,
    Analyzing = 2,
    Bidding = 3,
    Won = 4,
    Lost = 5,
    Purchased = 6,
    InTransport = 7,
    InRepair = 8,
    Ready = 9,
    Listed = 10,
    Reserved = 11,
    Sold = 12,
    Discarded = 13
}

public enum Transmission { Manual = 1, Automatic = 2, Cvt = 3, Other = 99 }

public enum FuelType { Gasoline = 1, Diesel = 2, Hybrid = 3, Electric = 4, Other = 99 }

public class Vehicle : AuditableEntity
{
    public long? MakeId { get; set; }
    public Make? Make { get; set; }

    public long? ModelId { get; set; }
    public VehicleModel? Model { get; set; }

    public long? TrimId { get; set; }
    public Trim? Trim { get; set; }

    /// <summary>Texto libre para cuando el catálogo aún no tiene la marca o el modelo.</summary>
    public string? DisplayName { get; set; }

    public int Year { get; set; }
    public int MileageKm { get; set; }

    public Transmission? Transmission { get; set; }
    public FuelType? Fuel { get; set; }
    public string? BodyType { get; set; }

    public string? Plate { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }

    public string? Region { get; set; }
    public string? Comuna { get; set; }

    /// <summary>Equipamiento como jsonb: es información heterogénea que no vale la pena normalizar.</summary>
    public string? EquipmentJson { get; set; }

    public string? ConditionNotes { get; set; }

    public VehicleStatus Status { get; set; } = VehicleStatus.Detected;

    public MechanicalInspectionLevel InspectionLevel { get; set; } = MechanicalInspectionLevel.VisualOnly;
    public DocumentRiskLevel DocumentRisk { get; set; } = DocumentRiskLevel.None;

    public string? SourceType { get; set; }
    public string? ExternalRef { get; set; }
    public string? Url { get; set; }

    public DateTimeOffset? DetectedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<VehicleStatusHistory> StatusHistory { get; set; } = [];
    public ICollection<MarketComparableEntity> Comparables { get; set; } = [];
    public ICollection<DamageItemEntity> Damages { get; set; } = [];
    public ICollection<DealAnalysisSnapshot> Analyses { get; set; } = [];
    public ICollection<AuctionLot> Lots { get; set; } = [];
}

public class VehicleStatusHistory : AuditableEntity
{
    public long VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public VehicleStatus? FromStatus { get; set; }
    public VehicleStatus ToStatus { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public string? Note { get; set; }
}
