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
    /// Apagada por defecto. Su endpoint de búsqueda responde 403 a las aplicaciones: dejó de
    /// abrirse para consultar avisos de terceros, que es justamente para lo que servía aquí.
    /// El adaptador se conserva porque funciona y no cuesta nada apagado, por si el acceso
    /// vuelve a otorgarse; encenderlo hoy solo agrega un error permanente a la pantalla.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Credenciales de la aplicación registrada en developers.mercadolibre.cl.</summary>
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>MLC es el sitio de Chile.</summary>
    public string SiteId { get; set; } = "MLC";

    /// <summary>Categoría de autos y camionetas.</summary>
    public string CategoryId { get; set; } = "MLC1744";
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
