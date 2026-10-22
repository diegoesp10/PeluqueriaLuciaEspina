using PeluqueriaLucia.Application.Comun;
using SkiaSharp;

namespace PeluqueriaLucia.Infrastructure.Almacenamiento;

/// <summary>
/// Fotos subidas desde el móvil o el ordenador: comprueba que sean JPG, PNG o WebP de verdad (no por la extensión),
/// las endereza según la orientación de la cámara, las reduce y las vuelve a codificar, así que no conservan metadatos
/// (GPS, modelo del móvil…) ni nada que no sea la imagen.
/// </summary>
internal static class NormalizadorFotos
{
    public const int LadoMaximo = 1600;

    /// <summary>La foto derecha, sobre fondo blanco y como mucho de <paramref name="ladoMaximo"/> píxeles por lado. Quien la recibe la libera.</summary>
    public static async Task<Resultado<SKBitmap>> NormalizarAsync(Stream contenido, long bytesMaximos, string campo, CancellationToken ct, int ladoMaximo = LadoMaximo)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[81920]; int cantidad;
        while ((cantidad = await contenido.ReadAsync(buffer, ct)) > 0) {
            if (memoria.Length + cantidad > bytesMaximos) return new Error($"La foto no puede superar {bytesMaximos / (1024 * 1024)} MB.", campo);
            await memoria.WriteAsync(buffer.AsMemory(0, cantidad), ct);
        }
        using var datos = SKData.CreateCopy(memoria.ToArray());
        using var codec = SKCodec.Create(datos);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            codec.Info.Width is < 32 or > 8000 || codec.Info.Height is < 32 or > 8000 || (long)codec.Info.Width * codec.Info.Height > 20000000 || codec.FrameCount > 1)
            return new Error("Sube una foto JPG, PNG o WebP válida, de hasta 20 megapíxeles.", campo);
        using var original = new SKBitmap(codec.Info);
        if (codec.GetPixels(original.Info, original.GetPixels()) != SKCodecResult.Success) return new Error("La imagen está dañada.", campo);
        var rotada = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var w = original.Width; var h = original.Height;
        var ancho = rotada ? h : w; var alto = rotada ? w : h;
        var escala = Math.Min(1f, (float)ladoMaximo / Math.Max(ancho, alto));
        var normal = new SKBitmap(Math.Max(1, (int)(ancho * escala)), Math.Max(1, (int)(alto * escala)));
        using (var lienzo = new SKCanvas(normal)) {
            lienzo.Clear(SKColors.White); lienzo.Scale(escala);
            switch (codec.EncodedOrigin) {
                case SKEncodedOrigin.TopRight: lienzo.Translate(w, 0); lienzo.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: lienzo.Translate(w, h); lienzo.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: lienzo.Translate(0, h); lienzo.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: lienzo.RotateDegrees(90); lienzo.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: lienzo.Translate(h, 0); lienzo.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: lienzo.Translate(h, w); lienzo.RotateDegrees(90); lienzo.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: lienzo.Translate(0, w); lienzo.RotateDegrees(270); break;
            }
            lienzo.DrawBitmap(original, 0, 0, new SKSamplingOptions(SKFilterMode.Linear));
        }
        return Resultado.Ok(normal);
    }

    /// <summary>Copia reducida para miniaturas (si ya es pequeña, del mismo tamaño). Quien la recibe la libera.</summary>
    public static SKBitmap Reducir(SKBitmap imagen, int ladoMaximo)
    {
        var escala = Math.Min(1f, (float)ladoMaximo / Math.Max(imagen.Width, imagen.Height));
        var info = new SKImageInfo(Math.Max(1, (int)(imagen.Width * escala)), Math.Max(1, (int)(imagen.Height * escala)));
        return imagen.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
    }

    public static byte[] Jpeg(SKBitmap imagen, int calidad = 85)
    {
        using var datos = SKImage.FromBitmap(imagen);
        using var codificada = datos.Encode(SKEncodedImageFormat.Jpeg, calidad);
        return codificada.ToArray();
    }
}
