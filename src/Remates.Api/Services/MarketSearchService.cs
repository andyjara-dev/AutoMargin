using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Remates.Domain.Market;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.MarketSources;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

public sealed record MarketSearchResponse(
    IReadOnlyList<MarketSearchResult> Results,
    IReadOnlyList<SourceStatus> Sources,
    bool FromCache);

public sealed record SourceStatus(string Name, bool Configured, int Results, string? Problem);

/// <summary>
/// Consulta todas las fuentes configuradas y reúne los resultados.
///
/// Una fuente caída no puede dejar la búsqueda sin respuesta: cada una se consulta por separado
/// y su problema se informa aparte, para que el usuario sepa qué falta en vez de creer que no
/// hay avisos.
/// </summary>
public sealed class MarketSearchService(
    IEnumerable<IMarketSource> sources,
    RematesDbContext db,
    IMemoryCache cache,
    IOptions<MarketSourceOptions> options,
    TimeProvider timeProvider,
    ILogger<MarketSearchService> logger)
{
    private readonly MarketSourceOptions _options = options.Value;

    public async Task<MarketSearchResponse> SearchAsync(MarketSearchQuery query, CancellationToken ct)
    {
        var key = BuildCacheKey(query);

        // La caché no es rendimiento: evita repetir la misma consulta a un sitio ajeno cuando
        // alguien recarga la pantalla o afina un filtro varias veces seguidas.
        if (cache.TryGetValue(key, out MarketSearchResponse? cached) && cached is not null)
            return cached with { FromCache = true };

        var statuses = new List<SourceStatus>();
        var results = new List<MarketSearchResult>();

        foreach (var source in sources)
        {
            if (!source.IsConfigured)
            {
                statuses.Add(new SourceStatus(source.Name, false, 0,
                    "No configurada. Ver la sección de fuentes de mercado en el despliegue."));
                continue;
            }

            try
            {
                var outcome = await source.SearchAsync(query, ct);

                statuses.Add(new SourceStatus(
                    source.Name, true, outcome.Results.Count, outcome.Problem));

                results.AddRange(outcome.Results.Where(r => r.IsUsable));
            }
            catch (Exception ex)
            {
                // Que una fuente falle de forma inesperada no debe tumbar la búsqueda entera.
                logger.LogError(ex, "La fuente {Source} lanzó una excepción no controlada", source.Name);
                statuses.Add(new SourceStatus(source.Name, true, 0, "Error inesperado al consultar."));
            }
        }

        var response = new MarketSearchResponse(
            Deduplicate(results, query),
            statuses,
            FromCache: false);

        cache.Set(key, response, TimeSpan.FromMinutes(_options.CacheMinutes));

        return response;
    }

    /// <summary>
    /// Quita repetidos y ordena por cercanía al vehículo objetivo: los avisos del mismo año y
    /// kilometraje parecido son los que mejor sostienen la valuación.
    /// </summary>
    private static List<MarketSearchResult> Deduplicate(
        List<MarketSearchResult> results, MarketSearchQuery query)
    {
        return results
            .GroupBy(r => r.Url ?? $"{r.Source}|{r.ListedPrice}|{r.Year}|{r.MileageKm}")
            .Select(g => g.First())
            .OrderBy(r => query.Year is { } year ? Math.Abs(r.Year - year) : 0)
            .ThenBy(r => r.MileageKm ?? int.MaxValue)
            .Take(query.Limit)
            .ToList();
    }

    /// <summary>Importa los avisos elegidos como comparables del vehículo.</summary>
    public async Task<int> ImportAsync(
        long vehicleId, IReadOnlyList<MarketSearchResult> selected, CancellationToken ct)
    {
        if (!await db.Vehicles.AnyAsync(v => v.Id == vehicleId, ct))
            throw new KeyNotFoundException($"No existe el vehículo {vehicleId}.");

        var existing = await db.MarketComparables
            .Where(c => c.VehicleId == vehicleId)
            .Select(c => new { c.Url, c.ListedPrice, c.Year, c.MileageKm })
            .ToListAsync(ct);

        var known = existing
            .Select(c => Fingerprint(c.Url, c.ListedPrice, c.Year, c.MileageKm))
            .ToHashSet();

        var now = timeProvider.GetUtcNow();
        var imported = 0;

        foreach (var result in selected.Where(r => r.IsUsable))
        {
            // No duplicar un aviso ya cargado: inflaría la muestra y sesgaría la mediana.
            // El aviso pegado a mano no siempre trae dirección, así que cuando falta se lo
            // identifica por sus cifras: dos avisos del mismo precio, año y kilometraje son
            // el mismo auto o son indistinguibles para la valuación, y da igual cuál sea.
            // El cero es el mismo valor que se guarda cuando el aviso no declara kilometraje,
            // así que la huella debe calcularse igual que la fila que se va a insertar.
            if (!known.Add(Fingerprint(result.Url, result.ListedPrice, result.Year, result.MileageKm ?? 0)))
                continue;

            db.MarketComparables.Add(new MarketComparableEntity
            {
                VehicleId = vehicleId,
                ListedPrice = result.ListedPrice,
                Year = result.Year,
                MileageKm = result.MileageKm ?? 0,
                Source = result.Source,
                Url = result.Url,
                Region = result.Region,
                ObservedAt = result.PublishedAt ?? now
            });

            imported++;
        }

        if (imported > 0) await db.SaveChangesAsync(ct);

        return imported;
    }

    /// <summary>
    /// Identidad de un aviso: su dirección si la tiene, y si no, sus cifras.
    ///
    /// El precio se formatea con escala fija a propósito. La columna es numeric(14,2), así que
    /// lo que vuelve de la base trae los decimales («8950000.00») y lo que llega del navegador
    /// no («8950000»). Comparados como texto crudo serían distintos, y el aviso repetido
    /// entraría dos veces.
    /// </summary>
    private static string Fingerprint(string? url, decimal price, int year, int mileageKm)
        => string.IsNullOrWhiteSpace(url)
            ? $"#{price.ToString("F2", CultureInfo.InvariantCulture)}|{year}|{mileageKm}"
            : url;

    private static string BuildCacheKey(MarketSearchQuery query)
        => $"market:{query.Make}|{query.Model}|{query.Year}|{query.YearTolerance}|{query.Region}|{query.Limit}|{query.FreeText}";
}
