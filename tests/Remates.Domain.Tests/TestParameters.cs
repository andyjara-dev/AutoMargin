using Remates.Domain.Parameters;

namespace Remates.Domain.Tests;

internal static class TestParameters
{
    /// <summary>
    /// Parámetros neutralizados: sin costos proporcionales, sin costo de capital, sin contingencia
    /// y sin margen de seguridad. Sirven para verificar la aritmética base contra un cálculo hecho a mano.
    /// </summary>
    public static AnalysisParameters Neutral => new()
    {
        CommissionPct = 0m,
        CommissionHasVat = false,
        VatPct = 0m,
        AdminFeePct = 0m,
        TransferTaxPct = 0m,
        TransferFixed = 0m,
        AdminFeeFixed = 0m,
        TransportDefault = 0m,
        DetailingDefault = 0m,
        ContingencyPct = 0m,
        MarketingPct = 0m,
        WarrantyProvisionPct = 0m,
        CapitalCostMonthlyPct = 0m,
        MinProfitAbs = 0m,
        MinRoiAnnual = 0m,
        SafetyMarginBase = 0m,
        SafetyMarginMin = 0m,
        SafetyMarginMax = 0m,
        ProfitTaxPct = 0m,
        MinComparables = 3
    };
}
