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

    public string? UnavailableReason
    {
        get
        {
            if (IsConfigured) return null;

            return _options.MercadoLibre.Enabled
                ? "Faltan las credenciales. Definir MarketSources__MercadoLibre__ClientId y ClientSecret."
                : "Desactivada. Su API solo permite buscar publicaciones de un vendedor concreto, " +
                  "no del marketplace completo, así que encenderla solo agregaría un error " +
                  "permanente. Para este portal, usa el pegado de avisos.";
        }
    }

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
                // El código solo no basta para actuar: un 403 puede ser falta de permisos, una
                // categoría inexistente o un endpoint restringido, y MercadoLibre distingue esos
                // casos en el cuerpo. Sin leerlo, el usuario queda con un número y sin salida.
                var detail = await ReadErrorMessageAsync(response, ct);

                logger.LogWarning("MercadoLibre respondió {Status} a {Url}: {Detail}",
                    response.StatusCode, url, detail ?? "(sin detalle)");

                return MarketSearchOutcome.Failed(Name, Explain(response.StatusCode, detail));
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
            logger.LogWarning("MercadoLibre rechazó las credenciales con {Status}: {Detail}",
                response.StatusCode, await ReadErrorMessageAsync(response, ct) ?? "(sin detalle)");

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

    /// <summary>
    /// Traduce el fallo a algo sobre lo que se pueda actuar. «forbidden · forbidden» es
    /// literalmente lo que devuelve la API, y no le dice nada a quien está mirando la pantalla.
    /// </summary>
    private static string Explain(System.Net.HttpStatusCode status, string? detail) => (int)status switch
    {
        // La búsqueda abierta del marketplace no existe en la API. Su documentación solo ofrece
        // /sites/{site}/search acotado a un vendedor, con seller_id o nickname. No hay permiso
        // que activar en el panel de desarrollador, así que conviene decirlo para no mandar a
        // nadie a buscar una casilla que no está.
        403 => "La API de MercadoLibre solo permite buscar publicaciones de un vendedor concreto, " +
               "no del marketplace completo. No es un problema de tus credenciales ni de los " +
               "permisos que marcaste. Para avisos de este portal, usa el pegado de avisos.",

        401 => "Las credenciales fueron rechazadas. Revisa MarketSources__MercadoLibre__ClientId " +
               "y ClientSecret.",

        429 => "Se superó el límite de consultas de la API. Espera unos minutos.",

        _ => detail is null
            ? $"La API respondió {(int)status}."
            : $"La API respondió {(int)status}: {detail}"
    };

    /// <summary>
    /// Saca el motivo del error del cuerpo de la respuesta. MercadoLibre devuelve un JSON con
    /// «message» y «error»; si viniera otra cosa, se muestra el texto recortado antes que nada.
    /// </summary>
    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                using var json = JsonDocument.Parse(body);
                var root = json.RootElement;

                var message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;

                var text = string.Join(" · ", new[] { message, error }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

                if (!string.IsNullOrWhiteSpace(text)) return Trim(text);
            }
            catch (JsonException)
            {
                // No era JSON; sirve igual el texto crudo.
            }

            return Trim(body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static string Trim(string text)
    {
        var single = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return single.Length <= 200 ? single : single[..200] + "…";
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
