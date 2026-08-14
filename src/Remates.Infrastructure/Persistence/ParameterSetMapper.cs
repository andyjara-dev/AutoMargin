using Remates.Domain.Parameters;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence;

/// <summary>
/// Traduce entre el conjunto de parámetros persistido (clave/valor, versionable) y el record
/// que consumen los motores.
///
/// La tabla es clave/valor a propósito: agregar un parámetro nuevo no debe requerir una migración.
/// </summary>
public static class ParameterSetMapper
{
    public static IReadOnlyList<ParameterValue> ToValues(AnalysisParameters p) =>
    [
        Num("commission_pct", p.CommissionPct),
        Bool("commission_has_vat", p.CommissionHasVat),
        Num("vat_pct", p.VatPct),
        Num("admin_fee_pct", p.AdminFeePct),
        Num("transfer_tax_pct", p.TransferTaxPct),
        Num("transfer_fixed", p.TransferFixed),
        Num("transport_default", p.TransportDefault),
        Num("detailing_default", p.DetailingDefault),
        Num("admin_fee_fixed", p.AdminFeeFixed),
        Num("contingency_pct", p.ContingencyPct),
        Num("marketing_pct", p.MarketingPct),
        Num("warranty_provision_pct", p.WarrantyProvisionPct),
        Num("capital_cost_monthly_pct", p.CapitalCostMonthlyPct),
        Num("default_days_to_sell", p.DefaultDaysToSell),
        Num("min_profit_abs", p.MinProfitAbs),
        Num("min_roi_annual", p.MinRoiAnnual),
        Num("safety_margin_base", p.SafetyMarginBase),
        Num("safety_margin_min", p.SafetyMarginMin),
        Num("safety_margin_max", p.SafetyMarginMax),
        Num("max_capital_per_unit_pct", p.MaxCapitalPerUnitPct),
        Num("max_pessimistic_loss_pct", p.MaxPessimisticLossPct),
        Num("negotiation_discount_pct", p.NegotiationDiscountPct),
        Num("mileage_adjust_pct_per_1000km", p.MileageAdjustPctPer1000Km),
        Num("year_adjust_pct", p.YearAdjustPct),
        Num("max_comparable_adjustment_pct", p.MaxComparableAdjustmentPct),
        Num("min_comparables", p.MinComparables),
        Num("pessimistic_sale_factor", p.PessimisticSaleFactor),
        Num("pessimistic_days_factor", p.PessimisticDaysFactor),
        Num("optimistic_days_factor", p.OptimisticDaysFactor),
        Num("profit_tax_pct", p.ProfitTaxPct),
        Num("green_score_threshold", p.GreenScoreThreshold),
        Num("yellow_score_threshold", p.YellowScoreThreshold),
        Num("green_price_ratio", p.GreenPriceRatio),
        Num("weight_profitability", p.Weights.Profitability),
        Num("weight_bid_headroom", p.Weights.BidHeadroom),
        Num("weight_liquidity", p.Weights.Liquidity),
        Num("weight_mechanical_risk", p.Weights.MechanicalRisk),
        Num("weight_document_risk", p.Weights.DocumentRisk),
        Num("weight_estimate_certainty", p.Weights.EstimateCertainty),
        Num("weight_evidence_quality", p.Weights.EvidenceQuality)
    ];

    /// <summary>
    /// Reconstruye los parámetros desde la base. Las claves ausentes conservan el valor por
    /// defecto del dominio, de modo que agregar un parámetro nuevo no rompe conjuntos antiguos.
    /// </summary>
    public static AnalysisParameters ToParameters(IEnumerable<ParameterValue> values)
    {
        var map = values.ToDictionary(v => v.Key, v => v);
        var d = AnalysisParameters.Default;

        decimal N(string key, decimal fallback) =>
            map.TryGetValue(key, out var v) && v.NumericValue.HasValue ? v.NumericValue.Value : fallback;

        bool B(string key, bool fallback) =>
            map.TryGetValue(key, out var v) && v.BoolValue.HasValue ? v.BoolValue.Value : fallback;

        return d with
        {
            CommissionPct = N("commission_pct", d.CommissionPct),
            CommissionHasVat = B("commission_has_vat", d.CommissionHasVat),
            VatPct = N("vat_pct", d.VatPct),
            AdminFeePct = N("admin_fee_pct", d.AdminFeePct),
            TransferTaxPct = N("transfer_tax_pct", d.TransferTaxPct),
            TransferFixed = N("transfer_fixed", d.TransferFixed),
            TransportDefault = N("transport_default", d.TransportDefault),
            DetailingDefault = N("detailing_default", d.DetailingDefault),
            AdminFeeFixed = N("admin_fee_fixed", d.AdminFeeFixed),
            ContingencyPct = N("contingency_pct", d.ContingencyPct),
            MarketingPct = N("marketing_pct", d.MarketingPct),
            WarrantyProvisionPct = N("warranty_provision_pct", d.WarrantyProvisionPct),
            CapitalCostMonthlyPct = N("capital_cost_monthly_pct", d.CapitalCostMonthlyPct),
            DefaultDaysToSell = (int)N("default_days_to_sell", d.DefaultDaysToSell),
            MinProfitAbs = N("min_profit_abs", d.MinProfitAbs),
            MinRoiAnnual = N("min_roi_annual", d.MinRoiAnnual),
            SafetyMarginBase = N("safety_margin_base", d.SafetyMarginBase),
            SafetyMarginMin = N("safety_margin_min", d.SafetyMarginMin),
            SafetyMarginMax = N("safety_margin_max", d.SafetyMarginMax),
            MaxCapitalPerUnitPct = N("max_capital_per_unit_pct", d.MaxCapitalPerUnitPct),
            MaxPessimisticLossPct = N("max_pessimistic_loss_pct", d.MaxPessimisticLossPct),
            NegotiationDiscountPct = N("negotiation_discount_pct", d.NegotiationDiscountPct),
            MileageAdjustPctPer1000Km = N("mileage_adjust_pct_per_1000km", d.MileageAdjustPctPer1000Km),
            YearAdjustPct = N("year_adjust_pct", d.YearAdjustPct),
            MaxComparableAdjustmentPct = N("max_comparable_adjustment_pct", d.MaxComparableAdjustmentPct),
            MinComparables = (int)N("min_comparables", d.MinComparables),
            PessimisticSaleFactor = N("pessimistic_sale_factor", d.PessimisticSaleFactor),
            PessimisticDaysFactor = N("pessimistic_days_factor", d.PessimisticDaysFactor),
            OptimisticDaysFactor = N("optimistic_days_factor", d.OptimisticDaysFactor),
            ProfitTaxPct = N("profit_tax_pct", d.ProfitTaxPct),
            GreenScoreThreshold = N("green_score_threshold", d.GreenScoreThreshold),
            YellowScoreThreshold = N("yellow_score_threshold", d.YellowScoreThreshold),
            GreenPriceRatio = N("green_price_ratio", d.GreenPriceRatio),
            Weights = new ScoreWeights
            {
                Profitability = N("weight_profitability", d.Weights.Profitability),
                BidHeadroom = N("weight_bid_headroom", d.Weights.BidHeadroom),
                Liquidity = N("weight_liquidity", d.Weights.Liquidity),
                MechanicalRisk = N("weight_mechanical_risk", d.Weights.MechanicalRisk),
                DocumentRisk = N("weight_document_risk", d.Weights.DocumentRisk),
                EstimateCertainty = N("weight_estimate_certainty", d.Weights.EstimateCertainty),
                EvidenceQuality = N("weight_evidence_quality", d.Weights.EvidenceQuality)
            }
        };
    }

    private static ParameterValue Num(string key, decimal value) => new() { Key = key, NumericValue = value };
    private static ParameterValue Bool(string key, bool value) => new() { Key = key, BoolValue = value };
}
