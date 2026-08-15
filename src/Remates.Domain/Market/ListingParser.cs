using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Remates.Domain.Market;

/// <summary>Datos que se lograron reconocer en el texto de un aviso.</summary>
public sealed record ParsedListing
{
    public decimal? Price { get; init; }
    public int? Year { get; init; }
    public int? MileageKm { get; init; }

    public string? Make { get; init; }
    public string? Model { get; init; }

    public string? Transmission { get; init; }
    public string? Fuel { get; init; }
    public string? Region { get; init; }
    public string? Url { get; init; }

    /// <summary>Campos que no se pudieron reconocer y hay que completar a mano.</summary>
    public required IReadOnlyList<string> Missing { get; init; }

    /// <summary>Sirve como comparable si al menos tiene precio y año.</summary>
    public bool IsUsable => Price is > 0 && Year is > 0;
}

/// <summary>
/// Extrae los datos de un vehículo desde el texto de un aviso copiado y pegado.
///
/// Existe porque los portales que más se usan prohíben la lectura automatizada en su
/// robots.txt. Pegar el aviso que uno ya está mirando no es rastrear el sitio, y resuelve
/// igual el trabajo tedioso: transcribir cifras a mano es donde se cometen los errores.
///
/// Es determinístico y sin dependencias: ningún modelo de lenguaje interpreta el texto.
/// </summary>
public static class ListingParser
{
    /// <summary>
    /// En Chile el punto separa los miles y la coma los decimales, al revés que en inglés.
    /// Un precio se escribe 12.400.000 y confundirlo daría 12,4 en vez de doce millones.
    /// </summary>
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("es-CL");

    private static readonly Regex PriceRegex = new(
        @"\$\s*([\d.,]+)|(?:precio|valor)\s*:?\s*\$?\s*([\d.,]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MileageRegex = new(
        @"([\d.,]+)\s*(?:kms?\.?|kil[oó]metros)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>«92 mil km» es tan común en los avisos como «92.000 km».</summary>
    private static readonly Regex MileageInThousandsRegex = new(
        @"(\d{1,3})\s*mil\s*(?:kms?\.?|kil[oó]metros)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex YearRegex = new(@"\b(19[89]\d|20[0-4]\d)\b", RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(@"https?://\S+", RegexOptions.Compiled);

    private static readonly (string Keyword, string Value)[] Transmissions =
    [
        ("automat", "Automatic"), ("automát", "Automatic"), ("at ", "Automatic"),
        ("cvt", "Cvt"),
        ("manual", "Manual"), ("mecánic", "Manual"), ("mecanic", "Manual")
    ];

    private static readonly (string Keyword, string Value)[] Fuels =
    [
        ("bencin", "Gasoline"), ("gasolin", "Gasoline"),
        ("diesel", "Diesel"), ("diésel", "Diesel"), ("petrol", "Diesel"),
        ("híbrid", "Hybrid"), ("hibrid", "Hybrid"),
        ("eléctric", "Electric"), ("electric", "Electric")
    ];

    /// <summary>
    /// Regiones de Chile por la forma en que aparecen escritas en los avisos, no por su nombre
    /// oficial. Se busca sin tildes porque en los portales se escriben de las dos maneras.
    ///
    /// El orden importa: las claves más específicas van primero, o «valparaiso» quedaría
    /// atrapado antes de poder distinguir «viña del mar».
    /// </summary>
    private static readonly (string Keyword, string Value)[] Regions =
    [
        ("metropolitana", "Metropolitana"), ("santiago", "Metropolitana"), (" rm ", "Metropolitana"),
        ("valparaiso", "Valparaíso"), ("viña del mar", "Valparaíso"), ("vina del mar", "Valparaíso"),
        ("quilpue", "Valparaíso"), ("san antonio", "Valparaíso"),
        ("biobio", "Biobío"), ("bio bio", "Biobío"), ("concepcion", "Biobío"), ("talcahuano", "Biobío"),
        ("araucania", "La Araucanía"), ("temuco", "La Araucanía"),
        ("antofagasta", "Antofagasta"), ("calama", "Antofagasta"),
        ("coquimbo", "Coquimbo"), ("la serena", "Coquimbo"),
        ("o'higgins", "O'Higgins"), ("ohiggins", "O'Higgins"), ("rancagua", "O'Higgins"),
        ("maule", "Maule"), ("talca", "Maule"), ("curico", "Maule"),
        ("los lagos", "Los Lagos"), ("puerto montt", "Los Lagos"), ("osorno", "Los Lagos"),
        ("los rios", "Los Ríos"), ("valdivia", "Los Ríos"),
        ("nuble", "Ñuble"), ("chillan", "Ñuble"),
        ("atacama", "Atacama"), ("copiapo", "Atacama"),
        ("tarapaca", "Tarapacá"), ("iquique", "Tarapacá"),
        ("arica", "Arica y Parinacota"),
        ("aysen", "Aysén"), ("coyhaique", "Aysén"),
        ("magallanes", "Magallanes"), ("punta arenas", "Magallanes")
    ];

    /// <param name="text">Texto del aviso, tal como se copió.</param>
    /// <param name="knownMakes">Catálogo de marcas, para reconocer cuál aparece.</param>
    public static ParsedListing Parse(string? text, IReadOnlyCollection<string>? knownMakes = null)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return new ParsedListing { Missing = ["precio", "año", "kilometraje", "marca"] };
        }

        var normalized = text.Replace(' ', ' ');
        var lower = normalized.ToLowerInvariant();

        // Los espacios de los extremos permiten que « rm » case cuando la sigla abre o cierra
        // el aviso, sin confundirla con esas dos letras dentro de otra palabra.
        var folded = $" {RemoveDiacritics(lower)} ";

        var mileage = ParseMileage(normalized);
        var price = ParsePrice(normalized, mileage);
        var year = ParseYear(normalized);
        var (make, model) = ParseMakeAndModel(normalized, knownMakes);

        if (price is null) missing.Add("precio");
        if (year is null) missing.Add("año");
        if (mileage is null) missing.Add("kilometraje");
        if (make is null) missing.Add("marca");

        return new ParsedListing
        {
            Price = price,
            Year = year,
            MileageKm = mileage,
            Make = make,
            Model = model,
            Transmission = MatchKeyword(lower, Transmissions),
            Fuel = MatchKeyword(lower, Fuels),
            Region = MatchKeyword(folded, Regions),
            Url = UrlRegex.Match(normalized) is { Success: true } m ? m.Value.TrimEnd('.', ',', ')') : null,
            Missing = missing
        };
    }

    private static int? ParseMileage(string text)
    {
        // Se prueba primero la forma «92 mil km»: la general no la reconoce, porque entre el
        // número y la unidad hay una palabra.
        var thousands = MileageInThousandsRegex.Match(text);
        if (thousands.Success)
        {
            var inThousands = ParseNumber(thousands.Groups[1].Value) * 1000m;
            return inThousands is > 0 and <= 1_000_000m ? (int)inThousands : null;
        }

        var match = MileageRegex.Match(text);
        if (!match.Success) return null;

        var value = ParseNumber(match.Groups[1].Value);

        // El cero se conserva: «0 km» no es un dato ausente, es un auto sin uso. Descartarlo lo
        // haría pasar por «no informado», y son cosas opuestas a la hora de elegir comparables:
        // un auto nuevo se vende al precio de nuevo y no sirve para valorizar uno de remate.
        //
        // Por encima del millón ya no es kilometraje de un auto usable.
        return value is >= 0 and <= 1_000_000m ? (int)value : null;
    }

    /// <summary>
    /// El precio se reconoce por el símbolo o la palabra. Si no aparecen, se toma el número
    /// más grande, descartando el kilometraje: en un aviso chileno el precio siempre es la
    /// cifra mayor, y confundirlos invertiría por completo la valuación.
    /// </summary>
    private static decimal? ParsePrice(string text, int? mileage)
    {
        foreach (Match match in PriceRegex.Matches(text))
        {
            var raw = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var value = ParseNumber(raw);

            if (value >= 100_000m) return value;
        }

        var candidates = Regex.Matches(text, @"\b[\d.,]{6,}\b")
            .Select(m => ParseNumber(m.Value))
            .Where(v => v >= 500_000m && v <= 500_000_000m)
            .Where(v => mileage is null || (int)v != mileage)
            .ToList();

        return candidates.Count > 0 ? candidates.Max() : null;
    }

    private static int? ParseYear(string text)
    {
        var years = YearRegex.Matches(text)
            .Select(m => int.Parse(m.Value, Culture))
            .Where(y => y >= 1990 && y <= DateTime.UtcNow.Year + 1)
            .ToList();

        // Un aviso puede mencionar varios años (revisión técnica, permiso de circulación).
        // El del vehículo es el más antiguo de los plausibles.
        return years.Count > 0 ? years.Min() : null;
    }

    private static (string? Make, string? Model) ParseMakeAndModel(
        string text, IReadOnlyCollection<string>? knownMakes)
    {
        if (knownMakes is null || knownMakes.Count == 0) return (null, null);

        var lower = text.ToLowerInvariant();

        // La marca más larga primero: «Mercedes-Benz» debe ganarle a una coincidencia parcial.
        var make = knownMakes
            .OrderByDescending(m => m.Length)
            .FirstOrDefault(m => lower.Contains(m.ToLowerInvariant(), StringComparison.Ordinal));

        if (make is null) return (null, null);

        // El modelo es lo que sigue a la marca, hasta el año o el final de la línea.
        var index = lower.IndexOf(make.ToLowerInvariant(), StringComparison.Ordinal);
        var rest = text[(index + make.Length)..].TrimStart(' ', '-', ',');

        var cut = rest.IndexOfAny(['\n', '\r', '|', '$']);
        if (cut > 0) rest = rest[..cut];

        var yearMatch = YearRegex.Match(rest);
        if (yearMatch.Success) rest = rest[..yearMatch.Index];

        var model = rest.Trim(' ', '-', ',', '.');

        return (make, string.IsNullOrWhiteSpace(model) ? null : Shorten(model));
    }

    /// <summary>El modelo rara vez pasa de tres palabras; el resto suele ser equipamiento.</summary>
    private static string Shorten(string value)
    {
        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(3));
    }

    private static decimal ParseNumber(string raw)
    {
        var cleaned = raw.Trim().TrimEnd('.', ',');

        // Se descartan los separadores y se conserva solo el valor entero: los avisos no
        // publican precios con centavos.
        var digits = new string(cleaned.Where(char.IsDigit).ToArray());

        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0m;
    }

    private static string? MatchKeyword(string lower, (string Keyword, string Value)[] options)
        => options.FirstOrDefault(o => lower.Contains(o.Keyword, StringComparison.Ordinal)).Value;

    /// <summary>
    /// Quita las tildes para comparar. En los avisos «Valparaíso» y «Valparaiso» conviven, y
    /// exigir la forma correcta haría que el reconocimiento dependiera de cómo escribe cada
    /// vendedor. Solo afecta a la comparación: lo que se guarda es la forma correcta de la tabla,
    /// por lo que «ñuble» reconocido llega a la base como «Ñuble».
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
