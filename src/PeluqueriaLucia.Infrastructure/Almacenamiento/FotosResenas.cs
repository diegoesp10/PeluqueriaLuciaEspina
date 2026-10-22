using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;

namespace PeluqueriaLucia.Infrastructure.Almacenamiento;

public sealed class OpcionesFotosResenas { public string Carpeta { get; set; } = ""; }
internal sealed class FotosResenas(IOptions<OpcionesFotosResenas> opciones) : IFotosResenas
{
    public async Task<Resultado<string>> GuardarAsync(Stream contenido, CancellationToken ct)
    {
        var normalizada = await NormalizadorFotos.NormalizarAsync(contenido, 5 * 1024 * 1024, "Foto", ct);
        if (!normalizada.Exito) return normalizada.Error!;
        using var imagen = normalizada.Valor!;
        var jpeg = NormalizadorFotos.Jpeg(imagen, 85);
        Directory.CreateDirectory(opciones.Value.Carpeta);
        var nombre = $"{Guid.NewGuid():N}.jpg";
        try { await File.WriteAllBytesAsync(Path.Combine(opciones.Value.Carpeta, nombre), jpeg, ct); }
        catch { Eliminar(nombre); throw; }
        return Resultado.Ok(nombre);
    }
    public string? RutaFisica(string nombre) => nombre.Length == 36 && nombre.EndsWith(".jpg", StringComparison.Ordinal) && Guid.TryParseExact(nombre[..32], "N", out _) ? Path.Combine(opciones.Value.Carpeta, nombre) : null;
    public void Eliminar(string nombre) { if (RutaFisica(nombre) is { } ruta && File.Exists(ruta)) File.Delete(ruta); }
}
