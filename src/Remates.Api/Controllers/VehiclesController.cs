using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Api.Services;
using Remates.Domain.Analysis;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
[Authorize]
[Produces("application/json")]
public sealed class VehiclesController(
    RematesDbContext db,
    VehicleAnalysisService analysisService,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Listado con el resultado del último análisis de cada vehículo.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<VehicleSummary>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VehicleSummary>>> List(
        [FromQuery] VehicleStatus? status,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var query = db.Vehicles.AsNoTracking();

        if (status is not null) query = query.Where(v => v.Status == status);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(v =>
                EF.Functions.ILike(v.DisplayName ?? "", term) ||
                EF.Functions.ILike(v.Plate ?? "", term) ||
                EF.Functions.ILike(v.Make!.Name, term) ||
                EF.Functions.ILike(v.Model!.Name, term));
        }

        var vehicles = await query
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new
            {
                v.Id,
                v.DisplayName,
                MakeName = v.Make != null ? v.Make.Name : null,
                ModelName = v.Model != null ? v.Model.Name : null,
                v.Year,
                v.MileageKm,
                v.Status,
                v.Region,
                ComparableCount = v.Comparables.Count,
                DamageCount = v.Damages.Count,
                Last = v.Analyses
                    .OrderByDescending(a => a.ComputedAt)
                    .Select(a => new { a.MaxBid, a.Score, a.TrafficLight, a.ComputedAt })
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return Ok(vehicles.Select(v => new VehicleSummary(
            v.Id,
            BuildLabel(v.MakeName, v.ModelName, v.DisplayName, v.Year),
            v.Year,
            v.MileageKm,
            v.Status,
            v.Region,
            v.ComparableCount,
            v.DamageCount,
            v.Last?.MaxBid,
            v.Last?.Score,
            v.Last?.TrafficLight.ToString(),
            v.Last?.ComputedAt)).ToList());
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType<VehicleDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDetail>> Get(long id, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking()
            .Include(x => x.Make).Include(x => x.Model).Include(x => x.Trim)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return v is null ? NotFound() : Ok(ToDetail(v));
    }

    [HttpPost]
    [ProducesResponseType<VehicleDetail>(StatusCodes.Status201Created)]
    public async Task<ActionResult<VehicleDetail>> Create(
        [FromBody] VehicleUpsertRequest request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        var vehicle = new Vehicle { Year = request.Year, MileageKm = request.MileageKm, DetectedAt = now };
        Apply(request, vehicle);

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            FromStatus = null,
            ToStatus = vehicle.Status,
            ChangedAt = now,
            Note = "Vehículo creado."
        });
        await db.SaveChangesAsync(ct);

        await db.Entry(vehicle).Reference(x => x.Make).LoadAsync(ct);
        await db.Entry(vehicle).Reference(x => x.Model).LoadAsync(ct);
        await db.Entry(vehicle).Reference(x => x.Trim).LoadAsync(ct);

        return CreatedAtAction(nameof(Get), new { id = vehicle.Id }, ToDetail(vehicle));
    }

    [HttpPut("{id:long}")]
    [ProducesResponseType<VehicleDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDetail>> Update(
        long id, [FromBody] VehicleUpsertRequest request, CancellationToken ct)
    {
        var vehicle = await db.Vehicles
            .Include(x => x.Make).Include(x => x.Model).Include(x => x.Trim)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (vehicle is null) return NotFound();

        vehicle.Year = request.Year;
        vehicle.MileageKm = request.MileageKm;
        Apply(request, vehicle);

        await db.SaveChangesAsync(ct);
        return Ok(ToDetail(vehicle));
    }

    /// <summary>Cambia el estado y deja la transición registrada en el historial.</summary>
    [HttpPost("{id:long}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(
        long id, [FromBody] ChangeStatusRequest request, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null) return NotFound();

        if (vehicle.Status == request.Status) return NoContent();

        db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            FromStatus = vehicle.Status,
            ToStatus = request.Status,
            ChangedAt = timeProvider.GetUtcNow(),
            Note = request.Note
        });

        vehicle.Status = request.Status;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Baja lógica: el vehículo se oculta pero conserva su historial de análisis.</summary>
    [HttpDelete("{id:long}")]
    [Authorize(Roles = AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vehicle is null) return NotFound();

        vehicle.DeletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    // ---------------- Análisis persistido ----------------

    /// <summary>Analiza el vehículo con sus comparables y daños, y guarda la fotografía del resultado.</summary>
    [HttpPost("{id:long}/analysis")]
    [ProducesResponseType<DealAnalysisResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealAnalysisResult>> Analyze(
        long id, [FromBody] AnalyzeVehicleRequest request, CancellationToken ct)
    {
        try
        {
            var (result, _) = await analysisService.AnalyzeAndSaveAsync(id, request, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:long}/analysis/latest")]
    [ProducesResponseType<DealAnalysisSnapshot>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealAnalysisSnapshot>> Latest(long id, CancellationToken ct)
    {
        var snapshot = await db.DealAnalyses.AsNoTracking()
            .Where(a => a.VehicleId == id)
            .OrderByDescending(a => a.ComputedAt)
            .FirstOrDefaultAsync(ct);

        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("{id:long}/analysis/history")]
    [ProducesResponseType<IReadOnlyList<DealAnalysisSnapshot>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DealAnalysisSnapshot>>> History(
        long id, CancellationToken ct)
    {
        var snapshots = await db.DealAnalyses.AsNoTracking()
            .Where(a => a.VehicleId == id)
            .OrderByDescending(a => a.ComputedAt)
            .Take(50)
            .ToListAsync(ct);

        return Ok(snapshots);
    }

    // ---------------- Auxiliares ----------------

    private static void Apply(VehicleUpsertRequest request, Vehicle vehicle)
    {
        vehicle.MakeId = request.MakeId;
        vehicle.ModelId = request.ModelId;
        vehicle.TrimId = request.TrimId;
        vehicle.DisplayName = request.DisplayName;
        vehicle.Transmission = request.Transmission;
        vehicle.Fuel = request.Fuel;
        vehicle.BodyType = request.BodyType;
        vehicle.Plate = request.Plate;
        vehicle.Vin = request.Vin;
        vehicle.Color = request.Color;
        vehicle.Region = request.Region;
        vehicle.Comuna = request.Comuna;
        vehicle.ConditionNotes = request.ConditionNotes;
        vehicle.InspectionLevel = request.InspectionLevel;
        vehicle.DocumentRisk = request.DocumentRisk;
        vehicle.SourceType = request.SourceType;
        vehicle.Url = request.Url;
    }

    private static VehicleDetail ToDetail(Vehicle v) => new(
        v.Id, v.MakeId, v.Make?.Name, v.ModelId, v.Model?.Name, v.TrimId, v.Trim?.Name,
        v.DisplayName,
        BuildLabel(v.Make?.Name, v.Model?.Name, v.DisplayName, v.Year),
        v.Year, v.MileageKm, v.Transmission, v.Fuel, v.BodyType, v.Plate, v.Vin, v.Color,
        v.Region, v.Comuna, v.ConditionNotes, v.InspectionLevel, v.DocumentRisk, v.Status,
        v.SourceType, v.Url, v.CreatedAt);

    /// <summary>
    /// Nombre legible del vehículo. El catálogo manda solo cuando identifica el vehículo por
    /// completo: con la marca sola, el nombre escrito a mano suele decir más
    /// («Toyota Yaris Sport» frente a «Toyota»).
    /// </summary>
    private static string BuildLabel(string? make, string? model, string? displayName, int year)
    {
        var fromCatalog = string.Join(" ", new[] { make, model }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var catalogIsComplete = !string.IsNullOrWhiteSpace(make) && !string.IsNullOrWhiteSpace(model);

        var baseName = catalogIsComplete ? fromCatalog
            : !string.IsNullOrWhiteSpace(displayName) ? displayName
            : fromCatalog;

        return string.IsNullOrWhiteSpace(baseName) ? $"Vehículo {year}" : $"{baseName} {year}";
    }
}
