using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Api.Services;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

/// <summary>Ciclo real del vehículo: compra, gastos, publicación y venta.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class InventoryController(
    InventoryService inventory,
    RematesDbContext db) : ControllerBase
{
    [HttpPost("api/vehicles/{vehicleId:long}/purchase")]
    [ProducesResponseType<PurchaseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RegisterPurchase(
        long vehicleId, [FromBody] RegisterPurchaseRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var purchase = await inventory.RegisterPurchaseAsync(vehicleId, request, ct);
            return Created($"/api/vehicles/{vehicleId}/financials", purchase.ToResponse());
        });

    [HttpGet("api/vehicles/{vehicleId:long}/expenses")]
    [ProducesResponseType<IReadOnlyList<ExpenseResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> GetExpenses(
        long vehicleId, CancellationToken ct)
    {
        // Se materializa antes de mapear: el mapeo es código C#, no algo que EF pueda traducir a SQL.
        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.VehicleId == vehicleId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync(ct);

        return Ok(expenses.Select(e => e.ToResponse()).ToList());
    }

    [HttpPost("api/vehicles/{vehicleId:long}/expenses")]
    [ProducesResponseType<ExpenseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RegisterExpense(
        long vehicleId, [FromBody] RegisterExpenseRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var expense = await inventory.RegisterExpenseAsync(vehicleId, request, ct);
            return Created($"/api/vehicles/{vehicleId}/expenses", expense.ToResponse());
        });

    [HttpDelete("api/expenses/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExpense(long id, CancellationToken ct)
    {
        var expense = await db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (expense is null) return NotFound();

        db.Expenses.Remove(expense);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("api/vehicles/{vehicleId:long}/listing")]
    [ProducesResponseType<ListingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> Publish(
        long vehicleId, [FromBody] PublishListingRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var listing = await inventory.PublishAsync(vehicleId, request, ct);
            return Created($"/api/vehicles/{vehicleId}/financials", listing.ToResponse());
        });

    [HttpPost("api/listings/{listingId:long}/price-change")]
    [ProducesResponseType<PriceChangeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ChangePrice(
        long listingId, [FromBody] ChangePriceRequest request, CancellationToken ct)
        => Run(async () => Ok((await inventory.ChangePriceAsync(listingId, request, ct)).ToResponse()));

    /// <summary>
    /// Registra la venta y cierra la operación: congela el resultado real y guarda la
    /// comparación contra lo que había proyectado el análisis.
    /// </summary>
    [HttpPost("api/vehicles/{vehicleId:long}/sale")]
    [ProducesResponseType<SaleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> RegisterSale(
        long vehicleId, [FromBody] RegisterSaleRequest request, CancellationToken ct)
        => Run(async () =>
        {
            var sale = await inventory.RegisterSaleAsync(vehicleId, request, ct);
            return Created($"/api/vehicles/{vehicleId}/financials", sale.ToResponse());
        });

    /// <summary>Estado económico completo del vehículo, esté vendido o no.</summary>
    [HttpGet("api/vehicles/{vehicleId:long}/financials")]
    [ProducesResponseType<VehicleFinancials>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetFinancials(long vehicleId, CancellationToken ct)
        => Run(async () => Ok(await inventory.GetFinancialsAsync(vehicleId, ct)));

    /// <summary>
    /// Traduce las excepciones del servicio a respuestas HTTP, para no repetir el mismo
    /// try/catch en cada acción.
    /// </summary>
    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = ex.Message });
        }
    }
}
