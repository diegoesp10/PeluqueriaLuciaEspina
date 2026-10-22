using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;

namespace PeluqueriaLucia.Infrastructure.Almacenamiento;

public sealed class OpcionesFotosProductos
{
    public const string Seccion = "FotosProductos";

    /// <summary>Carpeta privada de las fotos. Se rellena al arrancar (ver PresentacionServiceCollectionExtensions).</summary>
    public string Carpeta { get; set; } = string.Empty;
}

/// <summary>Cada foto se guarda como «{id}.jpg» (hasta 1600 px) y «{id}-mini.jpg» (hasta 480 px), siempre en JPEG limpio.</summary>
internal sealed class FotosProductos(IOptions<OpcionesFotosProductos> opciones, ILogger<FotosProductos> logger) : IFotosProductos
{
    public const int LadoMiniatura = 480;

    public async Task<Resultado<Guid>> GuardarAsync(Stream contenido, CancellationToken ct)
    {
        var normalizada = await NormalizadorFotos.NormalizarAsync(contenido, IFotosProductos.TamanoMaximo, "Fotos", ct);
        if (!normalizada.Exito)
            return normalizada.Error!;

        using var grande = normalizada.Valor!;
        using var miniatura = NormalizadorFotos.Reducir(grande, LadoMiniatura);
        var id = Guid.NewGuid();
        Directory.CreateDirectory(opciones.Value.Carpeta);
        try
        {
            await File.WriteAllBytesAsync(Ruta(id, miniatura: false), NormalizadorFotos.Jpeg(grande, 85), ct);
            await File.WriteAllBytesAsync(Ruta(id, miniatura: true), NormalizadorFotos.Jpeg(miniatura, 80), ct);
        }
        catch
        {
            Eliminar(id);
            throw;
        }

        return Resultado.Ok(id);
    }

    public string? RutaFisica(Guid id, bool miniatura)
    {
        var ruta = Ruta(id, miniatura);
        return File.Exists(ruta) ? ruta : null;
    }

    public void Eliminar(Guid id)
    {
        foreach (var miniatura in new[] { false, true })
        {
            try
            {
                File.Delete(Ruta(id, miniatura));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un fichero que no se puede borrar no debe deshacer la operación: queda huérfano en la carpeta
                logger.LogWarning(ex, "No se ha podido borrar la foto de producto {Id}", id);
            }
        }
    }

    /// <summary>El nombre sale del Guid (32 cifras hexadecimales), así que nunca apunta fuera de la carpeta.</summary>
    private string Ruta(Guid id, bool miniatura) =>
        Path.Combine(opciones.Value.Carpeta, miniatura ? $"{id:N}-mini.jpg" : $"{id:N}.jpg");
}
