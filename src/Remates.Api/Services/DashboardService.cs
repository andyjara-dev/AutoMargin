using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Domain.Alerts;
using Remates.Domain.Learning;
using Remates.Domain.Common;
using Remates.Domain.Inventory;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Arma el dashboard. Todos los agregados salen de consultas sobre datos ya registrados;
/// ningún número se estima aquí.
/// </summary>
public sealed class DashboardService(
    RematesDbContext db,
    ParameterProvider parameters,
    TimeProvider timeProvider)
{
    private static readonly ExpenseCategory[] RepairCategories =
        [ExpenseCategory.Repair, ExpenseCategory.Parts, ExpenseCategory.Labor];

    public async Task<DashboardSummary> BuildAsync(CancellationToken ct)
    {
        var (_, analysisParameters) = await parameters.GetActiveAsync(ct);
        var now = timeProvider.GetUtcNow();

        var purchases = await db.Purchases.AsNoTracking().ToListAsync(ct);
        var sales = await db.Sales.AsNoTracking().ToListAsync(ct);
        var expenses = await db.Expenses.AsNoTracking().ToListAsync(ct);
        var movements = await db.CashMovements.AsNoTracking().ToListAsync(ct);

        var vehicles = await db.Vehicles.AsNoTracking()
            .Include(v => v.Make).Include(v => v.Model)
            .ToListAsync(ct);

        // Último análisis de cada vehículo, para valorar el inventario y listar oportunidades.
        var latestAnalyses = await db.DealAnalyses.AsNoTracking()
            .GroupBy(a => a.VehicleId)
            .Select(g => g.OrderByDescending(a => a.ComputedAt).First())
            .ToListAsync(ct);

        var analysisByVehicle = latestAnalyses.ToDictionary(a => a.VehicleId);
        var purchaseByVehicle = purchases.ToDictionary(p => p.VehicleId);
        var saleByVehicle = sales.ToDictionary(s => s.VehicleId);
        var expensesByVehicle = expenses.GroupBy(e => e.VehicleId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var listings = await db.Listings.AsNoTracking()
            .Where(l => l.UnpublishedAt == null)
            .ToListAsync(ct);
        var listingByVehicle = listings
            .GroupBy(l => l.VehicleId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.PublishedAt).First());

        // ---- Capital ----
        var contributed = movements
            .Where(m => m.Type is CashMovementType.Contribution or CashMovementType.Withdrawal)
            .Sum(m => m.Amount);

        var available = movements.Sum(m => m.Amount);

        var snapshots = new List<InventorySnapshot>();
        decimal immobilized = 0m;
        decimal inventoryCost = 0m;
        decimal inventoryExpected = 0m;
        decimal valuedCost = 0m;
        decimal unvaluedCost = 0m;
        var unvaluedCount = 0;

        foreach (var purchase in purchases)
        {
            var vehicle = vehicles.FirstOrDefault(v => v.Id == purchase.VehicleId);
            if (vehicle is null) continue;

            var vehicleExpenses = expensesByVehicle.GetValueOrDefault(purchase.VehicleId, []);
            var cashInvested = purchase.HammerPrice + purchase.CommissionPaid
                             + vehicleExpenses.Sum(e => e.Amount);

            var sale = saleByVehicle.GetValueOrDefault(purchase.VehicleId);
            var isSold = sale is not null;

            var days = Math.Max(0, (int)((sale?.SaleDate ?? now) - purchase.PurchaseDate).TotalDays);

            var listing = listingByVehicle.GetValueOrDefault(purchase.VehicleId);
            var daysListed = listing is null || isSold
                ? 0
                : Math.Max(0, (int)(now - listing.PublishedAt).TotalDays);

            var analysis = analysisByVehicle.GetValueOrDefault(purchase.VehicleId);
            var expectedValue = analysis?.SaleValueConservative ?? listing?.ListPrice ?? 0m;

            if (!isSold)
            {
                immobilized += cashInvested;
                inventoryCost += cashInvested;

                // Un vehículo sin valuación no vale cero: queda fuera del cálculo y se cuenta
                // aparte, para no inventar una pérdida potencial que no existe.
                if (expectedValue > 0m)
                {
                    inventoryExpected += expectedValue;
                    valuedCost += cashInvested;
                }
                else
                {
                    unvaluedCount++;
                    unvaluedCost += cashInvested;
                }
            }

            snapshots.Add(new InventorySnapshot
            {
                VehicleId = vehicle.Id,
                Label = BuildLabel(vehicle),
                CashInvested = cashInvested,
                DaysInInventory = days,
                DaysListed = daysListed,
                IsSold = isSold,
                HasAnalysis = purchase.DealAnalysisId is not null,
                ExpectedSaleValue = expectedValue,
                RepairBudgeted = analysis?.RepairExpected ?? 0m,
                RepairActual = vehicleExpenses
                    .Where(e => RepairCategories.Contains(e.Category))
                    .Sum(e => e.Amount)
            });
        }

        var totalCapital = Math.Max(contributed, available + immobilized);

        var capital = new CapitalSummary(
            MoneyMath.RoundToPeso(contributed),
            MoneyMath.RoundToPeso(available),
            MoneyMath.RoundToPeso(immobilized),
            MoneyMath.RoundRate(MoneyMath.SafeDivide(immobilized, totalCapital)));

        // ---- Inventario ----
        var openSnapshots = snapshots.Where(s => !s.IsSold).ToList();

        var inventory = new InventorySummary(
            openSnapshots.Count,
            sales.Count,
            vehicles.Count(v => v.Status is VehicleStatus.Detected or VehicleStatus.Analyzing
                                          or VehicleStatus.Bidding),
            MoneyMath.RoundToPeso(inventoryCost),
            MoneyMath.RoundToPeso(inventoryExpected),
            // La utilidad potencial se compara solo contra el costo de los vehículos valuados.
            MoneyMath.RoundToPeso(inventoryExpected - valuedCost),
            openSnapshots.Count == 0 ? 0m : Math.Round(openSnapshots.Average(s => (decimal)s.DaysInInventory), 1),
            unvaluedCount,
            MoneyMath.RoundToPeso(unvaluedCost));

        // ---- Utilidad ----
        var last30 = now.AddDays(-30);
        var recentSales = sales.Where(s => s.SaleDate >= last30).ToList();

        var profit = new ProfitSummary(
            MoneyMath.RoundToPeso(sales.Sum(s => s.RealProfitCash)),
            MoneyMath.RoundToPeso(sales.Sum(s => s.RealProfitEconomic)),
            sales.Count == 0 ? 0m : MoneyMath.RoundRate(sales.Average(s => s.RealRoiEconomic)),
            sales.Count == 0 ? 0m : MoneyMath.RoundRate(sales.Average(s => s.RealMarginPct)),
            sales.Count == 0 ? 0m : Math.Round(sales.Average(s => (decimal)s.DaysInInventory), 1),
            MoneyMath.RoundToPeso(recentSales.Sum(s => s.RealProfitEconomic)),
            recentSales.Count);

        // ---- Ranking por modelo ----
        var byModel = sales
            .Join(vehicles, s => s.VehicleId, v => v.Id, (s, v) => new { Sale = s, Vehicle = v })
            .GroupBy(x => ModelKey(x.Vehicle))
            .Select(g => new ModelPerformance(
                g.Key,
                g.Count(),
                MoneyMath.RoundToPeso(g.Sum(x => x.Sale.RealProfitEconomic)),
                MoneyMath.RoundToPeso(g.Average(x => x.Sale.RealProfitEconomic)),
                MoneyMath.RoundRate(g.Average(x => x.Sale.RealRoiEconomic)),
                Math.Round(g.Average(x => (decimal)x.Sale.DaysInInventory), 1)))
            .ToList();

        // ---- Oportunidades: analizadas y aún sin comprar ----
        var opportunities = latestAnalyses
            .Where(a => !purchaseByVehicle.ContainsKey(a.VehicleId))
            .OrderByDescending(a => a.Score)
            .Take(10)
            .Select(a =>
            {
                var vehicle = vehicles.FirstOrDefault(v => v.Id == a.VehicleId);
                return new OpportunityRow(
                    a.VehicleId,
                    vehicle is null ? $"Vehículo {a.VehicleId}" : BuildLabel(vehicle),
                    a.MaxBid, a.CurrentAuctionPrice, a.Headroom,
                    a.Score, a.TrafficLight.ToString(),
                    a.RoiAnnualized, a.EstimatedDaysToSell, a.ComputedAt);
            })
            .ToList();

        // ---- Alertas ----
        var alerts = AlertEngine.Evaluate(
            new AlertContext
            {
                Inventory = snapshots,
                TotalCapital = totalCapital,
                AvailableCapital = available
            },
            analysisParameters);

        return new DashboardSummary(
            capital,
            inventory,
            profit,
            byModel.OrderByDescending(m => m.AverageProfit).Take(5).ToList(),
            byModel.OrderBy(m => m.AverageProfit).Take(5).ToList(),
            opportunities,
            alerts,
            sales.Count,
            await CalibrationAsync(ct));
    }

    /// <summary>
    /// Solo entran los remates a los que efectivamente se fue a pujar. Los desistidos quedan
    /// fuera: no perdimos contra nadie, decidimos no ir, y contarlos como derrotas haría creer
    /// que la puja máxima va corta cuando lo que pasó fue que el auto no convencía.
    /// </summary>
    private async Task<CalibrationReport> CalibrationAsync(CancellationToken ct)
    {
        var bids = await db.Bids.AsNoTracking()
            .Where(b => b.Result != BidResult.NotBid)
            .Select(b => new BidOutcome
            {
                MaxBidAuthorized = b.MaxBidAuthorized,
                WinningPrice = b.WinningPrice,
                Won = b.Result == BidResult.Won
            })
            .ToListAsync(ct);

        return CalibrationCalculator.Analyze(bids);
    }

    public async Task<CashMovement> RegisterMovementAsync(
        CashMovementRequest request, CancellationToken ct)
    {
        if (request.Type is not (CashMovementType.Contribution or CashMovementType.Withdrawal))
        {
            throw new InvalidOperationException(
                "Solo se registran a mano aportes y retiros de capital. Las compras, gastos e " +
                "ingresos por venta los genera el propio ciclo del vehículo.");
        }

        var movement = new CashMovement
        {
            Type = request.Type,
            // El retiro sale de la caja: se guarda con signo negativo para que la suma cuadre.
            Amount = request.Type == CashMovementType.Withdrawal
                ? -Math.Abs(request.Amount)
                : Math.Abs(request.Amount),
            MovementDate = request.MovementDate ?? timeProvider.GetUtcNow(),
            Note = request.Note
        };

        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        return movement;
    }

    private static string ModelKey(Vehicle v)
    {
        var name = string.Join(" ", new[] { v.Make?.Name, v.Model?.Name }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

        return string.IsNullOrWhiteSpace(name)
            ? v.DisplayName ?? "Sin clasificar"
            : name;
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
