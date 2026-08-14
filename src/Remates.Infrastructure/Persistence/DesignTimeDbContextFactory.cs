using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Remates.Infrastructure.Persistence;

/// <summary>
/// Permite a `dotnet ef` construir el contexto sin arrancar la API ni tener la base viva.
/// Generar una migración solo requiere el modelo; aplicarla es lo que necesita PostgreSQL.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<RematesDbContext>
{
    public RematesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=remates;Username=remates;Password=remates_dev_password";

        var options = new DbContextOptionsBuilder<RematesDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new RematesDbContext(options);
    }
}
