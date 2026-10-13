using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Application.Catalogo;

public sealed record DatosPublicacion(int? CategoriaId, string? Descripcion, Domain.Publico.TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado, string? DescripcionEn = null);
/// <summary>El nombre en inglés es obligatorio al crear; en los servicios anteriores al inglés puede faltar y la web muestra el español.</summary>
public sealed record DatosServicio(string Nombre, decimal Precio, Sexo Sexo, DatosPublicacion? Publicacion = null, string? NombreEn = null);

/// <summary>Qué se hizo al pedir eliminar un servicio.</summary>
public enum Eliminacion
{
    Eliminado,

    /// <summary>Aparece en visitas, así que se archiva en lugar de borrarse para no perder el historial.</summary>
    Archivado,
}

public sealed class CatalogoService(
    IServicioRepositorio servicios,
    IAlmacenImagenes imagenes,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj, IContenidoRepositorio? contenido = null)
{
    public const long TamanoMaximoImagen = 5 * 1024 * 1024;

    private static readonly HashSet<string> ExtensionesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public Task<IReadOnlyList<Servicio>> ListarAsync(Sexo? sexo, bool incluirArchivados, CancellationToken ct = default) =>
        servicios.ListarAsync(sexo, incluirArchivados, ct);

    /// <summary>Buscador del catálogo: palabras en el nombre o la descripción pública, filtradas en la base de datos.</summary>
    public Task<IReadOnlyList<Servicio>> BuscarAsync(string? texto, Sexo? sexo, bool incluirArchivados, CancellationToken ct = default) =>
        servicios.BuscarAsync(texto, sexo, incluirArchivados, ct);

    public Task<Servicio?> ObtenerAsync(int id, CancellationToken ct = default) => servicios.ObtenerAsync(id, ct);

    public Task<Resultado<int>> CrearAsync(DatosServicio datos, ArchivoImagen? imagen, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            if (ValidarImagen(imagen) is { } error)
                return error;
            if (await ValidarPublicacionAsync(datos.Publicacion, ct) is { } errorWeb) return errorWeb;
            // Lo nuevo nace en los dos idiomas; solo lo anterior al inglés puede quedarse sin traducción
            if (string.IsNullOrWhiteSpace(datos.NombreEn))
                return new Error("Escribe también el nombre en inglés.", "NombreEn");
            if (datos.Publicacion is { Descripcion.Length: > 0 } web && string.IsNullOrWhiteSpace(web.DescripcionEn))
                return new Error("Escribe también la descripción en inglés.", "DescripcionPublicaEn");

            var servicio = Servicio.Crear(datos.Nombre, datos.Precio, datos.Sexo, reloj.GetLocalNow().DateTime, datos.NombreEn);
            await ConfigurarWebAsync(servicio, datos.Publicacion, ct);
            servicio.Traducir(datos.NombreEn, datos.Publicacion?.DescripcionEn);
            var nuevaRuta = imagen is null ? null : await GuardarImagenAsync(imagen, ct);
            servicio.CambiarImagen(nuevaRuta);
            servicios.Agregar(servicio);
            await GuardarODescartarImagenAsync(nuevaRuta, ct);
            return Resultado.Ok(servicio.Id);
        });

    public Task<Resultado<int>> ActualizarAsync(
        int id, DatosServicio datos, ArchivoImagen? imagen, bool quitarImagen, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var servicio = await servicios.ObtenerAsync(id, ct);
            if (servicio is null)
                return Error.NoEncontrado("El servicio");
            if (ValidarImagen(imagen) is { } error)
                return error;
            if (await ValidarPublicacionAsync(datos.Publicacion, ct) is { } errorWeb) return errorWeb;

            servicio.Actualizar(datos.Nombre, datos.Precio, datos.Sexo);
            await ConfigurarWebAsync(servicio, datos.Publicacion, ct);
            // Al editar, el inglés es opcional: los servicios anteriores al inglés se traducen cuando se pueda
            servicio.Traducir(datos.NombreEn, datos.Publicacion is null ? servicio.DescripcionPublicaEn : datos.Publicacion.DescripcionEn);

            var anterior = servicio.ImagenRuta;
            string? nuevaRuta = null;
            if (imagen is not null)
            {
                nuevaRuta = await GuardarImagenAsync(imagen, ct);
                servicio.CambiarImagen(nuevaRuta);
            }
            else if (quitarImagen)
            {
                servicio.CambiarImagen(null);
            }

            await GuardarODescartarImagenAsync(nuevaRuta, ct);

            // La foto anterior solo se borra cuando la base de datos ya apunta a la nueva
            if (anterior is not null && anterior != servicio.ImagenRuta)
                imagenes.Eliminar(anterior);
            return Resultado.Ok(servicio.Id);
        });

    public async Task<Resultado<Eliminacion>> EliminarAsync(int id, CancellationToken ct = default)
    {
        var servicio = await servicios.ObtenerAsync(id, ct);
        if (servicio is null)
            return Error.NoEncontrado("El servicio");

        if (await servicios.TieneVisitasAsync(id, ct))
        {
            servicio.Archivar();
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(Eliminacion.Archivado);
        }

        var imagen = servicio.ImagenRuta;
        servicios.Eliminar(servicio);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        imagenes.Eliminar(imagen);
        return Resultado.Ok(Eliminacion.Eliminado);
    }

    public async Task<Resultado> ReactivarAsync(int id, CancellationToken ct = default)
    {
        var servicio = await servicios.ObtenerAsync(id, ct);
        if (servicio is null)
            return Error.NoEncontrado("El servicio");

        servicio.Reactivar();
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    private static Error? ValidarImagen(ArchivoImagen? imagen)
    {
        if (imagen is null)
            return null;
        if (!ExtensionesPermitidas.Contains(Path.GetExtension(imagen.NombreOriginal)))
            return new Error("La foto tiene que ser JPG, PNG, WEBP o GIF.", "Imagen");
        if (imagen.Tamano > TamanoMaximoImagen)
            return new Error("La foto no puede pesar más de 5 MB.", "Imagen");
        return null;
    }

    private async Task ConfigurarWebAsync(Servicio servicio, DatosPublicacion? web, CancellationToken ct)
    {
        // La posición no se escribe a mano: se conserva y, al cambiar de categoría, el servicio pasa al final de la nueva
        if (web is not null) servicio.ConfigurarPublicacion(web.CategoriaId, web.Descripcion, web.TipoPrecio, web.DuracionMinutos,
            web.CategoriaId is { } categoria && categoria != servicio.CategoriaPublicaId ? await Publico.OrdenWeb.SiguienteAsync(servicios, categoria, ct) : servicio.OrdenPublico,
            web.Publicado, web.Destacado);
        else if (servicio.Publicado) servicio.ConfigurarPublicacion(servicio.CategoriaPublicaId, servicio.DescripcionPublica, servicio.TipoPrecioPublico, servicio.DuracionMinutos, servicio.OrdenPublico, true, servicio.Destacado);
    }

    private async Task<Error?> ValidarPublicacionAsync(DatosPublicacion? web, CancellationToken ct) =>
        web?.CategoriaId is { } id && contenido is not null && await contenido.CategoriaAsync(id, ct) is null
            ? new Error("Elige una categoría válida.", "CategoriaPublicaId") : null;

    private Task<string> GuardarImagenAsync(ArchivoImagen imagen, CancellationToken ct) =>
        imagenes.GuardarAsync(imagen.Contenido, Path.GetExtension(imagen.NombreOriginal).ToLowerInvariant(), ct);

    /// <summary>Si la base de datos falla, la foto recién subida no se queda huérfana en disco.</summary>
    private async Task GuardarODescartarImagenAsync(string? nuevaRuta, CancellationToken ct)
    {
        try
        {
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        }
        catch
        {
            imagenes.Eliminar(nuevaRuta);
            throw;
        }
    }
}
