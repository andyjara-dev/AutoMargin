using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Remates.LogoTracer;

/// <summary>
/// Comprueba el trazador contra figuras de geometría conocida, sin depender de una imagen real.
/// Un anillo debe producir dos contornos (el borde exterior y el del agujero); un disco, uno.
/// </summary>
public static class SelfTest
{
    public static int Run()
    {
        var failures = 0;

        failures += Check("disco sobre fondo transparente", Disc(), expectedContours: 1);
        failures += Check("anillo sobre fondo transparente", Ring(), expectedContours: 2);
        failures += Check("anillo sobre fondo oscuro opaco", RingOnDark(), expectedContours: 2);

        Console.WriteLine(failures == 0
            ? "Autoprueba correcta: el trazador detecta contornos exteriores e interiores."
            : $"Autoprueba con {failures} fallo(s).");

        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, Image<Rgba32> image, int expectedContours)
    {
        using (image)
        {
            var mask = Tracer.BuildMask(image, 0.25);
            var contours = Tracer.FindContours(mask, minArea: 40);

            var simplified = contours.Select(c => Tracer.Simplify(c, 1.2)).ToList();
            var path = simplified.Count > 0 ? Tracer.ToBezierPath(simplified[0], 1, 0, 0) : "";

            var ok = contours.Count == expectedContours && path.StartsWith('M') && path.EndsWith('Z');

            Console.WriteLine(
                $"  [{(ok ? "ok" : "FALLA")}] {name}: {contours.Count} contorno(s), " +
                $"esperados {expectedContours}");

            return ok ? 0 : 1;
        }
    }

    private static Image<Rgba32> Disc() => Build((dx, dy) => Math.Sqrt(dx * dx + dy * dy) < 180, opaque: false);

    private static Image<Rgba32> Ring() => Build(
        (dx, dy) => { var r = Math.Sqrt(dx * dx + dy * dy); return r is > 110 and < 190; }, opaque: false);

    private static Image<Rgba32> RingOnDark() => Build(
        (dx, dy) => { var r = Math.Sqrt(dx * dx + dy * dy); return r is > 110 and < 190; }, opaque: true);

    private static Image<Rgba32> Build(Func<double, double, bool> isInk, bool opaque)
    {
        const int size = 500;
        var image = new Image<Rgba32>(size, size);

        var background = opaque ? new Rgba32(11, 15, 25, 255) : new Rgba32(0, 0, 0, 0);
        var ink = new Rgba32(14, 165, 233, 255);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                image[x, y] = isInk(x - size / 2d, y - size / 2d) ? ink : background;
            }
        }

        return image;
    }
}
