using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remates.Api.Services;
using Remates.Domain.Parameters;
using Remates.Infrastructure.Entities;

namespace Remates.Api.Controllers;

public sealed class UpdateParametersRequest
{
    [MaxLength(120)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [Required]
    public AnalysisParameters Parameters { get; set; } = AnalysisParameters.Default;
}

public sealed record ParameterSetResponse(
    long Id,
    string Name,
    bool IsActive,
    DateTimeOffset ValidFrom,
    string? Note,
    AnalysisParameters Parameters);

public sealed record ParameterVersionRow(
    long Id, string Name, bool IsActive, DateTimeOffset ValidFrom, string? Note, string? CreatedBy);

[ApiController]
[Route("api/parameters")]
[Authorize]
[Produces("application/json")]
public sealed class ParametersController(ParameterProvider provider) : ControllerBase
{
    /// <summary>Conjunto de parámetros con el que se calcula todo en este momento.</summary>
    [HttpGet("active")]
    [ProducesResponseType<ParameterSetResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ParameterSetResponse>> Active(CancellationToken ct)
    {
        var (set, parameters) = await provider.GetActiveAsync(ct);
        return Ok(new ParameterSetResponse(set.Id, set.Name, set.IsActive, set.ValidFrom, set.Note, parameters));
    }

    /// <summary>Valores de fábrica, para poder volver al punto de partida.</summary>
    [HttpGet("defaults")]
    [ProducesResponseType<AnalysisParameters>(StatusCodes.Status200OK)]
    public ActionResult<AnalysisParameters> Defaults() => Ok(AnalysisParameters.Default);

    /// <summary>
    /// Guarda los parámetros como una versión nueva y la deja activa. No modifica la anterior:
    /// los análisis ya calculados siguen apuntando a la versión con la que se decidieron.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType<ParameterSetResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParameterSetResponse>> Create(
        [FromBody] UpdateParametersRequest request, CancellationToken ct)
    {
        var validation = Validate(request.Parameters);
        if (validation is not null) return ValidationProblem(validation);

        var set = await provider.CreateVersionAsync(request.Parameters, request.Name, request.Note, ct);

        return CreatedAtAction(nameof(Active), null,
            new ParameterSetResponse(set.Id, set.Name, set.IsActive, set.ValidFrom, set.Note, request.Parameters));
    }

    /// <summary>Historial de versiones.</summary>
    [HttpGet("history")]
    [ProducesResponseType<IReadOnlyList<ParameterVersionRow>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ParameterVersionRow>>> History(CancellationToken ct)
    {
        var sets = await provider.HistoryAsync(ct);

        return Ok(sets
            .Select(s => new ParameterVersionRow(s.Id, s.Name, s.IsActive, s.ValidFrom, s.Note, s.CreatedBy))
            .ToList());
    }

    /// <summary>
    /// Coherencia interna de los parámetros. Son combinaciones que no fallarían al guardarse
    /// pero producirían cálculos sin sentido.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary? Validate(
        AnalysisParameters p)
    {
        var errors = new Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary();

        void Rate(string field, decimal value, decimal max = 1m)
        {
            if (value < 0m || value > max)
                errors.AddModelError(field, $"Debe estar entre 0 y {max:P0}.");
        }

        Rate(nameof(p.CommissionPct), p.CommissionPct, 0.5m);
        Rate(nameof(p.VatPct), p.VatPct, 0.5m);
        Rate(nameof(p.TransferTaxPct), p.TransferTaxPct, 0.5m);
        Rate(nameof(p.ContingencyPct), p.ContingencyPct);
        Rate(nameof(p.MarketingPct), p.MarketingPct);
        Rate(nameof(p.WarrantyProvisionPct), p.WarrantyProvisionPct);
        Rate(nameof(p.CapitalCostMonthlyPct), p.CapitalCostMonthlyPct, 0.2m);
        Rate(nameof(p.NegotiationDiscountPct), p.NegotiationDiscountPct, 0.5m);
        Rate(nameof(p.MaxCapitalPerUnitPct), p.MaxCapitalPerUnitPct);
        Rate(nameof(p.ProfitTaxPct), p.ProfitTaxPct);
        Rate(nameof(p.MinMarginPct), p.MinMarginPct);

        if (p.SafetyMarginMin > p.SafetyMarginMax)
            errors.AddModelError(nameof(p.SafetyMarginMin), "El margen mínimo no puede superar al máximo.");

        if (p.SafetyMarginBase < 0m || p.SafetyMarginBase > 1m)
            errors.AddModelError(nameof(p.SafetyMarginBase), "Debe estar entre 0 y 100%.");

        if (p.MinRoiAnnual < 0m)
            errors.AddModelError(nameof(p.MinRoiAnnual), "No puede ser negativo.");

        if (p.MinProfitAbs < 0m)
            errors.AddModelError(nameof(p.MinProfitAbs), "No puede ser negativa.");

        if (p.MinComparables < 1)
            errors.AddModelError(nameof(p.MinComparables), "Debe exigirse al menos un comparable.");

        if (p.DefaultDaysToSell < 1)
            errors.AddModelError(nameof(p.DefaultDaysToSell), "Debe ser al menos un día.");

        if (p.GreenScoreThreshold < p.YellowScoreThreshold)
        {
            errors.AddModelError(nameof(p.GreenScoreThreshold),
                "El umbral de verde no puede ser menor que el de amarillo.");
        }

        if (p.GreenPriceRatio <= 0m || p.GreenPriceRatio > 1m)
            errors.AddModelError(nameof(p.GreenPriceRatio), "Debe estar entre 0 y 100%.");

        if (p.Weights.Total <= 0m)
            errors.AddModelError("Weights", "La suma de los pesos del score debe ser mayor que cero.");

        if (p.PessimisticSaleFactor is <= 0m or > 1m)
        {
            errors.AddModelError(nameof(p.PessimisticSaleFactor),
                "El castigo del escenario pesimista debe estar entre 0 y 100%.");
        }

        return errors.IsValid ? null : errors;
    }
}
