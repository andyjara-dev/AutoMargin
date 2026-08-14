using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Remates.Domain.Damage;
using Remates.Domain.Parameters;
using Remates.Infrastructure.Entities;

namespace Remates.Infrastructure.Persistence;

/// <summary>
/// Siembra lo mínimo para que el sistema sea usable: roles, un administrador, el conjunto de
/// parámetros por defecto y una tabla de costos base de reparación.
///
/// Es idempotente: se puede ejecutar en cada arranque sin duplicar nada.
/// </summary>
public sealed class DbSeeder(
    RematesDbContext db,
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    TimeProvider timeProvider,
    ILogger<DbSeeder> logger)
{
    public async Task SeedAsync(string adminEmail, string adminPassword, CancellationToken ct = default)
    {
        await SeedRolesAsync();
        await SeedAdminAsync(adminEmail, adminPassword);
        await SeedParametersAsync(ct);
        await SeedRepairBaselinesAsync(ct);
        await SeedCatalogAsync(ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var (name, description) in AppRoles.All)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;

            var result = await roleManager.CreateAsync(new AppRole { Name = name, Description = description });
            if (!result.Succeeded)
                logger.LogWarning("No se pudo crear el rol {Role}: {Errors}", name,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedAdminAsync(string email, string password)
    {
        if (await userManager.FindByEmailAsync(email) is not null) return;

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = "Administrador",
            IsActive = true,
            CreatedAt = timeProvider.GetUtcNow()
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            logger.LogError("No se pudo crear el administrador: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, AppRoles.Admin);
        logger.LogInformation("Administrador creado: {Email}", email);
    }

    private async Task SeedParametersAsync(CancellationToken ct)
    {
        if (await db.ParameterSets.AnyAsync(ct)) return;

        var set = new ParameterSet
        {
            Name = "Predeterminado Chile",
            IsActive = true,
            ValidFrom = timeProvider.GetUtcNow(),
            Note = "Valores de partida razonables para Chile. Ajustar con datos reales del negocio."
        };

        foreach (var value in ParameterSetMapper.ToValues(AnalysisParameters.Default))
            set.Values.Add(value);

        db.ParameterSets.Add(set);
    }

    /// <summary>
    /// Costos base por categoría y gravedad, en CLP. Son un punto de partida para que el
    /// formulario no arranque vacío, no una tarifa de taller.
    /// </summary>
    private async Task SeedRepairBaselinesAsync(CancellationToken ct)
    {
        if (await db.RepairCostBaselines.AnyAsync(ct)) return;

        var validFrom = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        (DamageCategory Category, DamageSeverity Severity, decimal Min, decimal Expected, decimal Max)[] rows =
        [
            (DamageCategory.Bodywork, DamageSeverity.Minor, 80_000, 120_000, 180_000),
            (DamageCategory.Bodywork, DamageSeverity.Moderate, 250_000, 400_000, 600_000),
            (DamageCategory.Bodywork, DamageSeverity.Severe, 600_000, 950_000, 1_500_000),
            (DamageCategory.Bodywork, DamageSeverity.Critical, 1_500_000, 2_500_000, 4_000_000),

            (DamageCategory.Paint, DamageSeverity.Minor, 60_000, 100_000, 150_000),
            (DamageCategory.Paint, DamageSeverity.Moderate, 180_000, 280_000, 400_000),
            (DamageCategory.Paint, DamageSeverity.Severe, 400_000, 600_000, 900_000),
            (DamageCategory.Paint, DamageSeverity.Critical, 900_000, 1_300_000, 1_900_000),

            (DamageCategory.Mechanical, DamageSeverity.Minor, 100_000, 200_000, 350_000),
            (DamageCategory.Mechanical, DamageSeverity.Moderate, 350_000, 650_000, 1_100_000),
            (DamageCategory.Mechanical, DamageSeverity.Severe, 1_000_000, 1_800_000, 3_200_000),
            (DamageCategory.Mechanical, DamageSeverity.Critical, 2_500_000, 4_500_000, 8_000_000),

            (DamageCategory.Electrical, DamageSeverity.Minor, 50_000, 100_000, 180_000),
            (DamageCategory.Electrical, DamageSeverity.Moderate, 180_000, 320_000, 550_000),
            (DamageCategory.Electrical, DamageSeverity.Severe, 500_000, 900_000, 1_600_000),
            (DamageCategory.Electrical, DamageSeverity.Critical, 1_200_000, 2_000_000, 3_500_000),

            (DamageCategory.Tires, DamageSeverity.Minor, 60_000, 90_000, 130_000),
            (DamageCategory.Tires, DamageSeverity.Moderate, 180_000, 250_000, 340_000),
            (DamageCategory.Tires, DamageSeverity.Severe, 320_000, 450_000, 600_000),
            (DamageCategory.Tires, DamageSeverity.Critical, 500_000, 700_000, 950_000),

            (DamageCategory.Interior, DamageSeverity.Minor, 40_000, 70_000, 110_000),
            (DamageCategory.Interior, DamageSeverity.Moderate, 120_000, 200_000, 320_000),
            (DamageCategory.Interior, DamageSeverity.Severe, 300_000, 500_000, 800_000),
            (DamageCategory.Interior, DamageSeverity.Critical, 700_000, 1_100_000, 1_700_000),

            (DamageCategory.Glass, DamageSeverity.Minor, 40_000, 70_000, 110_000),
            (DamageCategory.Glass, DamageSeverity.Moderate, 120_000, 190_000, 280_000),
            (DamageCategory.Glass, DamageSeverity.Severe, 250_000, 380_000, 550_000),
            (DamageCategory.Glass, DamageSeverity.Critical, 500_000, 750_000, 1_100_000),

            (DamageCategory.Lights, DamageSeverity.Minor, 40_000, 70_000, 110_000),
            (DamageCategory.Lights, DamageSeverity.Moderate, 110_000, 180_000, 280_000),
            (DamageCategory.Lights, DamageSeverity.Severe, 250_000, 400_000, 650_000),
            (DamageCategory.Lights, DamageSeverity.Critical, 600_000, 950_000, 1_500_000),

            (DamageCategory.Suspension, DamageSeverity.Minor, 80_000, 140_000, 220_000),
            (DamageCategory.Suspension, DamageSeverity.Moderate, 250_000, 420_000, 700_000),
            (DamageCategory.Suspension, DamageSeverity.Severe, 600_000, 1_000_000, 1_700_000),
            (DamageCategory.Suspension, DamageSeverity.Critical, 1_400_000, 2_200_000, 3_500_000),

            // El daño estructural es el que más se subestima mirando fotos: rangos deliberadamente anchos.
            (DamageCategory.Structural, DamageSeverity.Minor, 300_000, 600_000, 1_100_000),
            (DamageCategory.Structural, DamageSeverity.Moderate, 900_000, 1_700_000, 3_000_000),
            (DamageCategory.Structural, DamageSeverity.Severe, 2_200_000, 4_000_000, 7_000_000),
            (DamageCategory.Structural, DamageSeverity.Critical, 4_500_000, 8_000_000, 14_000_000),

            (DamageCategory.Airbags, DamageSeverity.Minor, 300_000, 500_000, 800_000),
            (DamageCategory.Airbags, DamageSeverity.Moderate, 700_000, 1_100_000, 1_700_000),
            (DamageCategory.Airbags, DamageSeverity.Severe, 1_300_000, 2_000_000, 3_000_000),
            (DamageCategory.Airbags, DamageSeverity.Critical, 2_200_000, 3_400_000, 5_000_000),

            (DamageCategory.Other, DamageSeverity.Minor, 50_000, 100_000, 180_000),
            (DamageCategory.Other, DamageSeverity.Moderate, 150_000, 300_000, 500_000),
            (DamageCategory.Other, DamageSeverity.Severe, 400_000, 700_000, 1_200_000),
            (DamageCategory.Other, DamageSeverity.Critical, 1_000_000, 1_800_000, 3_000_000)
        ];

        db.RepairCostBaselines.AddRange(rows.Select(r => new RepairCostBaseline
        {
            Category = r.Category,
            Severity = r.Severity,
            CostMin = r.Min,
            CostExpected = r.Expected,
            CostMax = r.Max,
            ValidFrom = validFrom,
            Notes = "Semilla inicial. Reemplazar con costos reales de taller a medida que se acumulen."
        }));
    }

    /// <summary>Marcas frecuentes en remates chilenos, para que el catálogo no arranque vacío.</summary>
    private async Task SeedCatalogAsync(CancellationToken ct)
    {
        if (await db.Makes.AnyAsync(ct)) return;

        string[] makes =
        [
            "Chevrolet", "Hyundai", "Kia", "Nissan", "Suzuki", "Toyota", "Mazda", "Mitsubishi",
            "Peugeot", "Renault", "Ford", "Volkswagen", "Subaru", "Honda", "Citroën", "Jeep",
            "Great Wall", "Changan", "MG", "Chery", "JAC", "BMW", "Mercedes-Benz", "Audi"
        ];

        db.Makes.AddRange(makes.Select(name => new Make { Name = name }));
    }
}
