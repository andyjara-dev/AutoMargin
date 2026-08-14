using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Remates.Infrastructure.Auth;

namespace Remates.Api.Auth;

public static class JwtAuthExtensions
{
    public static IServiceCollection AddRematesJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Falta Jwt:SigningKey, o tiene menos de 32 caracteres. Nunca debe versionarse. " +
                "En desarrollo se define con user-secrets:\n" +
                "  dotnet user-secrets set \"Jwt:SigningKey\" \"<48+ caracteres aleatorios>\" --project src/Remates.Api\n" +
                "En producción, por variable de entorno Jwt__SigningKey. " +
                "Ver la sección de puesta en marcha del README.");
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorization();

        return services;
    }
}
