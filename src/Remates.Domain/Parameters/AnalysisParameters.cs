namespace Remates.Domain.Parameters;

/// <summary>
/// Conjunto de parámetros que gobiernan todos los cálculos. Es el equivalente en memoria de
/// <c>parameter_set</c> + <c>parameter_value</c> en la base de datos.
///
/// Regla de reproducibilidad: cada análisis persistido guarda el id del conjunto de parámetros usado.
/// Cambiar un parámetro NO debe alterar análisis históricos.
/// </summary>
public sealed record AnalysisParameters
{
    // ---------- Costos proporcionales al precio de martillo ----------

    /// <summary>Comisión del martillero sobre el precio de adjudicación (0,10 = 10%).</summary>
    public decimal CommissionPct { get; init; } = 0.10m;

    /// <summary>Si la comisión del martillero está afecta a IVA.</summary>
    public bool CommissionHasVat { get; init; } = true;

    public decimal VatPct { get; init; } = 0.19m;

    /// <summary>Gastos administrativos del remate expresados como % del martillo.</summary>
    public decimal AdminFeePct { get; init; } = 0m;

    /// <summary>Impuesto de transferencia aplicado sobre el precio de compra.</summary>
    public decimal TransferTaxPct { get; init; } = 0.015m;

    // ---------- Costos fijos posteriores a la compra ----------

    /// <summary>Costos fijos de trámite de transferencia (notaría, gestoría, formularios).</summary>
    public decimal TransferFixed { get; init; } = 25_000m;

    public decimal TransportDefault { get; init; } = 150_000m;
    public decimal DetailingDefault { get; init; } = 150_000m;

    /// <summary>Gastos administrativos fijos del remate (no proporcionales).</summary>
    public decimal AdminFeeFixed { get; init; } = 0m;

    /// <summary>Colchón sobre los costos estimados controlables (reparación, transporte, detailing).</summary>
    public decimal ContingencyPct { get; init; } = 0.10m;

    // ---------- Costos proporcionales al precio de venta ----------

    /// <summary>Publicación, fotos, comisión de portales.</summary>
    public decimal MarketingPct { get; init; } = 0.005m;

    /// <summary>Provisión por garantía legal y postventa (Ley 19.496 si vendes de forma habitual).</summary>
    public decimal WarrantyProvisionPct { get; init; } = 0.02m;

    // ---------- Tiempo y capital ----------

    /// <summary>Costo mensual del capital inmovilizado (interés real u oportunidad).</summary>
    public decimal CapitalCostMonthlyPct { get; init; } = 0.015m;

    /// <summary>Días estimados de venta cuando no hay una estimación específica del vehículo.</summary>
    public int DefaultDaysToSell { get; init; } = 45;

    // ---------- Umbrales de decisión ----------

    /// <summary>Utilidad mínima absoluta por operación, en CLP.</summary>
    public decimal MinProfitAbs { get; init; } = 1_500_000m;

    /// <summary>Rentabilidad anualizada mínima exigida al capital (0,35 = 35% anual).</summary>
    public decimal MinRoiAnnual { get; init; } = 0.35m;

    /// <summary>Piso del margen de seguridad. El margen efectivo crece con la incertidumbre del vehículo.</summary>
    public decimal SafetyMarginBase { get; init; } = 0.05m;

    public decimal SafetyMarginMin { get; init; } = 0.03m;
    public decimal SafetyMarginMax { get; init; } = 0.25m;

    /// <summary>Máximo del capital total que puede quedar comprometido en una sola unidad.</summary>
    public decimal MaxCapitalPerUnitPct { get; init; } = 0.35m;

    /// <summary>Pérdida máxima tolerada en el escenario pesimista, como % del capital desembolsado.</summary>
    public decimal MaxPessimisticLossPct { get; init; } = 0.10m;

    // ---------- Mercado ----------

    /// <summary>Brecha entre precio de lista publicado y precio real de transacción.</summary>
    public decimal NegotiationDiscountPct { get; init; } = 0.07m;

    /// <summary>Ajuste de precio por cada 1.000 km de diferencia con el vehículo objetivo.</summary>
    public decimal MileageAdjustPctPer1000Km { get; init; } = 0.004m;

    /// <summary>Ajuste de precio por cada año de diferencia con el vehículo objetivo.</summary>
    public decimal YearAdjustPct { get; init; } = 0.05m;

    /// <summary>Tope del ajuste total aplicable a un comparable, para que un dato lejano no distorsione.</summary>
    public decimal MaxComparableAdjustmentPct { get; init; } = 0.35m;

    /// <summary>Mínimo de comparables válidos para que la valuación sea utilizable.</summary>
    public int MinComparables { get; init; } = 3;

    // ---------- Umbrales de alerta ----------

    /// <summary>Días en inventario a partir de los cuales un vehículo se considera estancado.</summary>
    public int MaxDaysInInventory { get; init; } = 60;

    /// <summary>Días publicado sin vender que sugieren revisar el precio.</summary>
    public int ListedTooLongDays { get; init; } = 30;

    /// <summary>Margen proyectado bajo el cual la operación deja de compensar.</summary>
    public decimal MinMarginPct { get; init; } = 0.12m;

    /// <summary>Cuánto puede excederse la reparación antes de avisar.</summary>
    public decimal RepairOverBudgetTolerancePct { get; init; } = 0.10m;

    // ---------- Escenarios ----------

    /// <summary>Castigo al precio de venta en el escenario pesimista.</summary>
    public decimal PessimisticSaleFactor { get; init; } = 0.93m;

    /// <summary>Multiplicador de días en el escenario pesimista.</summary>
    public decimal PessimisticDaysFactor { get; init; } = 1.6m;

    /// <summary>Multiplicador de días en el escenario optimista.</summary>
    public decimal OptimisticDaysFactor { get; init; } = 0.7m;

    // ---------- Tributario (definir con tu contador; el sistema no asume régimen) ----------

    /// <summary>Impuesto sobre la utilidad. Se informa aparte, no altera la puja máxima salvo que lo configures.</summary>
    public decimal ProfitTaxPct { get; init; } = 0m;

    // ---------- Semáforo ----------

    public decimal GreenScoreThreshold { get; init; } = 70m;
    public decimal YellowScoreThreshold { get; init; } = 50m;

    /// <summary>Para VERDE, el precio actual debe estar bajo este múltiplo de la puja máxima.</summary>
    public decimal GreenPriceRatio { get; init; } = 0.90m;

    public ScoreWeights Weights { get; init; } = ScoreWeights.Default;

    public static AnalysisParameters Default => new();

    /// <summary>
    /// Tasa proporcional total aplicada sobre el precio de martillo (α en la documentación).
    /// Aquí se resuelve la circularidad: estos costos dependen del precio que estamos despejando.
    /// </summary>
    public decimal ProportionalRate =>
        CommissionPct * (CommissionHasVat ? 1m + VatPct : 1m)
        + AdminFeePct
        + TransferTaxPct;
}
