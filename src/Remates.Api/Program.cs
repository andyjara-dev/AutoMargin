using System.Text.Json.Serialization;

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
app.UseAuthorization();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Expuesto para las pruebas de integración con WebApplicationFactory.</summary>
public partial class Program;
