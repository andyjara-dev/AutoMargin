using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remates.Domain.Market;

namespace Remates.Infrastructure.MarketSources;

/// <summary>
/// Lee avisos de Yapo desde el HTML de su página de búsqueda.
///
/// Su robots.txt permite el acceso a esa ruta: solo bloquea rutas internas como /ajax/ y
/// /captcha/. Aun así se consulta con moderación, con intervalo entre peticiones, un agente
/// que se identifica y un tope bajo de resultados. La idea es consultar el sitio, no rastrearlo.
///
/// El reconocimiento no depende de nombres de clases CSS, que cambian sin aviso, sino del texto
/// de cada bloque, que se interpreta con el mismo parser probado que usa el pegado manual.
/// Un rediseño del sitio degrada el resultado, pero rara vez lo rompe del todo.
/// </summary>
public sealed class YapoSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketSourceOptions> options,
    HostRateLimiter rateLimiter,
    ILogger<YapoSource> logger) : IMarketSource
{
    private readonly MarketSourceOptions _options = options.Value;

    /// <summary>
    /// Rutas que su robots.txt prohíbe. Se comprueban antes de pedir nada: si alguna vez se
    /// construye una URL que caiga ahí, la petición no debe salir.
    /// </summary>
    private static readonly string[] DisallowedPaths =
        ["/captcha/", "/ajax/", "/ajaxcat/", "/storetoken/", "/cnStatistic/", "/chile-es/", "/chile-en/"];

    public string Name => "Yapo";

    public bool IsConfigured => _options.Yapo.Enabled;

    public string? UnavailableReason => IsConfigured
        ? null
        : "Desactivada. Se enciende con MarketSources__Yapo__Enabled=true (YAPO_ENABLED en el .env).";

    public async Task<MarketSearchOutcome> SearchAsync(MarketSearchQuery query, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            return MarketSearchOutcome.Failed(Name,
                "La fuente está desactivada. Se habilita con MarketSources__Yapo__Enabled=true.");
        }

        var url = BuildSearchUrl(query);

        if (DisallowedPaths.Any(path => url.Contains(path, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Se descartó una consulta a una ruta que Yapo no permite: {Url}", url);
            return MarketSearchOutcome.Failed(Name, "La ruta solicitada no está permitida por el sitio.");
        }

        try
        {
            var host = new Uri(url).Host;
            await rateLimiter.WaitTurnAsync(host, TimeSpan.FromSeconds(_options.MinSecondsBetweenRequests), ct);

            var client = httpClientFactory.CreateClient(nameof(YapoSource));
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);

            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Yapo respondió {Status} a {Url}", response.StatusCode, url);
                return MarketSearchOutcome.Failed(Name, $"El sitio respondió {(int)response.StatusCode}.");
            }

            var html = await response.Content.ReadAsStringAsync(ct);
            var results = await ExtractAsync(html, query, ct);

            return new MarketSearchOutcome { Source = Name, Results = results };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            logger.LogWarning(ex, "Falló la consulta a Yapo");
            return MarketSearchOutcome.Failed(Name, "No se pudo consultar la fuente.");
        }
    }

    private string BuildSearchUrl(MarketSearchQuery query)
    {
        var text = query.BuildSearchText();
        var baseUrl = _options.Yapo.BaseUrl.TrimEnd('/');

        return string.IsNullOrWhiteSpace(text)
            ? $"{baseUrl}/chile/autos"
            : $"{baseUrl}/chile/autos?q={Uri.EscapeDataString(text)}";
    }

    /// <summary>
    /// Busca los bloques que contienen un precio y toma el contenedor más pequeño que además
    /// tenga un año: ese es el aviso. Subir hasta el primer ancestro con ambos datos evita
    /// quedarse con el precio suelto y también tragarse la página entera.
    /// </summary>
    private async Task<List<MarketSearchResult>> ExtractAsync(
        string html, MarketSearchQuery query, CancellationToken ct)
    {
        var parser = new HtmlParser();
        using var document = await parser.ParseDocumentAsync(html, ct);

        var seen = new HashSet<string>();
        var results = new List<MarketSearchResult>();

        foreach (var candidate in FindListingBlocks(document))
        {
            if (results.Count >= query.Limit) break;

            var text = Normalize(candidate.TextContent);
            if (text.Length is < 15 or > 600) continue;

            var parsed = ListingParser.Parse(text);
            if (!parsed.IsUsable) continue;

            var link = candidate.QuerySelector("a[href]")?.GetAttribute("href");
            var url = BuildAbsoluteUrl(link);

            // Sin dirección, se deduplica por la combinación de cifras del aviso.
            var key = url ?? $"{parsed.Price}|{parsed.Year}|{parsed.MileageKm}";
            if (!seen.Add(key)) continue;

            results.Add(new MarketSearchResult
            {
                Source = Name,
                ListedPrice = parsed.Price!.Value,
                Year = parsed.Year!.Value,
                MileageKm = parsed.MileageKm,
                Title = Shorten(text),
                Url = url,
                Region = parsed.Region
            });
        }

        return results;
    }

    private static IEnumerable<IElement> FindListingBlocks(IDocument document)
    {
        var visited = new HashSet<IElement>();

        foreach (var element in document.QuerySelectorAll("body *"))
        {
            // Solo hojas o casi hojas: un contenedor grande mezclaría varios avisos y el
            // parser tomaría el precio de uno con el año de otro.
            if (element.Children.Length > 12) continue;

            var text = element.TextContent;
            if (text.Length is < 15 or > 600) continue;
            if (!text.Contains('$')) continue;

            // Si un ancestro cercano ya se aceptó, este es parte del mismo aviso.
            if (element.Ancestors<IElement>().Take(4).Any(visited.Contains)) continue;

            visited.Add(element);
            yield return element;
        }
    }

    private string? BuildAbsoluteUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;

        return $"{_options.Yapo.BaseUrl.TrimEnd('/')}/{href.TrimStart('/')}";
    }

    private static string Normalize(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Shorten(string text)
        => text.Length <= 90 ? text : text[..90] + "…";
}
