using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Remates.Infrastructure.Auth;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRematesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.TryAddTimeProvider();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'Postgres'. Definirla en appsettings o en la variable " +
                "de entorno ConnectionStrings__Postgres.");

        services.AddScoped<AuditInterceptor>();

        services.AddDbContext<RematesDbContext>((provider, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Los reintentos cubren cortes transitorios, pero con la espera por defecto una
                // base caída deja al usuario 20 segundos mirando un botón girando. Se acota el
                // retardo para que el fallo se manifieste rápido y con un mensaje claro.
                npgsql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(2),
                    errorCodesToAdd: null);

                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            });

            options.AddInterceptors(provider.GetRequiredService<AuditInterceptor>());
        });

        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = 8;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<RematesDbContext>();
        // Sin AddDefaultTokenProviders: los flujos de recuperación de contraseña y confirmación
        // de correo no existen todavía. Se agrega cuando haga falta, con su paquete.

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<DbSeeder>();

        return services;
    }

    private static IServiceCollection TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);

        return services;
    }
}
