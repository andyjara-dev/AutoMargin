using FluentValidation;
using Remates.Api.Contracts;

namespace Remates.Api.Validation;

/// <summary>
/// Validaciones que las anotaciones no expresan bien: coherencia entre campos.
/// Un rango de costo invertido no falla al guardarse, pero produce una incertidumbre negativa
/// y con ella un margen de seguridad sin sentido.
/// </summary>
public sealed class DamageUpsertValidator : AbstractValidator<DamageUpsertRequest>
{
    public DamageUpsertValidator()
    {
        RuleFor(x => x.CostMin).LessThanOrEqualTo(x => x.CostExpected)
            .WithMessage("El costo mínimo no puede superar al esperado.");

        RuleFor(x => x.CostExpected).LessThanOrEqualTo(x => x.CostMax)
            .WithMessage("El costo esperado no puede superar al máximo.");
    }
}

public sealed class DamageDtoValidator : AbstractValidator<DamageDto>
{
    public DamageDtoValidator()
    {
        RuleFor(x => x.CostMin).LessThanOrEqualTo(x => x.CostExpected)
            .WithMessage("El costo mínimo no puede superar al esperado.");

        RuleFor(x => x.CostExpected).LessThanOrEqualTo(x => x.CostMax)
            .WithMessage("El costo esperado no puede superar al máximo.");
    }
}

public sealed class ManualValuationValidator : AbstractValidator<ManualValuationDto>
{
    public ManualValuationValidator()
    {
        RuleFor(x => x.Expected)
            .GreaterThanOrEqualTo(x => x.Conservative)
            .When(x => x.Expected.HasValue)
            .WithMessage("El valor esperado no puede ser menor que el conservador.");

        RuleFor(x => x.Optimistic)
            .GreaterThanOrEqualTo(x => x.Expected!.Value)
            .When(x => x.Optimistic.HasValue && x.Expected.HasValue)
            .WithMessage("El valor optimista no puede ser menor que el esperado.");
    }
}

public sealed class SimulateAnalysisValidator : AbstractValidator<SimulateAnalysisRequest>
{
    public SimulateAnalysisValidator()
    {
        RuleForEach(x => x.Damages).SetValidator(new DamageDtoValidator());
        RuleFor(x => x.ManualValuation!).SetValidator(new ManualValuationValidator())
            .When(x => x.ManualValuation is not null);

        RuleFor(x => x)
            .Must(x => x.Comparables.Count > 0 || x.ManualValuation is not null)
            .WithName("comparables")
            .WithMessage("Se requiere al menos un comparable de mercado o un valor ingresado a mano.");
    }
}

public sealed class RegisterSaleValidator : AbstractValidator<RegisterSaleRequest>
{
    public RegisterSaleValidator()
    {
        RuleFor(x => x.SaleCosts).LessThan(x => x.SalePrice)
            .WithMessage("Los costos de venta no pueden igualar ni superar el precio de venta.");

        RuleFor(x => x.SaleDate)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .When(x => x.SaleDate.HasValue)
            .WithMessage("La fecha de venta no puede estar en el futuro.");
    }
}

public sealed class RegisterPurchaseValidator : AbstractValidator<RegisterPurchaseRequest>
{
    public RegisterPurchaseValidator()
    {
        RuleFor(x => x.PurchaseDate)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .When(x => x.PurchaseDate.HasValue)
            .WithMessage("La fecha de compra no puede estar en el futuro.");
    }
}

public sealed class ComparableUpsertValidator : AbstractValidator<ComparableUpsertRequest>
{
    public ComparableUpsertValidator()
    {
        RuleFor(x => x.ObservedAt)
            .LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddDays(1))
            .When(x => x.ObservedAt.HasValue)
            .WithMessage("La fecha de observación no puede estar en el futuro.");
    }
}
