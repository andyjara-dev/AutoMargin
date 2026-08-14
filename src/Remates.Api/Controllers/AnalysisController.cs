using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Remates.Api.Contracts;
using Remates.Domain.Analysis;
using Remates.Domain.Parameters;

namespace Remates.Api.Controllers;

/// <summary>
/// Simulador sin persistencia.
///
/// Requiere sesión aunque no toque la base de datos: no expone registros del negocio, pero sí
/// los parámetros con que se calcula (comisión, ROI objetivo, márgenes), que son la metodología
/// del negocio. Publicado en internet, dejarlo abierto equivale a regalarla.
/// </summary>
[ApiController]
[Route("api/analysis")]
[Authorize]
[Produces("application/json")]
public sealed class AnalysisController : ControllerBase
{
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(ILogger<AnalysisController> logger) => _logger = logger;

    /// <summary>
    /// Analiza una oportunidad sin persistir nada. Devuelve puja máxima, utilidad, ROI, escenarios,
    /// score y semáforo. Es el endpoint que usa la pantalla de análisis para recalcular en vivo.
    /// </summary>
    [HttpPost("simulate")]
    [ProducesResponseType<DealAnalysisResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<DealAnalysisResult> Simulate([FromBody] SimulateAnalysisRequest request)
    {
        var validation = Validate(request);
        if (validation is not null) return ValidationProblem(validation);

        var result = DealAnalyzer.Analyze(request.ToDomain());

        _logger.LogInformation(
            "Simulación: precio {Price}, puja máxima {MaxBid}, score {Score}, semáforo {Light}",
            result.CurrentAuctionPrice, result.MaxBid.MaxBid, result.Score.Score, result.Score.TrafficLight);

        return Ok(result);
    }

    /// <summary>Parámetros por defecto del motor. La UI los usa para precargar el formulario.</summary>
    [HttpGet("default-parameters")]
    [ProducesResponseType<AnalysisParameters>(StatusCodes.Status200OK)]
    public ActionResult<AnalysisParameters> DefaultParameters() => Ok(AnalysisParameters.Default);

    /// <summary>
    /// Validaciones que las anotaciones no cubren: coherencia interna de los rangos de costo.
    /// Un rango invertido produciría una incertidumbre negativa y un margen de seguridad sin sentido.
    /// </summary>
    private static ModelStateDictionary? Validate(SimulateAnalysisRequest request)
    {
        var errors = new ModelStateDictionary();

        for (var i = 0; i < request.Damages.Count; i++)
        {
            var damage = request.Damages[i];

            if (damage.CostMin > damage.CostExpected || damage.CostExpected > damage.CostMax)
            {
                errors.AddModelError(
                    $"damages[{i}]",
                    "El rango de costo debe cumplir mínimo ≤ esperado ≤ máximo.");
            }
        }

        if (request.ManualValuation is { } manual)
        {
            if (manual.Expected is { } expected && expected < manual.Conservative)
                errors.AddModelError("manualValuation.expected", "El valor esperado no puede ser menor que el conservador.");

            if (manual.Optimistic is { } optimistic && manual.Expected is { } exp && optimistic < exp)
                errors.AddModelError("manualValuation.optimistic", "El valor optimista no puede ser menor que el esperado.");
        }

        if (request.Comparables.Count == 0 && request.ManualValuation is null)
        {
            errors.AddModelError(
                "comparables",
                "Se requiere al menos un comparable de mercado o un valor de mercado ingresado a mano.");
        }

        return errors.IsValid ? null : errors;
    }
}
