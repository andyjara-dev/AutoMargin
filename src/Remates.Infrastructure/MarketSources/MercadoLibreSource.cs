using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remates.Domain.Market;

namespace Remates.Infrastructure.MarketSources;

/// <summary>
/// Consulta MercadoLibre por su API oficial.
///
/// Es la vía legítima y estable: no lee HTML, así que no se rompe cuando el sitio cambia de
/// diseño. Requiere registrar una aplicación gratuita, porque desde 2024 los endpoints de
/// búsqueda dejaron de ser públicos y responden 403 sin token.
/// </summary>
public sealed class MercadoLibreSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketSourceOptions> options,
    HostRateLimiter rateLimiter,
    TimeProvider timeProvider,
    ILogger<MercadoLibreSource> logger) : IMarketSource
{
    private const string ApiHost = "api.mercadolibre.com";

    private readonly MarketSourceOptions _options = options.Value;

    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public string Name => "MercadoLibre";

    public bool IsConfigured =>
        _options.MercadoLibre.Enabled
        && !string.IsNullOrWhiteSpace(_options.MercadoLibre.ClientId)
        && !string.IsNullOrWhiteSpace(_options.MercadoLibre.ClientSecret);

    public async Task<MarketSearchOutcome> SearchAsync(MarketSearchQuery query, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return MarketSearchOutcome.Failed(Name,
                "Falta configurar las credenciales. Registra una aplicación gratuita en " +
                "developers.mercadolibre.cl y define MarketSources__MercadoLibre__ClientId y ClientSecret.");
        }

        try
        {
            var token = await GetTokenAsync(ct);
            if (token is null)
                return MarketSearchOutcome.Failed(Name, "No se pudo obtener el token de acceso.");

            await rateLimiter.WaitTurnAsync(ApiHost, TimeSpan.FromSeconds(_options.MinSecondsBetweenRequests), ct);

            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            var url = BuildSearchUrl(query);
            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("MercadoLibre respondió {Status} a {Url}", response.StatusCode, url);
                return MarketSearchOutcome.Failed(Name, $"La API respondió {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var results = ParseResults(payload, query);

            return new MarketSearchOutcome { Source = Name, Results = results };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Falló la consulta a MercadoLibre");
            return MarketSearchOutcome.Failed(Name, "No se pudo consultar la fuente.");
        }
    }

    /// <summary>
    /// Flujo de credenciales de cliente. El token se reutiliza hasta poco antes de expirar,
    /// para no pedir uno nuevo en cada búsqueda.
    /// </summary>
    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (_token is not null && timeProvider.GetUtcNow() < _tokenExpiresAt) return _token;

        await rateLimiter.WaitTurnAsync(ApiHost, TimeSpan.FromSeconds(_options.MinSecondsBetweenRequests), ct);

        var client = CreateClient();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.MercadoLibre.ClientId,
            ["client_secret"] = _options.MercadoLibre.ClientSecret
        });

        using var response = await client.PostAsync("https://api.mercadolibre.com/oauth/token", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("MercadoLibre rechazó las credenciales con {Status}", response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (!payload.TryGetProperty("access_token", out var tokenProperty)) return null;

        _token = tokenProperty.GetString();

        var seconds = payload.TryGetProperty("expires_in", out var expires) ? expires.GetInt32() : 21_600;
        // Un minuto de margen: si el token vence a mitad de la petición, la búsqueda falla entera.
        _tokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(seconds - 60);

        return _token;
    }

    private string BuildSearchUrl(MarketSearchQuery query)
    {
        var ml = _options.MercadoLibre;
        var parameters = new List<string>
        {
            $"category={Uri.EscapeDataString(ml.CategoryId)}",
            $"limit={Math.Clamp(query.Limit, 1, 50)}"
        };

        var text = query.BuildSearchText();
        if (!string.IsNullOrWhiteSpace(text)) parameters.Add($"q={Uri.EscapeDataString(text)}");

        if (query.Year is { } year)
        {
            var from = year - query.YearTolerance;
            var to = year + query.YearTolerance;
            parameters.Add($"VEHICLE_YEAR={from}-{to}");
        }

        return $"https://{ApiHost}/sites/{ml.SiteId}/search?{string.Join("&", parameters)}";
    }

    private static List<MarketSearchResult> ParseResults(JsonElement payload, MarketSearchQuery query)
    {
        var results = new List<MarketSearchResult>();

        if (!payload.TryGetProperty("results", out var items) || items.ValueKind != JsonValueKind.Array)
            return results;

        foreach (var item in items.EnumerateArray())
        {
            var price = item.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDecimal()
                : 0m;

            var attributes = item.TryGetProperty("attributes", out var attrs) ? attrs : default;
            var year = ReadIntAttribute(attributes, "VEHICLE_YEAR");
            var mileage = ReadIntAttribute(attributes, "KILOMETERS");

            var result = new MarketSearchResult
            {
                Source = "MercadoLibre",
                ListedPrice = price,
                Year = year ?? 0,
                MileageKm = mileage,
                Title = item.TryGetProperty("title", out var t) ? t.GetString() : null,
                Url = item.TryGetProperty("permalink", out var u) ? u.GetString() : null,
                Region = ReadNestedString(item, "address", "state_name")
            };

            // Un aviso sin año o sin precio no aporta a la valuación y solo ensucia la muestra.
            if (result.IsUsable) results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Los atributos vienen como lista de pares, no como campos. El kilometraje además
    /// llega con la unidad pegada («80000 km»), así que se extraen solo los dígitos.
    /// </summary>
    private static int? ReadIntAttribute(JsonElement attributes, string id)
    {
        if (attributes.ValueKind != JsonValueKind.Array) return null;

        foreach (var attribute in attributes.EnumerateArray())
        {
            if (!attribute.TryGetProperty("id", out var attributeId)) continue;
            if (attributeId.GetString() != id) continue;

            var raw = attribute.TryGetProperty("value_name", out var value) ? value.GetString() : null;
            if (raw is null) continue;

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var parsed) ? parsed : null;
        }

        return null;
    }

    private static string? ReadNestedString(JsonElement element, string parent, string child)
        => element.TryGetProperty(parent, out var node) && node.TryGetProperty(child, out var value)
            ? value.GetString()
            : null;

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient(nameof(MercadoLibreSource));
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

        return client;
    }
}
