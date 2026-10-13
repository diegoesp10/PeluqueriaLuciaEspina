using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using SkiaSharp;

namespace PeluqueriaLucia.Infrastructure.Almacenamiento;

public sealed class OpcionesFotosResenas { public string Carpeta { get; set; } = ""; }
internal sealed class FotosResenas(IOptions<OpcionesFotosResenas> opciones) : IFotosResenas
{
    public async Task<Resultado<string>> GuardarAsync(Stream contenido, CancellationToken ct)
    {
        using var memoria = new MemoryStream();
        var buffer = new byte[81920]; int cantidad;
        while ((cantidad = await contenido.ReadAsync(buffer, ct)) > 0) {
            if (memoria.Length + cantidad > 5 * 1024 * 1024) return new Error("La foto no puede superar 5 MB.", "Foto");
            await memoria.WriteAsync(buffer.AsMemory(0, cantidad), ct);
        }
        using var datos = SKData.CreateCopy(memoria.ToArray());
        using var codec = SKCodec.Create(datos);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            codec.Info.Width is < 32 or > 8000 || codec.Info.Height is < 32 or > 8000 || (long)codec.Info.Width * codec.Info.Height > 20000000 || codec.FrameCount > 1)
            return new Error("Sube una foto JPG, PNG o WebP válida, de hasta 20 megapíxeles.", "Foto");
        using var original = new SKBitmap(codec.Info);
        if (codec.GetPixels(original.Info, original.GetPixels()) != SKCodecResult.Success) return new Error("La imagen está dañada.", "Foto");
        var rotada = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var w = original.Width; var h = original.Height;
        var ancho = rotada ? h : w; var alto = rotada ? w : h;
        var escala = Math.Min(1f, 1600f / Math.Max(ancho, alto));
        using var normal = new SKBitmap(Math.Max(1, (int)(ancho * escala)), Math.Max(1, (int)(alto * escala)));
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
        using var imagen = SKImage.FromBitmap(normal);
        using var codificada = imagen.Encode(SKEncodedImageFormat.Jpeg, 85);
        Directory.CreateDirectory(opciones.Value.Carpeta);
        var nombre = $"{Guid.NewGuid():N}.jpg";
        try { await File.WriteAllBytesAsync(Path.Combine(opciones.Value.Carpeta, nombre), codificada.ToArray(), ct); }
        catch { Eliminar(nombre); throw; }
        return Resultado.Ok(nombre);
    }
    public string? RutaFisica(string nombre) => nombre.Length == 36 && nombre.EndsWith(".jpg", StringComparison.Ordinal) && Guid.TryParseExact(nombre[..32], "N", out _) ? Path.Combine(opciones.Value.Carpeta, nombre) : null;
    public void Eliminar(string nombre) { if (RutaFisica(nombre) is { } ruta && File.Exists(ruta)) File.Delete(ruta); }
}
