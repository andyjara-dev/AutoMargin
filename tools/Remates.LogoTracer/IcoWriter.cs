using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Remates.LogoTracer;

/// <summary>
/// Arma el favicon.ico desde el PNG del isotipo.
///
/// Existe porque el navegador pide <c>/favicon.ico</c> aunque el HTML declare un SVG, y si ahí
/// sigue el archivo que trae Angular por defecto, en la pestaña aparece el logo de Angular.
///
/// El .ico se escribe a mano: la versión de ImageSharp que usamos no trae codificador de iconos,
/// pero el formato admite cargas PNG desde Windows Vista y eso es un contenedor sencillo —
/// una cabecera, una entrada por tamaño y los PNG uno detrás de otro.
/// </summary>
public static class IcoWriter
{
    /// <summary>
    /// Tamaños que se incluyen. El 16 es el de la pestaña y es el que más se mira; los grandes
    /// los usan la barra de marcadores, el escritorio y la pantalla de inicio del teléfono.
    /// </summary>
    private static readonly int[] Sizes = [16, 32, 48, 64, 128, 256];

    public static void Write(string sourcePng, string outputIco)
    {
        using var source = Image.Load<Rgba32>(sourcePng);

        var bounds = FindInkBounds(source);
        if (bounds.IsEmpty)
            throw new InvalidOperationException("La imagen está completamente transparente.");

        using var glyph = CropToSquare(source, bounds);

        var frames = Sizes.Select(size => RenderPng(glyph, size)).ToList();

        using var file = File.Create(outputIco);
        using var writer = new BinaryWriter(file);

        writer.Write((ushort)0);              // reservado
        writer.Write((ushort)1);              // 1 = icono
        writer.Write((ushort)frames.Count);

        // Los datos empiezan después de la cabecera y de todas las entradas del directorio.
        var offset = 6 + frames.Count * 16;

        for (var i = 0; i < frames.Count; i++)
        {
            var size = Sizes[i];

            // 256 se codifica como 0: el campo es de un byte y no le cabe.
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);            // colores de la paleta: ninguna, es color directo
            writer.Write((byte)0);            // reservado
            writer.Write((ushort)1);          // planos
            writer.Write((ushort)32);         // bits por píxel
            writer.Write(frames[i].Length);
            writer.Write(offset);

            offset += frames[i].Length;
        }

        foreach (var frame in frames) writer.Write(frame);
    }

    /// <summary>
    /// Recuadro de lo dibujado. Sin recortar, el isotipo queda diminuto dentro de su margen
    /// transparente y a 16 píxeles no se distingue nada.
    /// </summary>
    private static Rectangle FindInkBounds(Image<Rgba32> image)
    {
        int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);

                for (var x = 0; x < row.Length; x++)
                {
                    // Un umbral bajo y no cero: los bordes suavizados dejan un halo casi
                    // invisible que, tomado como dibujo, agranda el recuadro sin motivo.
                    if (row[x].A <= 12) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        });

        return maxX < 0 ? Rectangle.Empty : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>Recorta al dibujo y lo centra en un lienzo cuadrado, para que no se deforme.</summary>
    private static Image<Rgba32> CropToSquare(Image<Rgba32> source, Rectangle bounds)
    {
        using var cropped = source.Clone(ctx => ctx.Crop(bounds));

        // Un poco de aire alrededor: pegado al borde el icono se ve apretado entre las pestañas.
        var side = (int)(Math.Max(cropped.Width, cropped.Height) * 1.08);
        var canvas = new Image<Rgba32>(side, side);

        canvas.Mutate(ctx => ctx.DrawImage(
            cropped,
            new Point((side - cropped.Width) / 2, (side - cropped.Height) / 2),
            1f));

        return canvas;
    }

    private static byte[] RenderPng(Image<Rgba32> glyph, int size)
    {
        using var resized = glyph.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(size, size),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3
        }));

        using var buffer = new MemoryStream();
        resized.Save(buffer, new PngEncoder { ColorType = PngColorType.RgbWithAlpha });

        return buffer.ToArray();
    }
}
