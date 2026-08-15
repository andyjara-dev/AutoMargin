using System.Globalization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Remates.LogoTracer;

// Vectoriza un PNG del isotipo y genera el SVG con el gradiente de marca.
//
//   dotnet run --project tools/Remates.LogoTracer -- <entrada.png> [salida.svg] [tolerancia] [suavizado]
//
// tolerancia: 0..1, cuánto debe despegarse un píxel del fondo para contar como tinta (0,25 por defecto)
// suavizado:  cuántos píxeles de detalle se descartan al simplificar (1,2 por defecto)

if (args.Contains("--selftest")) return SelfTest.Run();

// El favicon sale del mismo PNG, así que vive con el vectorizador y no en otra herramienta:
//   dotnet run --project tools/Remates.LogoTracer -- --favicon <entrada.png> [salida.ico]
if (args.Contains("--favicon"))
{
    var rest = args.Where(a => a != "--favicon").ToArray();
    var png = rest.ElementAtOrDefault(0);

    if (string.IsNullOrWhiteSpace(png) || !File.Exists(png))
    {
        Console.Error.WriteLine("Uso: dotnet run --project tools/Remates.LogoTracer -- --favicon <entrada.png> [salida.ico]");
        return 1;
    }

    var ico = rest.ElementAtOrDefault(1)
        ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(png))!, "favicon.ico");

    IcoWriter.Write(png, ico);
    Console.WriteLine($"Favicon generado: {ico} ({new FileInfo(ico).Length / 1024d:N1} KB)");

    return 0;
}

var input = args.ElementAtOrDefault(0);

if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
{
    Console.Error.WriteLine("Uso: dotnet run --project tools/Remates.LogoTracer -- <entrada.png> [salida.svg] [tolerancia] [suavizado]");
    Console.Error.WriteLine(input is null ? "Falta la imagen de entrada." : $"No existe el archivo: {input}");
    return 1;
}

var output = args.ElementAtOrDefault(1) ?? Path.ChangeExtension(input, ".svg");
var tolerance = Parse(args.ElementAtOrDefault(2), 0.25);
var smoothing = Parse(args.ElementAtOrDefault(3), 1.2);

using var image = Image.Load<Rgba32>(input);
Console.WriteLine($"Imagen: {image.Width}x{image.Height}");

var mask = Tracer.BuildMask(image, tolerance);

var inkPixels = 0;
for (var y = 0; y < image.Height; y++)
    for (var x = 0; x < image.Width; x++)
        if (mask[x, y]) inkPixels++;

var coverage = (double)inkPixels / (image.Width * image.Height);
Console.WriteLine($"Tinta detectada: {coverage:P1} de la imagen");

if (coverage < 0.005)
{
    Console.Error.WriteLine(
        "Casi no se detectó dibujo. Prueba con una tolerancia menor, por ejemplo 0,12.");
    return 2;
}

if (coverage > 0.85)
{
    Console.Error.WriteLine(
        "Se detectó casi toda la imagen como dibujo. El fondo no se está separando: " +
        "prueba con una tolerancia mayor, por ejemplo 0,45, o exporta el PNG con fondo transparente.");
    return 2;
}

// Descarta manchas sueltas: por debajo de este perímetro son artefactos de compresión, no forma.
var minContour = (int)(Math.Min(image.Width, image.Height) * 0.08);
var contours = Tracer.FindContours(mask, minContour);

Console.WriteLine($"Contornos encontrados: {contours.Count}");

if (contours.Count == 0)
{
    Console.Error.WriteLine("No se encontró ningún contorno utilizable.");
    return 3;
}

// Recorta al dibujo y lo normaliza a un lienzo de 120, para que el SVG sea independiente
// del tamaño con que se exportó el PNG.
double minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
foreach (var point in contours.SelectMany(c => c))
{
    minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
    minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
}

const double canvas = 120d;
const double padding = 4d;
var scale = (canvas - padding * 2) / Math.Max(maxX - minX, maxY - minY);

// Centra el dibujo en el lienzo.
var drawnWidth = (maxX - minX) * scale;
var drawnHeight = (maxY - minY) * scale;
var shiftX = (canvas - drawnWidth) / 2d;
var shiftY = (canvas - drawnHeight) / 2d;

var paths = contours
    .Select(c => Tracer.ToBezierPath(Tracer.Simplify(c, smoothing), scale, minX, minY))
    .Where(p => !string.IsNullOrEmpty(p))
    .ToList();

var combined = string.Join(" ", paths);

// El destino habitual es el frontend: se escriben de una vez las tres variantes que usan
// el mismo trazo, para que no puedan quedar desincronizadas.
// El PNG vive en <frontend>/public, así que el proyecto está un nivel más arriba.
var frontendRoot = Path.GetFullPath(
    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, ".."));

if (Directory.Exists(Path.Combine(frontendRoot, "src", "app", "shared")))
{
    Console.WriteLine("Archivos generados:");
    Emitter.WriteAll(combined, shiftX, shiftY, frontendRoot);
}
else
{
    var svg = $"""
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120" role="img" aria-label="AutoMargin">
          <defs>
            <linearGradient id="am" x1="8%" y1="5%" x2="92%" y2="95%">
              <stop offset="0%" stop-color="#2563EB"/>
              <stop offset="52%" stop-color="#0EA5E9"/>
              <stop offset="100%" stop-color="#22D3EE"/>
            </linearGradient>
          </defs>
          <g transform="translate({F(shiftX)} {F(shiftY)})" fill="url(#am)" fill-rule="evenodd">
            <path d="{combined}"/>
          </g>
        </svg>
        """;

    await File.WriteAllTextAsync(output, svg);
    Console.WriteLine($"SVG generado: {output} ({new FileInfo(output).Length / 1024d:N1} KB)");
}

Console.WriteLine("Si el trazo salió con ruido, sube el suavizado. Si perdió detalle, bájalo.");
return 0;

static double Parse(string? value, double fallback) =>
    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
        ? parsed
        : fallback;

static string F(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);
