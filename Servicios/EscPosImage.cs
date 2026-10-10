#pragma warning disable CA1416 // Solo se ejecuta en la impresora local de Windows del salón, igual que RawPrinterHelper.

using System.Drawing;
using System.Drawing.Drawing2D;

namespace PeluqueriaLuciaEspina.Services
{
    // Convierte una imagen (el logotipo del salón) al comando de imagen raster de
    // ESC/POS ("GS v 0"), para poder imprimirla en la impresora térmica junto al ticket.
    public static class EscPosImage
    {
        public static byte[] BuildRasterCommand(string filePath, int maxWidthDots)
        {
            using var original = new Bitmap(filePath);

            var width = Math.Min(original.Width, maxWidthDots);
            var scale = (double)width / original.Width;
            var height = Math.Max(1, (int)Math.Round(original.Height * scale));

            // El ancho en bytes debe cubrir múltiplos de 8 dots; el sobrante queda en blanco.
            var widthBytes = (width + 7) / 8;
            var widthDots = widthBytes * 8;

            using var resized = new Bitmap(widthDots, height);
            using (var g = Graphics.FromImage(resized))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(original, 0, 0, width, height);
            }

            var pixelData = new byte[widthBytes * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < widthDots; x++)
                {
                    var pixel = resized.GetPixel(x, y);
                    // Umbral de luminancia sobre fondo blanco: los píxeles oscuros u
                    // opacos se imprimen como punto negro; el resto queda en blanco.
                    var luminancia = 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;
                    var esNegro = pixel.A > 32 && luminancia < 160;

                    if (esNegro)
                    {
                        var byteIndex = y * widthBytes + (x / 8);
                        var bit = 7 - (x % 8);
                        pixelData[byteIndex] |= (byte)(1 << bit);
                    }
                }
            }

            var xL = (byte)(widthBytes & 0xFF);
            var xH = (byte)((widthBytes >> 8) & 0xFF);
            var yL = (byte)(height & 0xFF);
            var yH = (byte)((height >> 8) & 0xFF);

            var header = new byte[] { 0x1D, 0x76, 0x30, 0x00, xL, xH, yL, yH };
            var result = new byte[header.Length + pixelData.Length];
            Buffer.BlockCopy(header, 0, result, 0, header.Length);
            Buffer.BlockCopy(pixelData, 0, result, header.Length, pixelData.Length);
            return result;
        }
    }
}
