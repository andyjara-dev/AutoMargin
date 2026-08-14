using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Api.Services;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
[Produces("application/json")]
public sealed class DashboardController(
    DashboardService dashboard,
    RematesDbContext db) : ControllerBase
{
    /// <summary>Estado del negocio: capital, inventario, utilidad, alertas y oportunidades.</summary>
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummary>(StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardSummary>> Summary(CancellationToken ct)
        => Ok(await dashboard.BuildAsync(ct));

    /// <summary>Registra un aporte o un retiro de capital.</summary>
    [HttpPost("capital")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterCapital(
        [FromBody] CashMovementRequest request, CancellationToken ct)
    {
        try
        {
            var movement = await dashboard.RegisterMovementAsync(request, ct);
            return Created("/api/dashboard/summary", new
            {
                movement.Id,
                movement.Type,
                movement.Amount,
                movement.MovementDate,
                movement.Note
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }

    /// <summary>Movimientos de caja más recientes.</summary>
    [HttpGet("capital")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CapitalMovements(CancellationToken ct)
    {
        var movements = await db.CashMovements.AsNoTracking()
            .OrderByDescending(m => m.MovementDate)
            .Take(100)
            .Select(m => new { m.Id, m.Type, m.Amount, m.MovementDate, m.VehicleId, m.Note })
            .ToListAsync(ct);

        return Ok(movements);
    }
}
