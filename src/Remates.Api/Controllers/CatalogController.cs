using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class CatalogController(RematesDbContext db) : ControllerBase
{
    [HttpGet("api/catalog/makes")]
    [ProducesResponseType<IReadOnlyList<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Makes(CancellationToken ct)
    {
        var makes = await db.Makes.AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);

        return Ok(makes);
    }

    [HttpGet("api/catalog/models")]
    [ProducesResponseType<IReadOnlyList<object>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Models([FromQuery] long? makeId, CancellationToken ct)
    {
        var query = db.VehicleModels.AsNoTracking();
        if (makeId is not null) query = query.Where(m => m.MakeId == makeId);

        var models = await query
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name, m.MakeId, m.BodyType })
            .ToListAsync(ct);

        return Ok(models);
    }

    /// <summary>
    /// Costos base de reparación. La UI los usa para precargar el rango al elegir categoría
    /// y gravedad, en vez de dejar al usuario inventando cifras.
    /// </summary>
    [HttpGet("api/catalog/repair-baselines")]
    [ProducesResponseType<IReadOnlyList<RepairCostBaseline>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> RepairBaselines(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var baselines = await db.RepairCostBaselines.AsNoTracking()
            .Where(b => b.ValidFrom <= today)
            .OrderBy(b => b.Category).ThenBy(b => b.Severity)
            .ToListAsync(ct);

        return Ok(baselines);
    }
}
