namespace Remates.Domain.Market;

/// <summary>Qué buscar en una fuente de mercado.</summary>
public sealed record MarketSearchQuery
{
    public string? Make { get; init; }
    public string? Model { get; init; }

    /// <summary>Año del vehículo objetivo. La búsqueda abre una ventana alrededor.</summary>
    public int? Year { get; init; }

    /// <summary>Años de tolerancia hacia arriba y abajo.</summary>
    public int YearTolerance { get; init; } = 2;

    public string? Region { get; init; }

    /// <summary>Tope de resultados. Se mantiene bajo a propósito: no se rastrea, se consulta.</summary>
    public int Limit { get; init; } = 25;

    /// <summary>Texto libre, para cuando la marca y el modelo no bastan.</summary>
    public string? FreeText { get; init; }

    public string BuildSearchText()
    {
        var parts = new[] { Make, Model }.Where(p => !string.IsNullOrWhiteSpace(p));
        var text = string.Join(" ", parts);

        return string.IsNullOrWhiteSpace(text) ? FreeText ?? string.Empty : text;
    }
}

/// <summary>Un aviso encontrado, ya normalizado al formato del sistema.</summary>
public sealed record MarketSearchResult
{
    public required string Source { get; init; }
    public required decimal ListedPrice { get; init; }
    public required int Year { get; init; }

    public int? MileageKm { get; init; }
    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? Region { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Sin año ni precio no aporta nada a la valuación.</summary>
    public bool IsUsable => ListedPrice > 0 && Year > 1900;
}

public sealed record MarketSearchOutcome
{
    public required string Source { get; init; }
    public required IReadOnlyList<MarketSearchResult> Results { get; init; }

    /// <summary>Motivo por el que la fuente no devolvió nada. Nulo si todo fue bien.</summary>
    public string? Problem { get; init; }

    public bool Succeeded => Problem is null;

    public static MarketSearchOutcome Failed(string source, string problem) =>
        new() { Source = source, Results = [], Problem = problem };
}

/// <summary>
/// Una fuente de la que se pueden obtener avisos comparables.
///
/// Cada fuente es un adaptador independiente: si una cambia su formato o deja de estar
/// disponible, las demás siguen funcionando y el sistema sigue sirviendo con carga manual.
/// </summary>
public interface IMarketSource
{
    /// <summary>Nombre con el que se guarda el comparable, para saber de dónde salió.</summary>
    string Name { get; }

    /// <summary>False cuando falta configuración, como las credenciales de una API.</summary>
    bool IsConfigured { get; }

    Task<MarketSearchOutcome> SearchAsync(MarketSearchQuery query, CancellationToken ct);
}
