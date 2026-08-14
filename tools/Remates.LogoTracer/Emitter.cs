using System.Text;

namespace Remates.LogoTracer;

/// <summary>
/// Escribe las tres variantes del isotipo a partir de un mismo trazo.
///
/// El logo vive en tres sitios (archivo suelto, favicon y componente Angular) y mantenerlos
/// a mano garantiza que tarde o temprano queden distintos.
/// </summary>
public static class Emitter
{
    private const string Gradient = """
            <stop offset="0%" stop-color="#2563EB"/>
            <stop offset="52%" stop-color="#0EA5E9"/>
            <stop offset="100%" stop-color="#22D3EE"/>
        """;

    public static void WriteAll(string path, double offsetX, double offsetY, string frontendRoot)
    {
        var publicDir = Path.Combine(frontendRoot, "public");
        var sharedDir = Path.Combine(frontendRoot, "src", "app", "shared");

        Directory.CreateDirectory(publicDir);
        Directory.CreateDirectory(sharedDir);

        WriteLogo(Path.Combine(publicDir, "logo.svg"), path, offsetX, offsetY);
        WriteFavicon(Path.Combine(publicDir, "favicon.svg"), path, offsetX, offsetY);
        WritePathModule(Path.Combine(sharedDir, "logo-path.ts"), path, offsetX, offsetY);
    }

    private static void WriteLogo(string file, string path, double x, double y)
    {
        var svg = new StringBuilder()
            .AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" role="img" aria-label="AutoMargin">""")
            .AppendLine("  <defs>")
            .AppendLine("""    <linearGradient id="am" x1="8%" y1="5%" x2="92%" y2="95%">""")
            .AppendLine(Gradient)
            .AppendLine("    </linearGradient>")
            .AppendLine("  </defs>")
            .AppendLine($"""  <g transform="translate({N(x)} {N(y)})" fill="url(#am)" fill-rule="evenodd">""")
            .AppendLine($"""    <path d="{path}"/>""")
            .AppendLine("  </g>")
            .AppendLine("</svg>")
            .ToString();

        File.WriteAllText(file, svg);
        Report(file);
    }

    /// <summary>
    /// El favicon lleva fondo propio: el isotipo es un trazo fino y sobre una pestaña clara
    /// desaparecería. Se encoge un poco para dejar aire dentro del cuadrado redondeado.
    /// </summary>
    private static void WriteFavicon(string file, string path, double x, double y)
    {
        const double shrink = 0.84;
        var inset = (120 - 120 * shrink) / 2d / shrink;

        var svg = new StringBuilder()
            .AppendLine("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" role="img" aria-label="AutoMargin">""")
            .AppendLine("  <defs>")
            .AppendLine("""    <linearGradient id="amf" x1="8%" y1="5%" x2="92%" y2="95%">""")
            .AppendLine(Gradient)
            .AppendLine("    </linearGradient>")
            .AppendLine("  </defs>")
            .AppendLine("""  <rect width="120" height="120" rx="26" fill="#0B0F19"/>""")
            .AppendLine($"""  <g transform="scale({N(shrink)}) translate({N(x + inset)} {N(y + inset)})" fill="url(#amf)" fill-rule="evenodd">""")
            .AppendLine($"""    <path d="{path}"/>""")
            .AppendLine("  </g>")
            .AppendLine("</svg>")
            .ToString();

        File.WriteAllText(file, svg);
        Report(file);
    }

    private static void WritePathModule(string file, string path, double x, double y)
    {
        var ts = new StringBuilder()
            .AppendLine("/**")
            .AppendLine(" * Trazo del isotipo de AutoMargin.")
            .AppendLine(" *")
            .AppendLine(" * Generado desde public/logo-source.png con tools/Remates.LogoTracer.")
            .AppendLine(" * No editar a mano: volver a ejecutar el trazador regenera este archivo,")
            .AppendLine(" * el logo.svg y el favicon a partir de la misma fuente.")
            .AppendLine(" */")
            .AppendLine($"export const LOGO_PATH = '{path}';")
            .AppendLine()
            .AppendLine($"export const LOGO_OFFSET = {{ x: {N(x)}, y: {N(y)} }};")
            .ToString();

        File.WriteAllText(file, ts);
        Report(file);
    }

    private static void Report(string file) =>
        Console.WriteLine($"  {Path.GetFileName(file),-16} {new FileInfo(file).Length / 1024d,6:N1} KB");

    private static string N(double value) =>
        Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
