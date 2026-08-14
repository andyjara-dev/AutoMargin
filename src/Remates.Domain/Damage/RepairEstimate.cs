using Remates.Domain.Common;

namespace Remates.Domain.Damage;

public sealed record RepairCategoryTotal
{
    public required DamageCategory Category { get; init; }
    public required decimal Min { get; init; }
    public required decimal Expected { get; init; }
    public required decimal Max { get; init; }
    public required int ItemCount { get; init; }
}

public sealed record RepairEstimate
{
    public required decimal TotalMin { get; init; }
    public required decimal TotalExpected { get; init; }
    public required decimal TotalMax { get; init; }
    public required IReadOnlyList<RepairCategoryTotal> ByCategory { get; init; }

    /// <summary>
    /// Incertidumbre relativa de la reparación: (max-min)/(2*esperado), acotada a [0,1].
    /// Es el principal insumo del margen de seguridad dinámico.
    /// </summary>
    public required decimal UncertaintyRatio { get; init; }

    /// <summary>Riesgo mecánico 0..100, combinando los daños registrados con el nivel de inspección logrado.</summary>
    public required decimal MechanicalRiskScore { get; init; }

    public required bool HasStructuralDamage { get; init; }
    public required bool HasAirbagDamage { get; init; }

    /// <summary>True si alguna línea proviene de IA sin confirmación humana.</summary>
    public required bool ContainsUnconfirmedAiEstimates { get; init; }

    public const string Disclaimer =
        "Estimación referencial basada en la información disponible. No reemplaza una inspección " +
        "mecánica profesional ni un presupuesto de taller.";
}

public static class RepairEstimator
{
    private static readonly DamageCategory[] MechanicalCategories =
    [
        DamageCategory.Mechanical,
        DamageCategory.Electrical,
        DamageCategory.Suspension,
        DamageCategory.Structural,
        DamageCategory.Airbags
    ];

    public static RepairEstimate Calculate(
        IReadOnlyList<DamageItem> items,
        MechanicalInspectionLevel inspectionLevel)
    {
        ArgumentNullException.ThrowIfNull(items);

        var totalMin = items.Sum(i => i.CostMin);
        var totalExpected = items.Sum(i => i.CostExpected);
        var totalMax = items.Sum(i => i.CostMax);

        var byCategory = items
            .GroupBy(i => i.Category)
            .Select(g => new RepairCategoryTotal
            {
                Category = g.Key,
                Min = MoneyMath.RoundToPeso(g.Sum(i => i.CostMin)),
                Expected = MoneyMath.RoundToPeso(g.Sum(i => i.CostExpected)),
                Max = MoneyMath.RoundToPeso(g.Sum(i => i.CostMax)),
                ItemCount = g.Count()
            })
            .OrderByDescending(c => c.Expected)
            .ToList();

        // Sin costo esperado no hay base sobre la cual medir dispersión relativa.
        var uncertainty = totalExpected > 0m
            ? MoneyMath.Clamp01(MoneyMath.SafeDivide(totalMax - totalMin, 2m * totalExpected))
            : 0m;

        return new RepairEstimate
        {
            TotalMin = MoneyMath.RoundToPeso(totalMin),
            TotalExpected = MoneyMath.RoundToPeso(totalExpected),
            TotalMax = MoneyMath.RoundToPeso(totalMax),
            ByCategory = byCategory,
            UncertaintyRatio = MoneyMath.RoundRate(uncertainty),
            MechanicalRiskScore = CalculateMechanicalRisk(items, inspectionLevel),
            HasStructuralDamage = items.Any(i => i.Category == DamageCategory.Structural),
            HasAirbagDamage = items.Any(i => i.Category == DamageCategory.Airbags),
            ContainsUnconfirmedAiEstimates = items.Any(i => i.Source == DamageSource.Ai)
        };
    }

    /// <summary>
    /// El riesgo mecánico nunca baja del piso que impone el nivel de inspección: no haber podido
    /// encender el vehículo es riesgo real aunque no se haya registrado ningún daño.
    /// </summary>
    private static decimal CalculateMechanicalRisk(
        IReadOnlyList<DamageItem> items,
        MechanicalInspectionLevel inspectionLevel)
    {
        var floor = inspectionLevel.BaselineMechanicalRisk();

        var mechanicalItems = items
            .Where(i => MechanicalCategories.Contains(i.Category))
            .ToList();

        if (mechanicalItems.Count == 0)
            return floor;

        var worst = mechanicalItems.Max(i => i.Severity.ToRiskPoints());
        var accumulation = Math.Min(20m, 5m * (mechanicalItems.Count - 1));

        return MoneyMath.Clamp(Math.Max(floor, worst + accumulation), 0m, 100m);
    }
}
