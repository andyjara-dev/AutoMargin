using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Domain.Damage;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

/// <summary>Comparables de mercado y daños de un vehículo: los dos insumos del análisis.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class VehicleDataController(RematesDbContext db, TimeProvider timeProvider) : ControllerBase
{
    // ---------------- Comparables ----------------

    [HttpGet("api/vehicles/{vehicleId:long}/comparables")]
    [ProducesResponseType<IReadOnlyList<MarketComparableEntity>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarketComparableEntity>>> GetComparables(
        long vehicleId, CancellationToken ct)
    {
        var items = await db.MarketComparables.AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderByDescending(c => c.ObservedAt)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("api/vehicles/{vehicleId:long}/comparables")]
    [ProducesResponseType<MarketComparableEntity>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MarketComparableEntity>> AddComparable(
        long vehicleId, [FromBody] ComparableUpsertRequest request, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct)) return NotFound();

        var entity = new MarketComparableEntity
        {
            VehicleId = vehicleId,
            ListedPrice = request.ListedPrice,
            Year = request.Year,
            MileageKm = request.MileageKm,
            Source = request.Source,
            Url = request.Url,
            Region = request.Region,
            ObservedAt = request.ObservedAt ?? timeProvider.GetUtcNow(),
            IsOutlier = request.IsOutlier,
            OutlierReason = request.OutlierReason
        };

        db.MarketComparables.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetComparables), new { vehicleId }, entity);
    }

    [HttpPut("api/comparables/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateComparable(
        long id, [FromBody] ComparableUpsertRequest request, CancellationToken ct)
    {
        var entity = await db.MarketComparables.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound();

        entity.ListedPrice = request.ListedPrice;
        entity.Year = request.Year;
        entity.MileageKm = request.MileageKm;
        entity.Source = request.Source;
        entity.Url = request.Url;
        entity.Region = request.Region;
        entity.IsOutlier = request.IsOutlier;
        entity.OutlierReason = request.OutlierReason;
        if (request.ObservedAt is not null) entity.ObservedAt = request.ObservedAt.Value;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("api/comparables/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComparable(long id, CancellationToken ct)
    {
        var entity = await db.MarketComparables.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return NotFound();

        db.MarketComparables.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------------- Daños ----------------

    [HttpGet("api/vehicles/{vehicleId:long}/damages")]
    [ProducesResponseType<IReadOnlyList<DamageItemEntity>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DamageItemEntity>>> GetDamages(
        long vehicleId, CancellationToken ct)
    {
        var items = await db.DamageItems.AsNoTracking()
            .Where(d => d.VehicleId == vehicleId)
            .OrderByDescending(d => d.CostExpected)
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("api/vehicles/{vehicleId:long}/damages")]
    [ProducesResponseType<DamageItemEntity>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DamageItemEntity>> AddDamage(
        long vehicleId, [FromBody] DamageUpsertRequest request, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct)) return NotFound();

        if (request.CostMin > request.CostExpected || request.CostExpected > request.CostMax)
        {
            ModelState.AddModelError(nameof(request.CostExpected),
                "El rango de costo debe cumplir mínimo ≤ esperado ≤ máximo.");
            return ValidationProblem(ModelState);
        }

        var entity = new DamageItemEntity
        {
            VehicleId = vehicleId,
            Category = request.Category,
            Severity = request.Severity,
            CostMin = request.CostMin,
            CostExpected = request.CostExpected,
            CostMax = request.CostMax,
            Description = request.Description,
            Source = request.Source,
            Confidence = request.Confidence,
            // Lo que propone la IA queda sin confirmar y no entra al cálculo hasta que alguien lo revise.
            IsConfirmed = request.Source != DamageSource.Ai
        };

        db.DamageItems.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetDamages), new { vehicleId }, entity);
    }

    /// <summary>Confirma una estimación propuesta por IA para que entre al cálculo.</summary>
    [HttpPost("api/damages/{id:long}/confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmDamage(long id, CancellationToken ct)
    {
        var entity = await db.DamageItems.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return NotFound();

        entity.IsConfirmed = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("api/damages/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDamage(long id, CancellationToken ct)
    {
        var entity = await db.DamageItems.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null) return NotFound();

        db.DamageItems.Remove(entity);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
