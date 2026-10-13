using SkiaSharp;

namespace PeluqueriaLucia.Web.Infraestructura.Impresion;

/// <summary>
/// Convierte una imagen (el logo del salón) al comando de imagen raster de ESC/POS («GS v 0») para imprimirla en la
/// térmica junto al ticket. Con SkiaSharp, que funciona igual en Windows y en el Linux de Azure.
/// </summary>
public static class ImagenEscPos
{
    /// <summary>Píxeles con luminancia por debajo de este valor (sobre fondo blanco) se imprimen como punto negro.</summary>
    private const int UmbralNegro = 160;

    public static byte[] ComandoRaster(Stream imagen, int anchoMaximoPuntos)
    {
        using var datos = SKData.Create(imagen);
        using var original = SKBitmap.Decode(datos) ?? throw new InvalidOperationException("No se pudo leer la imagen del logo.");

        var ancho = Math.Min(original.Width, Math.Max(8, anchoMaximoPuntos));
        var escala = (double)ancho / original.Width;
        var alto = Math.Max(1, (int)Math.Round(original.Height * escala));

        // Cada fila ocupa bytes enteros (múltiplos de 8 puntos); el sobrante queda en blanco
        var anchoBytes = (ancho + 7) / 8;
        var anchoPuntos = anchoBytes * 8;

        using var redimensionada = new SKBitmap(anchoPuntos, alto, SKColorType.Rgba8888, SKAlphaType.Premul);
        using (var lienzo = new SKCanvas(redimensionada))
        {
            lienzo.Clear(SKColors.White);
            using var imagenOriginal = SKImage.FromBitmap(original);
            lienzo.DrawImage(imagenOriginal, new SKRect(0, 0, ancho, alto), new SKSamplingOptions(SKCubicResampler.Mitchell));
        }

        var puntos = new byte[anchoBytes * alto];
        for (var y = 0; y < alto; y++)
        {
            for (var x = 0; x < anchoPuntos; x++)
            {
                var pixel = redimensionada.GetPixel(x, y);
                var luminancia = 0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue;
                if (pixel.Alpha > 32 && luminancia < UmbralNegro)
                    puntos[y * anchoBytes + x / 8] |= (byte)(1 << (7 - x % 8));
            }
        }

        var cabecera = new byte[]
        {
            0x1D, 0x76, 0x30, 0x00,
            (byte)(anchoBytes & 0xFF), (byte)((anchoBytes >> 8) & 0xFF),
            (byte)(alto & 0xFF), (byte)((alto >> 8) & 0xFF),
        };
        return [.. cabecera, .. puntos];
    }
}
