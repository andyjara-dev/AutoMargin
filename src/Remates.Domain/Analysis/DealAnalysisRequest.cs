using Remates.Domain.Damage;
using Remates.Domain.Market;
using Remates.Domain.Parameters;

namespace Remates.Domain.Analysis;

/// <summary>
/// Valor de mercado ingresado a mano, cuando todavía no hay comparables cargados.
/// Es evidencia más débil que los comparables y el score lo refleja.
/// </summary>
public sealed record ManualValuation
{
    public required decimal Conservative { get; init; }
    public decimal? Expected { get; init; }
    public decimal? Optimistic { get; init; }
}

public sealed record DealAnalysisRequest
{
    // ---------- Vehículo ----------
    public required int Year { get; init; }
    public required int MileageKm { get; init; }

    // ---------- Mercado ----------
    public IReadOnlyList<MarketComparable> Comparables { get; init; } = [];

    /// <summary>Si viene informado, reemplaza el cálculo por comparables.</summary>
    public ManualValuation? ManualValuation { get; init; }

    // ---------- Estado y daños ----------
    public IReadOnlyList<DamageItem> Damages { get; init; } = [];
    public MechanicalInspectionLevel InspectionLevel { get; init; } = MechanicalInspectionLevel.VisualOnly;
    public DocumentRiskLevel DocumentRisk { get; init; } = DocumentRiskLevel.None;

    // ---------- Costos (null = usar el valor por defecto de los parámetros) ----------
    public decimal? Transport { get; init; }
    public decimal? Detailing { get; init; }
    public decimal OtherFixedCosts { get; init; }

    // ---------- Tiempo ----------
    public int? EstimatedDaysToSell { get; init; }

    // ---------- Remate ----------
    /// <summary>Precio actual del lote, o el precio que se está evaluando pujar.</summary>
    public decimal CurrentAuctionPrice { get; init; }

    // ---------- Contexto del negocio ----------
    /// <summary>Capital total disponible. En 0 se omite el control de concentración.</summary>
    public decimal TotalCapital { get; init; }

    public AnalysisParameters Parameters { get; init; } = AnalysisParameters.Default;
}
