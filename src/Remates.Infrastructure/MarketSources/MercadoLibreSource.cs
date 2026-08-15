using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Remates.Domain.Market;

namespace Remates.Infrastructure.MarketSources;

/// <summary>
/// Lee avisos de MercadoLibre desde el HTML de su listado de autos usados.
///
/// No usa su API oficial a propósito: esa solo permite listar las publicaciones de un vendedor
/// concreto, con seller_id o nickname, y para comparar precios se necesitan avisos de muchos
/// vendedores distintos. Sin ese parámetro responde 403. Está documentado en DESPLIEGUE.md.
///
/// El robots.txt del sitio no prohíbe la ruta de listados por marca y modelo para un cliente
/// genérico. Aun así se consulta con moderación: intervalo entre peticiones, agente que se
/// identifica con un contacto, y un tope bajo de resultados. Se consulta el sitio, no se rastrea.
/// </summary>
public sealed class MercadoLibreSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketSourceOptions> options,
    HostRateLimiter rateLimiter,
    ILogger<MercadoLibreSource> logger) : IMarketSource
{
    private readonly MarketSourceOptions _options = options.Value;

    /// <summary>
    /// Contenedor de cada aviso. Verificado contra el sitio real: 48 avisos por página, 48
    /// precios, 48 kilometrajes. Es una correspondencia de uno a uno, no una aproximación.
    /// </summary>
    private const string ItemSelector = "li.ui-search-layout__item";

    /// <summary>
    /// El precio se lee de su propio elemento en vez de buscarlo en el texto. Hay exactamente
    /// uno por tarjeta, así que no hay riesgo de tomar el valor de una cuota por el precio, que
    /// es el error que invertiría la valuación entera.
    /// </summary>
    private const string PriceSelector = ".andes-money-amount__fraction";

    /// <summary>
    /// Fragmentos que su robots.txt prohíbe y que un nombre de marca o modelo podría producir.
    /// Se comprueban antes de pedir nada: si alguna vez se arma una URL que caiga ahí, la
    /// petición no debe salir.
    /// </summary>
    private static readonly string[] DisallowedFragments =
        ["/pagina/", "_pricerange_", "_kilometers_", "_pricemin_", "_pricemax_", "/adultos/", ".html", "/e/"];

    private static readonly Regex NonSlug = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);

    public string Name => "MercadoLibre";

    public bool IsConfigured => _options.MercadoLibre.Enabled;

    public string? UnavailableReason => IsConfigured
        ? null
        : "Desactivada. Se enciende con MarketSources__MercadoLibre__Enabled=true " +
          "(ML_ENABLED en el .env).";

    public async Task<MarketSearchOutcome> SearchAsync(MarketSearchQuery query, CancellationToken ct)
    {
        if (!IsConfigured) return MarketSearchOutcome.Failed(Name, UnavailableReason!);

        var (url, makeSlug) = BuildSearchUrl(query);

        if (url is null)
        {
            return MarketSearchOutcome.Failed(Name,
                "Hace falta la marca para buscar. Sin ella el sitio devuelve autos cualesquiera, " +
                "que no sirven como comparables.");
        }

        if (DisallowedFragments.Any(f => url.Contains(f, StringComparison.OrdinalIgnoreCase)))
        {
            logger.LogWarning("Se descartó una consulta a una ruta que MercadoLibre no permite: {Url}", url);
            return MarketSearchOutcome.Failed(Name, "La ruta solicitada no está permitida por el sitio.");
        }

        try
        {
            var host = new Uri(url).Host;
            await rateLimiter.WaitTurnAsync(host, TimeSpan.FromSeconds(_options.MinSecondsBetweenRequests), ct);

            var client = httpClientFactory.CreateClient(nameof(MercadoLibreSource));
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(_options.UserAgent);
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es-CL,es;q=0.9");

            using var response = await client.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("MercadoLibre respondió {Status} a {Url}", response.StatusCode, url);

                return MarketSearchOutcome.Failed(Name, response.StatusCode is System.Net.HttpStatusCode.NotFound
                    ? "No existe un listado para esa marca y modelo. Revisa cómo están escritos."
                    : $"El sitio respondió {(int)response.StatusCode}.");
            }

            // Si el sitio redirige a un listado que ya no es el de la marca pedida, la búsqueda se
            // perdió por el camino. Devolver esos avisos sería peor que no devolver nada: entrarían
            // como comparables de un vehículo con el que no tienen relación, y el error no se vería
            // mirando la puja máxima resultante.
            var finalUrl = response.RequestMessage?.RequestUri?.ToString() ?? url;

            if (!finalUrl.Contains(makeSlug!, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "MercadoLibre descartó la búsqueda: se pidió {Url} y se terminó en {FinalUrl}",
                    url, finalUrl);

                return MarketSearchOutcome.Failed(Name,
                    "El sitio ignoró la búsqueda y devolvió otro listado. No se usan esos avisos " +
                    "porque no corresponden al vehículo buscado.");
            }

            var html = await response.Content.ReadAsStringAsync(ct);

            return await ExtractAsync(html, query, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            logger.LogWarning(ex, "Falló la consulta a MercadoLibre");
            return MarketSearchOutcome.Failed(Name, "No se pudo consultar la fuente.");
        }
    }

    /// <summary>
    /// Ruta de listado por marca y modelo. El modelo es opcional; la marca no, porque sin ella
    /// el sitio devuelve el listado general de autos.
    /// </summary>
    private (string? Url, string? MakeSlug) BuildSearchUrl(MarketSearchQuery query)
    {
        var makeSlug = Slug(query.Make);
        if (string.IsNullOrEmpty(makeSlug)) return (null, null);

        var modelSlug = Slug(query.Model);
        var baseUrl = _options.MercadoLibre.BaseUrl.TrimEnd('/');

        var path = string.IsNullOrEmpty(modelSlug)
            ? $"{baseUrl}/{makeSlug}/usados/"
            : $"{baseUrl}/{makeSlug}/{modelSlug}/usados/";

        return (path, makeSlug);
    }

    private static string Slug(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : NonSlug.Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');

    /// <summary>
    /// Cada aviso vive en su propio contenedor, así que no hay que adivinar dónde empieza y
    /// termina. El precio sale de su elemento; el año, el kilometraje y la región se leen del
    /// texto de la tarjeta con el mismo parser probado que usa el pegado manual.
    /// </summary>
    private async Task<MarketSearchOutcome> ExtractAsync(
        string html, MarketSearchQuery query, CancellationToken ct)
    {
        var parser = new HtmlParser();
        using var document = await parser.ParseDocumentAsync(html, ct);

        var items = document.QuerySelectorAll(ItemSelector);

        if (items.Length == 0)
        {
            // Callar aquí sería lo peor: parecería que no hay autos de ese modelo cuando en
            // realidad el sitio cambió y dejamos de entenderlo.
            logger.LogWarning(
                "MercadoLibre devolvió una página sin avisos reconocibles con «{Selector}». " +
                "Probablemente cambió su maquetado.", ItemSelector);

            return MarketSearchOutcome.Failed(Name,
                "No se reconoció ningún aviso en la página. El sitio cambió su formato y hay que " +
                "actualizar la fuente. Mientras tanto, usa el pegado de avisos.");
        }

        var seen = new HashSet<string>();
        var results = new List<MarketSearchResult>();

        foreach (var item in items)
        {
            if (results.Count >= query.Limit) break;

            var text = Normalize(item.TextContent);
            var parsed = ListingParser.Parse(text);

            var price = ReadPrice(item) ?? parsed.Price;
            if (price is null or <= 0 || parsed.Year is null) continue;

            var url = item.QuerySelector("a[href]")?.GetAttribute("href");

            var key = url ?? $"{price}|{parsed.Year}|{parsed.MileageKm}";
            if (!seen.Add(key)) continue;

            results.Add(new MarketSearchResult
            {
                Source = Name,
                ListedPrice = price.Value,
                Year = parsed.Year.Value,
                MileageKm = parsed.MileageKm,
                Title = ReadTitle(item) ?? Shorten(text),
                Url = url,
                Region = parsed.Region
            });
        }

        return new MarketSearchOutcome { Source = Name, Results = results };
    }

    private static decimal? ReadPrice(IElement item)
    {
        var raw = item.QuerySelector(PriceSelector)?.TextContent;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        return decimal.TryParse(digits, out var value) && value > 0 ? value : null;
    }

    private static string? ReadTitle(IElement item)
    {
        // El nombre de clase cambió una vez y volverá a cambiar; si no está, el texto de la
        // tarjeta recortado sirve igual y el aviso no se pierde por un detalle cosmético.
        var title = item.QuerySelector(".poly-component__title")?.TextContent;

        return string.IsNullOrWhiteSpace(title) ? null : Normalize(title);
    }

    private static string Normalize(string text)
        => string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Shorten(string text)
        => text.Length <= 90 ? text : text[..90] + "…";
}
