using Microsoft.EntityFrameworkCore;
using Remates.Api.Contracts;
using Remates.Domain.Damage;
using Remates.Domain.Inventory;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Genera un historial ficticio para poder recorrer el sistema sin cargar todo a mano.
///
/// Los vehículos creados llevan la marca <see cref="DemoTag"/> en las notas, de modo que se
/// distinguen de los reales y se pueden borrar. Solo se ejecuta en desarrollo.
/// </summary>
public sealed class DemoDataSeeder(
    RematesDbContext db,
    VehicleAnalysisService analysisService,
    InventoryService inventory,
    DashboardService dashboard,
    TimeProvider timeProvider,
    ILogger<DemoDataSeeder> logger)
{
    public const string DemoTag = "[demo]";

    private sealed record DemoVehicle(
        string Make,
        string Model,
        int Year,
        int MileageKm,
        decimal MarketPrice,
        decimal HammerPrice,
        decimal RepairExpected,
        decimal RepairActual,
        int DaysAgoPurchased,
        int? DaysToSell,
        decimal? SalePrice,
        MechanicalInspectionLevel Inspection,
        DocumentRiskLevel DocumentRisk);

    /// <summary>
    /// Casos elegidos para que el dashboard muestre situaciones distintas: operaciones buenas,
    /// una que se pasó de reparación, una lenta y una todavía abierta y estancada.
    /// </summary>
    private static readonly DemoVehicle[] Catalog =
    [
        new("Hyundai", "Accent", 2019, 54_000, 9_800_000m, 4_600_000m, 620_000m, 590_000m, 210, 26, 9_650_000m, MechanicalInspectionLevel.TestDrive, DocumentRiskLevel.None),
        new("Chevrolet", "Sail", 2018, 78_000, 6_900_000m, 3_200_000m, 480_000m, 730_000m, 185, 41, 6_700_000m, MechanicalInspectionLevel.EngineRun, DocumentRiskLevel.Low),
        new("Suzuki", "Swift", 2020, 41_000, 11_200_000m, 5_600_000m, 380_000m, 350_000m, 160, 19, 11_150_000m, MechanicalInspectionLevel.TestDrive, DocumentRiskLevel.None),
        new("Nissan", "Versa", 2018, 92_000, 8_400_000m, 3_900_000m, 900_000m, 1_480_000m, 140, 74, 8_050_000m, MechanicalInspectionLevel.VisualOnly, DocumentRiskLevel.Medium),
        new("Kia", "Morning", 2019, 63_000, 7_300_000m, 3_500_000m, 410_000m, 395_000m, 115, 23, 7_250_000m, MechanicalInspectionLevel.TestDrive, DocumentRiskLevel.None),
        new("Mazda", "3", 2017, 105_000, 10_600_000m, 5_100_000m, 1_100_000m, 1_320_000m, 95, 58, 10_200_000m, MechanicalInspectionLevel.EngineRun, DocumentRiskLevel.Low),
        new("Toyota", "Corolla", 2020, 38_000, 14_500_000m, 7_800_000m, 520_000m, 505_000m, 78, 21, 14_400_000m, MechanicalInspectionLevel.WorkshopReport, DocumentRiskLevel.None),
        new("Peugeot", "208", 2018, 71_000, 8_100_000m, 4_100_000m, 760_000m, 1_150_000m, 62, 47, 7_700_000m, MechanicalInspectionLevel.VisualOnly, DocumentRiskLevel.Low),
        // Sigue en inventario y ya lleva demasiados días: debe disparar alerta.
        new("Renault", "Sandero", 2017, 118_000, 6_500_000m, 3_000_000m, 850_000m, 1_240_000m, 88, null, null, MechanicalInspectionLevel.VisualOnly, DocumentRiskLevel.Medium),
        // Recién comprado, todavía en preparación.
        new("Chevrolet", "Onix", 2021, 29_000, 12_400_000m, 6_900_000m, 340_000m, 320_000m, 9, null, null, MechanicalInspectionLevel.TestDrive, DocumentRiskLevel.None)
    ];

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        if (await db.Vehicles.AnyAsync(v => v.ConditionNotes!.Contains(DemoTag), ct))
        {
            logger.LogInformation("Los datos de demostración ya estaban cargados.");
            return 0;
        }

        // Capital suficiente para que las compras no dejen la caja en negativo.
        await dashboard.RegisterMovementAsync(new CashMovementRequest
        {
            Type = CashMovementType.Contribution,
            Amount = 60_000_000m,
            MovementDate = timeProvider.GetUtcNow().AddDays(-240),
            Note = $"{DemoTag} Capital inicial de demostración"
        }, ct);

        var created = 0;

        foreach (var demo in Catalog)
        {
            await CreateAsync(demo, ct);
            created++;
        }

        logger.LogInformation("Datos de demostración creados: {Count} vehículos.", created);
        return created;
    }

    private async Task CreateAsync(DemoVehicle demo, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var purchaseDate = now.AddDays(-demo.DaysAgoPurchased);

        var make = await db.Makes.FirstOrDefaultAsync(m => m.Name == demo.Make, ct);
        var model = await GetOrCreateModelAsync(make, demo.Model, ct);

        var vehicle = new Vehicle
        {
            MakeId = make?.Id,
            ModelId = model?.Id,
            DisplayName = $"{demo.Make} {demo.Model}",
            Year = demo.Year,
            MileageKm = demo.MileageKm,
            InspectionLevel = demo.Inspection,
            DocumentRisk = demo.DocumentRisk,
            Region = "Metropolitana",
            Status = VehicleStatus.Detected,
            DetectedAt = purchaseDate.AddDays(-5),
            ConditionNotes = $"{DemoTag} Generado para poblar el sistema."
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        // Comparables alrededor del valor de mercado, para que la valuación tenga dispersión real.
        decimal[] spread = [0.96m, 0.99m, 1.00m, 1.03m, 1.06m];
        foreach (var factor in spread)
        {
            db.MarketComparables.Add(new MarketComparableEntity
            {
                VehicleId = vehicle.Id,
                ListedPrice = Math.Round(demo.MarketPrice * factor, 0),
                Year = demo.Year,
                MileageKm = demo.MileageKm + (int)((factor - 1m) * 40_000m),
                Source = "Demo",
                ObservedAt = purchaseDate.AddDays(-3)
            });
        }

        db.DamageItems.Add(new DamageItemEntity
        {
            VehicleId = vehicle.Id,
            Category = DamageCategory.Bodywork,
            Severity = DamageSeverity.Moderate,
            CostMin = Math.Round(demo.RepairExpected * 0.8m, 0),
            CostExpected = demo.RepairExpected,
            CostMax = Math.Round(demo.RepairExpected * 1.35m, 0),
            Description = $"{DemoTag} Daño estimado"
        });

        await db.SaveChangesAsync(ct);

        await analysisService.AnalyzeAndSaveAsync(vehicle.Id, new AnalyzeVehicleRequest
        {
            CurrentAuctionPrice = demo.HammerPrice,
            Transport = 150_000m,
            Detailing = 140_000m,
            OtherFixedCosts = 80_000m,
            EstimatedDaysToSell = demo.DaysToSell ?? 35,
            TotalCapital = 60_000_000m
        }, ct);

        await inventory.RegisterPurchaseAsync(vehicle.Id, new RegisterPurchaseRequest
        {
            HammerPrice = demo.HammerPrice,
            CommissionPaid = Math.Round(demo.HammerPrice * 0.134m, 0),
            PurchaseDate = purchaseDate,
            Note = DemoTag
        }, ct);

        await AddExpenseAsync(vehicle.Id, ExpenseCategory.Repair, demo.RepairActual, purchaseDate.AddDays(6), ct);
        await AddExpenseAsync(vehicle.Id, ExpenseCategory.Transport, 150_000m, purchaseDate.AddDays(1), ct);
        await AddExpenseAsync(vehicle.Id, ExpenseCategory.Detailing, 140_000m, purchaseDate.AddDays(10), ct);
        await AddExpenseAsync(vehicle.Id, ExpenseCategory.Transfer, 26_000m, purchaseDate.AddDays(3), ct);

        await inventory.PublishAsync(vehicle.Id, new PublishListingRequest
        {
            Channel = "Chileautos",
            ListPrice = Math.Round(demo.MarketPrice * 1.02m, 0),
            PublishedAt = purchaseDate.AddDays(12)
        }, ct);

        if (demo is { DaysToSell: { } days, SalePrice: { } salePrice })
        {
            await inventory.RegisterSaleAsync(vehicle.Id, new RegisterSaleRequest
            {
                SalePrice = salePrice,
                SaleCosts = Math.Round(salePrice * 0.018m, 0),
                SaleDate = purchaseDate.AddDays(days),
                BuyerName = $"{DemoTag} Comprador",
                PaymentMethod = "Transferencia"
            }, ct);
        }
    }

    private async Task AddExpenseAsync(
        long vehicleId, ExpenseCategory category, decimal amount, DateTimeOffset date, CancellationToken ct)
    {
        if (amount <= 0m) return;

        await inventory.RegisterExpenseAsync(vehicleId, new RegisterExpenseRequest
        {
            Category = category,
            Amount = amount,
            ExpenseDate = date,
            Description = $"{DemoTag} {category}"
        }, ct);
    }

    private async Task<VehicleModel?> GetOrCreateModelAsync(Make? make, string name, CancellationToken ct)
    {
        if (make is null) return null;

        var model = await db.VehicleModels.FirstOrDefaultAsync(m => m.MakeId == make.Id && m.Name == name, ct);
        if (model is not null) return model;

        model = new VehicleModel { MakeId = make.Id, Name = name };
        db.VehicleModels.Add(model);
        await db.SaveChangesAsync(ct);

        return model;
    }
}
