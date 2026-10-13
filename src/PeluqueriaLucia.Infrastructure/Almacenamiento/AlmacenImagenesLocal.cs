using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;

namespace PeluqueriaLucia.Infrastructure.Almacenamiento;

public sealed class OpcionesImagenes
{
    public const string Seccion = "Imagenes";

    /// <summary>Carpeta física donde se guardan las fotos. Se rellena al arrancar (por defecto, wwwroot/imagenes).</summary>
    public string Carpeta { get; set; } = string.Empty;

    /// <summary>Ruta web con la que se sirven. Es la que queda guardada en la base de datos.</summary>
    public string RutaPublica { get; set; } = "/imagenes";
}

/// <summary>Guarda las fotos del catálogo en una carpeta del propio equipo con nombres únicos.</summary>
internal sealed class AlmacenImagenesLocal(IOptions<OpcionesImagenes> opciones, ILogger<AlmacenImagenesLocal> logger) : IAlmacenImagenes
{
    private readonly OpcionesImagenes _opciones = opciones.Value;

    public async Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct)
    {
        Directory.CreateDirectory(_opciones.Carpeta);
        var nombre = $"{Guid.NewGuid():N}{extension}";
        await using (var destino = new FileStream(Path.Combine(_opciones.Carpeta, nombre), FileMode.CreateNew))
        {
            await contenido.CopyToAsync(destino, ct);
        }

        return $"{_opciones.RutaPublica.TrimEnd('/')}/{nombre}";
    }

    public void Eliminar(string? rutaPublica)
    {
        if (RutaFisica(rutaPublica) is not { } ruta || !File.Exists(ruta))
            return;

        try
        {
            File.Delete(ruta);
        }
        catch (IOException ex)
        {
            // Una foto que no se puede borrar (abierta en otro programa…) no debe romper la operación
            logger.LogWarning(ex, "No se ha podido borrar la imagen {Ruta}", ruta);
        }
    }

    /// <summary>
    /// Solo se aceptan rutas de nuestra carpeta y sin subdirectorios, así un valor manipulado en la base de datos
    /// nunca puede borrar otro fichero del equipo.
    /// </summary>
    private string? RutaFisica(string? rutaPublica)
    {
        var prefijo = _opciones.RutaPublica.TrimEnd('/') + "/";
        if (string.IsNullOrEmpty(rutaPublica) || !rutaPublica.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
            return null;

        var nombre = rutaPublica[prefijo.Length..];
        return nombre.Length == 0 || nombre != Path.GetFileName(nombre) ? null : Path.Combine(_opciones.Carpeta, nombre);
    }
}
