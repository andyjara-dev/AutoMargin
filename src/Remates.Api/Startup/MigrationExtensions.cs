using Microsoft.EntityFrameworkCore;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Startup;

public static class MigrationExtensions
{
    /// <summary>
    /// Aplica las migraciones pendientes y siembra los datos base.
    ///
    /// Si la base no está disponible, se registra y la aplicación arranca igual: el simulador de
    /// análisis no necesita persistencia, y es preferible ofrecerlo a caerse entera. Los endpoints
    /// que sí requieren base fallarán con un error claro y /health lo reportará como degradado.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        if (!app.Configuration.GetValue("Database:AutoMigrate", true))
        {
            logger.LogInformation("Migración automática desactivada por configuración.");
            return;
        }

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RematesDbContext>();

        try
        {
            if (!await db.Database.CanConnectAsync())
            {
                logger.LogWarning(
                    "No hay conexión con PostgreSQL. La API arranca igual, pero solo estarán " +
                    "disponibles los endpoints que no requieren base de datos. " +
                    "Levanta la base con: docker compose up -d postgres");
                return;
            }

            await db.Database.MigrateAsync();
            logger.LogInformation("Migraciones aplicadas.");

            var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
            var email = app.Configuration["Seed:AdminEmail"] ?? "admin@automargin.local";
            var password = app.Configuration["Seed:AdminPassword"];

            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning(
                    "No hay Seed:AdminPassword configurada; se omite la creación del administrador. " +
                    "Definirla en appsettings.Development.json o en Seed__AdminPassword.");
                return;
            }

            await seeder.SeedAsync(email, password);
            logger.LogInformation("Datos base sembrados.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló la migración o el sembrado inicial.");
        }
    }
}
