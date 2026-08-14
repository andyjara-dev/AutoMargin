using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Remates.Api.Auth;
using Remates.Api.Services;
using Remates.Api.Startup;
using Remates.Infrastructure;
using Remates.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

const string AngularCorsPolicy = "angular-dev";

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Los enums viajan como texto: el contrato es legible y no se rompe si se reordena un enum.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "AutoMargin API",
        Version = "v1",
        Description = "Gestión y análisis de compra/reventa de vehículos adquiridos en remates. " +
                      "Todos los cálculos financieros son determinísticos: ningún modelo de lenguaje participa en ellos."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pegar solo el token, sin el prefijo 'Bearer'."
    });

    // Microsoft.OpenApi 2.x referencia los esquemas por tipo dedicado, ya no por la propiedad
    // Reference, y Swashbuckle 10 recibe el requisito como función del documento.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "Remates.Api.xml");
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy => policy
        .WithOrigins(
            builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:4200"])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddRematesInfrastructure(builder.Configuration);
builder.Services.AddRematesJwtAuth(builder.Configuration);

builder.Services.AddScoped<ParameterProvider>();
builder.Services.AddScoped<VehicleAnalysisService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<DashboardService>();

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AutoMargin API v1");
        options.RoutePrefix = "swagger";
    });
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors(AngularCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// Estado de la API y de la base, útil para saber si el contenedor de Postgres está arriba.
// Se acota el tiempo a propósito: la estrategia de reintentos de EF es correcta para una consulta
// de negocio, pero en un health check convierte una base caída en 20 segundos de espera, y
// cualquier balanceador daría la aplicación por muerta antes de recibir la respuesta.
app.MapGet("/health", async (RematesDbContext db, CancellationToken ct) =>
{
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));

    try
    {
        return await db.Database.CanConnectAsync(timeout.Token)
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
    }
    catch (Exception ex) when (ex is OperationCanceledException or Npgsql.NpgsqlException)
    {
        return Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
    }
}).ExcludeFromDescription();

await app.MigrateAndSeedAsync();

app.Run();

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
