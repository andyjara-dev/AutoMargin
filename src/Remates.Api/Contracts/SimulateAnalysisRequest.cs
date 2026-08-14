using System.ComponentModel.DataAnnotations;
using Remates.Domain.Damage;
using Remates.Domain.Parameters;

namespace Remates.Api.Contracts;

public sealed class ComparableDto
{
    [Range(1, 999_999_999)]
    public decimal ListedPrice { get; set; }

    [Range(1900, 2100)]
    public int Year { get; set; }

    [Range(0, 2_000_000)]
    public int MileageKm { get; set; }

    [Range(0, 3650)]
    public int AgeDays { get; set; }

    public string? Source { get; set; }
    public string? Url { get; set; }
    public bool IsOutlier { get; set; }
}

public sealed class DamageDto
{
    public DamageCategory Category { get; set; } = DamageCategory.Other;
    public DamageSeverity Severity { get; set; } = DamageSeverity.Minor;

    [Range(0, 999_999_999)]
    public decimal CostMin { get; set; }

    [Range(0, 999_999_999)]
    public decimal CostExpected { get; set; }

    [Range(0, 999_999_999)]
    public decimal CostMax { get; set; }

    public string? Description { get; set; }
    public DamageSource Source { get; set; } = DamageSource.Manual;

    [Range(0, 1)]
    public decimal? Confidence { get; set; }
}

public sealed class ManualValuationDto
{
    [Range(0, 999_999_999)]
    public decimal Conservative { get; set; }

    public decimal? Expected { get; set; }
    public decimal? Optimistic { get; set; }
}

/// <summary>
/// Entrada del simulador. Es stateless: no persiste nada y sirve para el recálculo en vivo
/// de la pantalla de análisis.
/// </summary>
public sealed class SimulateAnalysisRequest
{
    [Range(1900, 2100)]
    public int Year { get; set; } = DateTime.UtcNow.Year;

    [Range(0, 2_000_000)]
    public int MileageKm { get; set; }

    public List<ComparableDto> Comparables { get; set; } = [];

    /// <summary>Valor de mercado ingresado a mano, para cuando aún no hay comparables cargados.</summary>
    public ManualValuationDto? ManualValuation { get; set; }

    public List<DamageDto> Damages { get; set; } = [];

    public MechanicalInspectionLevel InspectionLevel { get; set; } = MechanicalInspectionLevel.VisualOnly;
    public DocumentRiskLevel DocumentRisk { get; set; } = DocumentRiskLevel.None;

    [Range(0, 999_999_999)]
    public decimal? Transport { get; set; }

    [Range(0, 999_999_999)]
    public decimal? Detailing { get; set; }

    [Range(0, 999_999_999)]
    public decimal OtherFixedCosts { get; set; }

    [Range(1, 3650)]
    public int? EstimatedDaysToSell { get; set; }

    [Range(0, 999_999_999)]
    public decimal CurrentAuctionPrice { get; set; }

    [Range(0, 999_999_999_999)]
    public decimal TotalCapital { get; set; }

    /// <summary>
    /// Sobrescritura opcional de parámetros. Las claves ausentes toman el valor por defecto,
    /// de modo que se puede enviar solo lo que se quiere cambiar.
    /// </summary>
    public AnalysisParameters? Parameters { get; set; }
}
