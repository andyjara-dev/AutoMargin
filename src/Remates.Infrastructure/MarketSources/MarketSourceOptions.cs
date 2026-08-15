namespace Remates.Infrastructure.MarketSources;

public sealed class MarketSourceOptions
{
    public const string SectionName = "MarketSources";

    /// <summary>
    /// Identificación honesta ante los sitios que se consultan. Incluye un contacto a
    /// propósito: si alguna consulta molesta, que puedan avisar en vez de bloquear a ciegas.
    /// </summary>
    public string UserAgent { get; set; } =
        "AutoMargin/1.0 (+https://automargin.andyjara.dev; contacto: hola@andyjara.dev)";

    /// <summary>Segundos mínimos entre peticiones a un mismo sitio.</summary>
    public int MinSecondsBetweenRequests { get; set; } = 3;

    /// <summary>Minutos que se conserva una búsqueda en caché, para no repetirla.</summary>
    public int CacheMinutes { get; set; } = 60;

    public int TimeoutSeconds { get; set; } = 15;

    public MercadoLibreOptions MercadoLibre { get; set; } = new();
    public YapoOptions Yapo { get; set; } = new();
}

public sealed class MercadoLibreOptions
{
    /// <summary>
    /// Desactivada por defecto, igual que Yapo: es lectura de HTML y se rompe cuando el sitio
    /// cambia, así que conviene que se encienda a conciencia. No necesita credenciales.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Subdominio de autos. El listado vive en /{marca}/{modelo}/usados/.</summary>
    public string BaseUrl { get; set; } = "https://autos.mercadolibre.cl";
}

public sealed class YapoOptions
{
    /// <summary>
    /// Desactivado por defecto. Su robots.txt permite el acceso, pero es lectura de HTML y
    /// se rompe cuando el sitio cambia: conviene que se encienda a conciencia.
    /// </summary>
    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://www.yapo.cl";
}
