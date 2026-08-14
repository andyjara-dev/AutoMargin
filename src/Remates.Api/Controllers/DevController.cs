using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remates.Api.Services;
using Remates.Infrastructure.Entities;

namespace Remates.Api.Controllers;

/// <summary>
/// Utilidades de desarrollo. Todas verifican el entorno antes de actuar: sembrar datos
/// ficticios en una base con operaciones reales las mezclaría sin vuelta atrás.
/// </summary>
[ApiController]
[Route("api/dev")]
[Authorize(Roles = AppRoles.Admin)]
[Produces("application/json")]
public sealed class DevController(
    DemoDataSeeder seeder,
    IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Carga un historial de demostración para poder recorrer el sistema con datos.</summary>
    [HttpPost("seed-demo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SeedDemo(CancellationToken ct)
    {
        if (!environment.IsDevelopment())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Los datos de demostración solo pueden cargarse en desarrollo.",
                Detail = "En un entorno con operaciones reales, mezclar datos ficticios haría " +
                         "irreconocibles las cifras del negocio."
            });
        }

        var created = await seeder.SeedAsync(ct);

        return Ok(new
        {
            created,
            message = created == 0
                ? "Los datos de demostración ya estaban cargados."
                : $"Se crearon {created} vehículos de demostración."
        });
    }
}
