using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Remates.LogoTracer;

public readonly record struct Pt(double X, double Y);

/// <summary>
/// Convierte un mapa de bits en contornos vectoriales.
///
/// El proceso es: separar tinta de fondo, seguir el borde de cada región, quitar los puntos
/// que no aportan forma y suavizar el resto en curvas. Trabaja sobre los píxeles reales, así
/// que el resultado sigue el dibujo original y no una interpretación.
/// </summary>
public static class Tracer
{
    /// <summary>
    /// Marca como tinta todo lo que se despegue del fondo.
    ///
    /// Si la imagen tiene transparencia, el canal alfa manda. Si no, se toma el color de las
    /// esquinas como fondo y se mide la distancia de cada píxel a ese color: así funciona
    /// igual con un recorte sobre fondo oscuro que sobre fondo blanco.
    /// </summary>
    public static bool[,] BuildMask(Image<Rgba32> image, double tolerance)
    {
        var width = image.Width;
        var height = image.Height;
        var mask = new bool[width, height];

        var hasAlpha = false;
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height && !hasAlpha; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x].A < 250) { hasAlpha = true; break; }
                }
            }
        });

        Rgba32 background = default;
        if (!hasAlpha)
        {
            // Promedio de las cuatro esquinas: una sola puede caer sobre un borde o un artefacto.
            var corners = new[]
            {
                image[0, 0], image[width - 1, 0],
                image[0, height - 1], image[width - 1, height - 1]
            };

            background = new Rgba32(
                (byte)corners.Average(c => c.R),
                (byte)corners.Average(c => c.G),
                (byte)corners.Average(c => c.B));
        }

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];

                    mask[x, y] = hasAlpha
                        ? p.A > 128
                        : Distance(p, background) > tolerance;
                }
            }
        });

        return mask;
    }

    private static double Distance(Rgba32 a, Rgba32 b)
    {
        double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return Math.Sqrt(dr * dr + dg * dg + db * db) / 441.67; // 441.67 = distancia máxima posible
    }

    /// <summary>
    /// Sigue el borde de cada región de tinta con el algoritmo de Moore, que recorre el
    /// contorno pegado al vecindario de 8 y vuelve al punto de partida.
    /// </summary>
    public static List<List<Pt>> FindContours(bool[,] mask, int minArea)
    {
        var width = mask.GetLength(0);
        var height = mask.GetLength(1);
        var visited = new bool[width, height];
        var contours = new List<List<Pt>>();

        // Vecindario de 8 en orden horario, empezando por el oeste.
        int[] dx = [-1, -1, 0, 1, 1, 1, 0, -1];
        int[] dy = [0, -1, -1, -1, 0, 1, 1, 1];

        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                if (!mask[x, y] || visited[x, y]) continue;

                // Solo arranca en un píxel de borde: si está rodeado, es interior.
                if (mask[x - 1, y] && mask[x + 1, y] && mask[x, y - 1] && mask[x, y + 1]) continue;

                var contour = new List<Pt>();
                int cx = x, cy = y, dir = 0;
                var start = (x, y);
                var steps = 0;
                var maxSteps = width * height * 4;

                do
                {
                    contour.Add(new Pt(cx, cy));
                    visited[cx, cy] = true;

                    var found = false;
                    // Retrocede dos posiciones para no perder esquinas cóncavas.
                    for (var i = 0; i < 8; i++)
                    {
                        var d = (dir + 6 + i) % 8;
                        var nx = cx + dx[d];
                        var ny = cy + dy[d];

                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        if (!mask[nx, ny]) continue;

                        cx = nx; cy = ny; dir = d;
                        found = true;
                        break;
                    }

                    if (!found) break;
                    steps++;
                } while ((cx, cy) != start && steps < maxSteps);

                if (contour.Count >= minArea) contours.Add(contour);
            }
        }

        return contours;
    }

    /// <summary>
    /// Ramer-Douglas-Peucker: descarta los puntos que caen casi sobre la recta entre sus
    /// vecinos. Deja solo los que definen la forma.
    /// </summary>
    public static List<Pt> Simplify(List<Pt> points, double epsilon)
    {
        if (points.Count < 3) return points;

        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        SimplifySegment(points, 0, points.Count - 1, epsilon, keep);

        return points.Where((_, i) => keep[i]).ToList();
    }

    private static void SimplifySegment(List<Pt> points, int first, int last, double epsilon, bool[] keep)
    {
        if (last <= first + 1) return;

        var maxDistance = 0d;
        var index = first;

        for (var i = first + 1; i < last; i++)
        {
            var d = PerpendicularDistance(points[i], points[first], points[last]);
            if (d > maxDistance) { maxDistance = d; index = i; }
        }

        if (maxDistance <= epsilon) return;

        keep[index] = true;
        SimplifySegment(points, first, index, epsilon, keep);
        SimplifySegment(points, index, last, epsilon, keep);
    }

    private static double PerpendicularDistance(Pt p, Pt a, Pt b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);

        if (length < 1e-9) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));

        return Math.Abs(dy * p.X - dx * p.Y + b.X * a.Y - b.Y * a.X) / length;
    }

    /// <summary>
    /// Convierte la poligonal en curvas cúbicas usando Catmull-Rom.
    ///
    /// Sin esto el contorno queda facetado: se notan los segmentos rectos en los bordes
    /// redondeados, que es justo donde un logotipo se ve mal.
    /// </summary>
    public static string ToBezierPath(List<Pt> points, double scale, double offsetX, double offsetY)
    {
        if (points.Count < 3) return string.Empty;

        Pt At(int i)
        {
            var p = points[((i % points.Count) + points.Count) % points.Count];
            return new Pt((p.X - offsetX) * scale, (p.Y - offsetY) * scale);
        }

        var sb = new System.Text.StringBuilder();
        var start = At(0);
        sb.Append($"M {F(start.X)} {F(start.Y)}");

        for (var i = 0; i < points.Count; i++)
        {
            var p0 = At(i - 1);
            var p1 = At(i);
            var p2 = At(i + 1);
            var p3 = At(i + 2);

            // Tensión 1/6: es la equivalencia estándar entre Catmull-Rom y Bézier cúbica.
            var c1 = new Pt(p1.X + (p2.X - p0.X) / 6d, p1.Y + (p2.Y - p0.Y) / 6d);
            var c2 = new Pt(p2.X - (p3.X - p1.X) / 6d, p2.Y - (p3.Y - p1.Y) / 6d);

            sb.Append($" C {F(c1.X)} {F(c1.Y)}, {F(c2.X)} {F(c2.Y)}, {F(p2.X)} {F(p2.Y)}");
        }

        sb.Append(" Z");
        return sb.ToString();
    }

    private static string F(double value) =>
        Math.Round(value, 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
