using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Domain.Inventory;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Ciclo real del vehículo: compra, gastos, publicación y venta.
///
/// Los cálculos de dinero los hace <see cref="RealPerformanceCalculator"/>; aquí se juntan los
/// datos, se mueve el estado y se deja el rastro contable.
/// </summary>
public sealed class InventoryService(
    RematesDbContext db,
    ParameterProvider parameters,
    TimeProvider timeProvider)
{
    /// <summary>Gastos que corresponden a reparación, para comparar contra lo presupuestado.</summary>
    private static readonly ExpenseCategory[] RepairCategories =
        [ExpenseCategory.Repair, ExpenseCategory.Parts, ExpenseCategory.Labor];

    // ---------------- Compra ----------------

    public async Task<Purchase> RegisterPurchaseAsync(
        long vehicleId, RegisterPurchaseRequest request, CancellationToken ct)
    {
        var vehicle = await GetVehicleAsync(vehicleId, ct);

        if (await db.Purchases.AnyAsync(p => p.VehicleId == vehicleId, ct))
            throw new InvalidOperationException("El vehículo ya tiene una compra registrada.");

        var date = request.PurchaseDate ?? timeProvider.GetUtcNow();

        // Se ancla el análisis vigente: sin esta referencia no se puede medir después si acertó.
        var analysisId = await db.DealAnalyses
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ComputedAt)
            .Select(a => (long?)a.Id)
            .FirstOrDefaultAsync(ct);

        var purchase = new Purchase
        {
            VehicleId = vehicleId,
            AuctionLotId = request.AuctionLotId,
            DealAnalysisId = analysisId,
            HammerPrice = request.HammerPrice,
            CommissionPaid = request.CommissionPaid,
            PurchaseDate = date,
            InvoiceRef = request.InvoiceRef,
            Note = request.Note
        };

        db.Purchases.Add(purchase);

        db.CashMovements.Add(new CashMovement
        {
            Type = CashMovementType.Purchase,
            Amount = -(request.HammerPrice + request.CommissionPaid),
            MovementDate = date,
            VehicleId = vehicleId,
            Note = "Compra en remate"
        });

        MoveStatus(vehicle, VehicleStatus.Purchased, date, "Compra registrada.");

        await db.SaveChangesAsync(ct);
        return purchase;
    }

    // ---------------- Gastos ----------------

    public async Task<Expense> RegisterExpenseAsync(
        long vehicleId, RegisterExpenseRequest request, CancellationToken ct)
    {
        await GetVehicleAsync(vehicleId, ct);

        var budgets = await BuildBudgetAsync(vehicleId, ct);
        var date = request.ExpenseDate ?? timeProvider.GetUtcNow();

        var expense = new Expense
        {
            VehicleId = vehicleId,
            Category = request.Category,
            Amount = request.Amount,
            ExpenseDate = date,
            Description = request.Description,
            Supplier = request.Supplier,
            DocumentRef = request.DocumentRef,
            // Nulo si el análisis no presupuesta esa categoría por separado; guardar 0 haría
            // parecer después que todo lo gastado fue sobrecosto.
            BudgetedAmount = budgets.TryGetValue(request.Category, out var budgeted) ? budgeted : null
        };

        db.Expenses.Add(expense);

        db.CashMovements.Add(new CashMovement
        {
            Type = CashMovementType.Expense,
            Amount = -request.Amount,
            MovementDate = date,
            VehicleId = vehicleId,
            Note = $"{request.Category}: {request.Description}".Trim()
        });

        await db.SaveChangesAsync(ct);
        return expense;
    }

    // ---------------- Publicación ----------------

    public async Task<Listing> PublishAsync(
        long vehicleId, PublishListingRequest request, CancellationToken ct)
    {
        var vehicle = await GetVehicleAsync(vehicleId, ct);
        var date = request.PublishedAt ?? timeProvider.GetUtcNow();

        var listing = new Listing
        {
            VehicleId = vehicleId,
            Channel = request.Channel,
            ListPrice = request.ListPrice,
            PublishedAt = date,
            Url = request.Url
        };

        db.Listings.Add(listing);
        MoveStatus(vehicle, VehicleStatus.Listed, date, $"Publicado en {request.Channel}.");

        await db.SaveChangesAsync(ct);
        return listing;
    }

    public async Task<PriceChange> ChangePriceAsync(
        long listingId, ChangePriceRequest request, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == listingId, ct)
            ?? throw new KeyNotFoundException($"No existe la publicación {listingId}.");

        var change = new PriceChange
        {
            ListingId = listingId,
            OldPrice = listing.ListPrice,
            NewPrice = request.NewPrice,
            ChangedAt = timeProvider.GetUtcNow(),
            Reason = request.Reason
        };

        listing.ListPrice = request.NewPrice;
        db.PriceChanges.Add(change);

        await db.SaveChangesAsync(ct);
        return change;
    }

    // ---------------- Venta ----------------

    /// <summary>
    /// Cierra la operación: calcula el resultado real, lo congela y registra la comparación
    /// contra lo que había proyectado el análisis.
    /// </summary>
    public async Task<Sale> RegisterSaleAsync(
        long vehicleId, RegisterSaleRequest request, CancellationToken ct)
    {
        var vehicle = await GetVehicleAsync(vehicleId, ct);

        if (await db.Sales.AnyAsync(s => s.VehicleId == vehicleId, ct))
            throw new InvalidOperationException("El vehículo ya tiene una venta registrada.");

        var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct)
            ?? throw new InvalidOperationException(
                "No se puede registrar la venta sin haber registrado antes la compra.");

        var date = request.SaleDate ?? timeProvider.GetUtcNow();
        var days = Math.Max(0, (int)(date - purchase.PurchaseDate).TotalDays);

        var expenses = await db.Expenses.Where(e => e.VehicleId == vehicleId).ToListAsync(ct);
        var (_, analysisParameters) = await parameters.GetActiveAsync(ct);

        var performance = RealPerformanceCalculator.Calculate(new RealPerformanceInputs
        {
            HammerPrice = purchase.HammerPrice,
            AuctionCosts = purchase.CommissionPaid,
            Expenses = expenses.Sum(e => e.Amount),
            SalePrice = request.SalePrice,
            SaleCosts = request.SaleCosts,
            DaysInInventory = days,
            CapitalCostMonthlyPct = analysisParameters.CapitalCostMonthlyPct,
            ProfitTaxPct = analysisParameters.ProfitTaxPct
        });

        var sale = new Sale
        {
            VehicleId = vehicleId,
            SalePrice = request.SalePrice,
            SaleCosts = request.SaleCosts,
            SaleDate = date,
            BuyerName = request.BuyerName,
            PaymentMethod = request.PaymentMethod,
            Note = request.Note,
            DaysInInventory = performance.DaysInInventory,
            TotalCashInvested = performance.TotalCashInvested,
            CapitalCost = performance.CapitalCost,
            RealProfitCash = performance.ProfitCash,
            RealProfitEconomic = performance.ProfitEconomic,
            RealRoiCash = performance.RoiCash,
            RealRoiEconomic = performance.RoiEconomic,
            RealRoiAnnualized = performance.RoiAnnualized,
            RealMarginPct = performance.MarginPct
        };

        db.Sales.Add(sale);

        db.CashMovements.Add(new CashMovement
        {
            Type = CashMovementType.SaleIncome,
            Amount = request.SalePrice - request.SaleCosts,
            MovementDate = date,
            VehicleId = vehicleId,
            Note = "Venta"
        });

        MoveStatus(vehicle, VehicleStatus.Sold, date, "Venta registrada.");

        await db.SaveChangesAsync(ct);

        await ClosePredictionOutcomeAsync(vehicleId, purchase, sale, expenses, performance, ct);

        return sale;
    }

    /// <summary>
    /// Registra predicción contra realidad. Si el vehículo se compró sin análisis previo no hay
    /// nada que comparar, y se omite en silencio en vez de inventar una línea base.
    /// </summary>
    private async Task ClosePredictionOutcomeAsync(
        long vehicleId,
        Purchase purchase,
        Sale sale,
        List<Expense> expenses,
        RealPerformance performance,
        CancellationToken ct)
    {
        if (purchase.DealAnalysisId is not { } analysisId) return;

        var analysis = await db.DealAnalyses.FirstOrDefaultAsync(a => a.Id == analysisId, ct);
        if (analysis is null) return;

        var actualRepair = expenses
            .Where(e => RepairCategories.Contains(e.Category))
            .Sum(e => e.Amount);

        var inputs = new PredictionAccuracyInputs
        {
            PredictedSaleValue = analysis.SaleValueConservative,
            ActualSaleValue = sale.SalePrice,
            PredictedRepairCost = analysis.RepairExpected,
            ActualRepairCost = actualRepair,
            PredictedDays = analysis.EstimatedDaysToSell,
            ActualDays = sale.DaysInInventory,
            PredictedProfit = analysis.ExpectedProfit,
            // Se compara contra la utilidad económica porque la proyectada también descontaba
            // el costo del capital. Usar la de caja inflaría el acierto del análisis.
            ActualProfit = performance.ProfitEconomic
        };

        var accuracy = PredictionAccuracyCalculator.Calculate(inputs);

        db.PredictionOutcomes.Add(new PredictionOutcome
        {
            VehicleId = vehicleId,
            DealAnalysisId = analysisId,
            SaleId = sale.Id,
            PredictedSaleValue = inputs.PredictedSaleValue,
            ActualSaleValue = inputs.ActualSaleValue,
            PredictedRepairCost = inputs.PredictedRepairCost,
            ActualRepairCost = inputs.ActualRepairCost,
            PredictedDays = inputs.PredictedDays,
            ActualDays = inputs.ActualDays,
            PredictedProfit = inputs.PredictedProfit,
            ActualProfit = inputs.ActualProfit,
            SaleValueErrorPct = accuracy.SaleValueErrorPct,
            RepairCostErrorPct = accuracy.RepairCostErrorPct,
            DaysErrorPct = accuracy.DaysErrorPct,
            ProfitErrorPct = accuracy.ProfitErrorPct,
            UnderPerformed = accuracy.UnderPerformed,
            ClosedAt = timeProvider.GetUtcNow()
        });

        await db.SaveChangesAsync(ct);
    }

    // ---------------- Consulta ----------------

    public async Task<VehicleFinancials> GetFinancialsAsync(long vehicleId, CancellationToken ct)
    {
        var vehicle = await db.Vehicles.AsNoTracking()
            .Include(v => v.Make).Include(v => v.Model)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new KeyNotFoundException($"No existe el vehículo {vehicleId}.");

        var purchase = await db.Purchases.AsNoTracking().FirstOrDefaultAsync(p => p.VehicleId == vehicleId, ct);
        var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.VehicleId == vehicleId, ct);
        var expenses = await db.Expenses.AsNoTracking().Where(e => e.VehicleId == vehicleId).ToListAsync(ct);
        var listing = await db.Listings.AsNoTracking()
            .Where(l => l.VehicleId == vehicleId && l.UnpublishedAt == null)
            .OrderByDescending(l => l.PublishedAt).FirstOrDefaultAsync(ct);

        var (_, analysisParameters) = await parameters.GetActiveAsync(ct);
        var budgets = await BuildBudgetAsync(vehicleId, ct);

        var now = timeProvider.GetUtcNow();
        var days = purchase is null
            ? 0
            : Math.Max(0, (int)((sale?.SaleDate ?? now) - purchase.PurchaseDate).TotalDays);

        var performance = RealPerformanceCalculator.Calculate(new RealPerformanceInputs
        {
            HammerPrice = purchase?.HammerPrice ?? 0m,
            AuctionCosts = purchase?.CommissionPaid ?? 0m,
            Expenses = expenses.Sum(e => e.Amount),
            SalePrice = sale?.SalePrice ?? 0m,
            SaleCosts = sale?.SaleCosts ?? 0m,
            DaysInInventory = days,
            CapitalCostMonthlyPct = analysisParameters.CapitalCostMonthlyPct,
            ProfitTaxPct = analysisParameters.ProfitTaxPct
        });

        var byCategory = expenses
            .GroupBy(e => e.Category)
            .Select(g =>
            {
                var actual = g.Sum(e => e.Amount);
                decimal? budgeted = budgets.TryGetValue(g.Key, out var b) ? b : null;
                var variance = budgeted is null ? (decimal?)null : actual - budgeted;

                return new ExpenseByCategory(
                    g.Key,
                    actual,
                    budgeted,
                    variance,
                    budgeted is null or 0m ? null : Math.Round(variance!.Value / budgeted.Value, 4),
                    g.Count());
            })
            .OrderByDescending(c => c.Actual)
            .ToList();

        // La reparación se compara agrupada: el análisis la estima como una cifra única y el
        // gasto real se registra repartido entre reparación, repuestos y mano de obra.
        var repairBudget = budgets.GetValueOrDefault(ExpenseCategory.Repair);
        var repairActual = expenses.Where(e => RepairCategories.Contains(e.Category)).Sum(e => e.Amount);
        var repairVariance = repairActual - repairBudget;

        var repair = new RepairSummary(
            repairBudget,
            repairActual,
            repairVariance,
            repairBudget == 0m ? null : Math.Round(repairVariance / repairBudget, 4),
            repairVariance > 0m);

        var outcome = await db.PredictionOutcomes.AsNoTracking()
            .FirstOrDefaultAsync(o => o.VehicleId == vehicleId, ct);

        return new VehicleFinancials(
            vehicle.Id,
            BuildLabel(vehicle),
            vehicle.Status,
            purchase?.HammerPrice,
            purchase?.CommissionPaid,
            expenses.Sum(e => e.Amount),
            budgets.Values.Sum(),
            byCategory,
            repair,
            listing?.ListPrice,
            sale?.SalePrice,
            purchase?.PurchaseDate,
            sale?.SaleDate,
            performance,
            outcome is null ? null : new PredictionComparison(
                outcome.PredictedSaleValue, outcome.ActualSaleValue,
                outcome.PredictedRepairCost, outcome.ActualRepairCost,
                outcome.PredictedDays, outcome.ActualDays,
                outcome.PredictedProfit, outcome.ActualProfit,
                outcome.SaleValueErrorPct, outcome.RepairCostErrorPct,
                outcome.DaysErrorPct, outcome.ProfitErrorPct,
                outcome.UnderPerformed));
    }

    // ---------------- Auxiliares ----------------

    /// <summary>
    /// Presupuesto por categoría, tomado del último análisis. Es lo que permite contrastar
    /// gasto real contra lo que se había estimado al decidir la compra.
    /// </summary>
    private async Task<Dictionary<ExpenseCategory, decimal>> BuildBudgetAsync(
        long vehicleId, CancellationToken ct)
    {
        var budgets = new Dictionary<ExpenseCategory, decimal>();

        var analysis = await db.DealAnalyses.AsNoTracking()
            .Where(a => a.VehicleId == vehicleId)
            .OrderByDescending(a => a.ComputedAt)
            .FirstOrDefaultAsync(ct);

        if (analysis is null) return budgets;

        budgets[ExpenseCategory.Repair] = analysis.RepairExpected;

        if (string.IsNullOrWhiteSpace(analysis.CostBreakdownJson)) return budgets;

        try
        {
            using var doc = JsonDocument.Parse(analysis.CostBreakdownJson);
            if (!doc.RootElement.TryGetProperty("fixedCostLines", out var lines)) return budgets;

            foreach (var line in lines.EnumerateArray())
            {
                var key = line.GetProperty("key").GetString();
                var amount = line.GetProperty("amount").GetDecimal();

                var category = key switch
                {
                    "transport" => ExpenseCategory.Transport,
                    "detailing" => ExpenseCategory.Detailing,
                    "transferFixed" => ExpenseCategory.Transfer,
                    "adminFixed" => ExpenseCategory.AuctionFee,
                    // Los imprevistos son literalmente gastos varios: engrosan ese presupuesto.
                    "other" or "contingency" => ExpenseCategory.Other,
                    _ => (ExpenseCategory?)null
                };

                if (category is { } c) budgets[c] = budgets.GetValueOrDefault(c) + amount;
            }
        }
        catch (JsonException)
        {
            // Un desglose ilegible no debe impedir registrar gastos; se queda sin presupuesto.
        }

        return budgets;
    }

    private async Task<Vehicle> GetVehicleAsync(long vehicleId, CancellationToken ct)
        => await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
           ?? throw new KeyNotFoundException($"No existe el vehículo {vehicleId}.");

    private void MoveStatus(Vehicle vehicle, VehicleStatus to, DateTimeOffset at, string note)
    {
        if (vehicle.Status == to) return;

        db.VehicleStatusHistory.Add(new VehicleStatusHistory
        {
            VehicleId = vehicle.Id,
            FromStatus = vehicle.Status,
            ToStatus = to,
            ChangedAt = at,
            Note = note
        });

        vehicle.Status = to;
    }

    private static string BuildLabel(Vehicle v)
    {
        var fromCatalog = string.Join(" ", new[] { v.Make?.Name, v.Model?.Name }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var complete = v.Make is not null && v.Model is not null;

        var baseName = complete ? fromCatalog
            : !string.IsNullOrWhiteSpace(v.DisplayName) ? v.DisplayName
            : fromCatalog;

        return string.IsNullOrWhiteSpace(baseName) ? $"Vehículo {v.Year}" : $"{baseName} {v.Year}";
    }
}
