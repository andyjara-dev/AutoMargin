using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Services;
using Remates.Domain.Market;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

public sealed class ParseListingRequest
{
    [Required, MaxLength(4000)]
    public string Text { get; set; } = string.Empty;
}

public sealed class ImportComparablesRequest
{
    [Required]
    public List<MarketSearchResult> Results { get; set; } = [];
}

/// <summary>
/// Búsqueda de comparables de mercado.
///
/// Reduce el trabajo más tedioso del análisis: transcribir a mano los avisos que sostienen la
/// valuación, que es además donde se cometen los errores de dedo.
/// </summary>
[ApiController]
[Route("api/market")]
[Authorize]
[Produces("application/json")]
public sealed class MarketController(
    MarketSearchService market,
    RematesDbContext db) : ControllerBase
{
    /// <summary>
    /// Extrae los datos de un aviso pegado como texto.
    ///
    /// Es la vía que funciona con cualquier portal, incluidos los que no permiten lectura
    /// automatizada: copiar y pegar un aviso que ya se está mirando no es rastrear un sitio.
    /// </summary>
    [HttpPost("parse")]
    [ProducesResponseType<ParsedListing>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ParsedListing>> Parse(
        [FromBody] ParseListingRequest request, CancellationToken ct)
    {
        var makes = await db.Makes.AsNoTracking().Select(m => m.Name).ToListAsync(ct);

        return Ok(ListingParser.Parse(request.Text, makes));
    }

    /// <summary>Busca avisos comparables en las fuentes configuradas.</summary>
    [HttpGet("search")]
    [ProducesResponseType<MarketSearchResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketSearchResponse>> Search(
        [FromQuery] string? make,
        [FromQuery] string? model,
        [FromQuery] int? year,
        [FromQuery] int yearTolerance = 2,
        [FromQuery] string? region = null,
        [FromQuery] int limit = 25,
        CancellationToken ct = default)
    {
        var query = new MarketSearchQuery
        {
            Make = make,
            Model = model,
            Year = year,
            YearTolerance = Math.Clamp(yearTolerance, 0, 5),
            Region = region,
            Limit = Math.Clamp(limit, 1, 50)
        };

        return Ok(await market.SearchAsync(query, ct));
    }

    /// <summary>Busca comparables para un vehículo ya registrado, usando sus propios datos.</summary>
    [HttpGet("search/vehicle/{vehicleId:long}")]
    [ProducesResponseType<MarketSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarketSearchResponse>> SearchForVehicle(
        long vehicleId, [FromQuery] int limit = 25, CancellationToken ct = default)
    {
        var vehicle = await db.Vehicles.AsNoTracking()
            .Include(v => v.Make).Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct);

        if (vehicle is null) return NotFound();

        var query = new MarketSearchQuery
        {
            Make = vehicle.Make?.Name,
            Model = vehicle.Model?.Name,
            Year = vehicle.Year,
            Region = vehicle.Region,
            Limit = Math.Clamp(limit, 1, 50),
            // El nombre escrito a mano sirve de respaldo cuando el catálogo está incompleto.
            FreeText = vehicle.DisplayName
        };

        return Ok(await market.SearchAsync(query, ct));
    }

    /// <summary>Carga como comparables del vehículo los avisos seleccionados.</summary>
    [HttpPost("import/{vehicleId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Import(
        long vehicleId, [FromBody] ImportComparablesRequest request, CancellationToken ct)
    {
        try
        {
            var imported = await market.ImportAsync(vehicleId, request.Results, ct);

            return Ok(new
            {
                imported,
                skipped = request.Results.Count - imported,
                message = imported == 0
                    ? "No se importó ninguno: o ya estaban cargados, o les faltaba precio o año."
                    : $"Se importaron {imported} comparables."
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message });
        }
    }
}
