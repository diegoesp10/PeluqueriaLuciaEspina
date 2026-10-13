using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Domain.Comun;
namespace PeluqueriaLucia.Application.Publico;

public sealed record DatosTratamiento(int ServicioId, int CategoriaId, string? Descripcion, TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado, string? DescripcionEn = null);
public sealed class ContenidoService(IContenidoRepositorio repositorio, IServicioRepositorio servicios, IUnidadDeTrabajo unidad)
{
    public Task<IReadOnlyList<CategoriaTratamiento>> CategoriasAsync(CancellationToken ct = default) => repositorio.CategoriasAsync(ct);
    public async Task<IReadOnlyList<TratamientoWeb>> TratamientosAsync(bool publicados, CancellationToken ct = default) =>
        (await servicios.ListarAsync(null, true, ct)).Where(s => s.CategoriaPublicaId != null && (!publicados || s.Publicado && s.Activo))
        .OrderBy(s => s.OrdenPublico).ThenBy(s => s.Id).Select(TratamientoWeb.Desde).ToList();
    /// <summary>Buscador de «Servicios de la web»: palabras en el nombre o la descripción, filtradas en la base de datos.</summary>
    public async Task<IReadOnlyList<TratamientoWeb>> BuscarTratamientosAsync(string? texto, CancellationToken ct = default) =>
        (await servicios.BuscarAsync(texto, null, true, ct)).Where(s => s.CategoriaPublicaId != null)
        .OrderBy(s => s.OrdenPublico).ThenBy(s => s.Id).Select(TratamientoWeb.Desde).ToList();
    public async Task<TratamientoWeb?> TratamientoAsync(int id, CancellationToken ct = default) =>
        await servicios.ObtenerAsync(id, ct) is { CategoriaPublicaId: not null } s ? TratamientoWeb.Desde(s) : null;
    public Task<CategoriaTratamiento?> CategoriaAsync(int id, CancellationToken ct = default) => repositorio.CategoriaAsync(id, ct);
    public Task<Resultado> GuardarTratamientoAsync(int? id, DatosTratamiento datos, CancellationToken ct = default) => Resultado.ProtegerAsync(async () => {
        if (await repositorio.CategoriaAsync(datos.CategoriaId, ct) is null) throw new ReglaDeNegocioException("Elige una categoría válida.");
        var servicio = await servicios.ObtenerAsync(id ?? datos.ServicioId, ct) ?? throw new ReglaDeNegocioException("Selecciona un servicio existente del catálogo.");
        if (!servicio.Activo) throw new ReglaDeNegocioException("Reactiva el servicio antes de publicarlo.");
        // Al publicarlo o cambiarlo de categoría entra al final; si no, conserva su posición
        var orden = servicio.CategoriaPublicaId == datos.CategoriaId ? servicio.OrdenPublico : await OrdenWeb.SiguienteAsync(servicios, datos.CategoriaId, ct);
        servicio.ConfigurarPublicacion(datos.CategoriaId, datos.Descripcion, datos.TipoPrecio, datos.DuracionMinutos, orden, datos.Publicado, datos.Destacado);
        // El servicio ya existe en el catálogo: la descripción en inglés es opcional y, si falta, la web muestra la española
        servicio.Traducir(servicio.NombreEn, datos.DescripcionEn);
        await unidad.GuardarCambiosAsync(ct);
    });
    /// <summary>Crea o renombra una categoría. Una categoría nueva aparece la última en la web.</summary>
    public Task<Resultado> GuardarCategoriaAsync(int? id, string nombre, string? nombreEn = null, CancellationToken ct = default) => Resultado.ProtegerAsync(async () => {
        if (id is { } clave) {
            // Las categorías anteriores al inglés se traducen cuando se pueda; mientras, la web muestra el español
            var c = await repositorio.CategoriaAsync(clave, ct) ?? throw new ReglaDeNegocioException("La categoría no existe."); c.Editar(nombre, c.Orden); c.Traducir(nombreEn);
        } else {
            if (string.IsNullOrWhiteSpace(nombreEn)) throw new ReglaDeNegocioException("Escribe también el nombre en inglés.", "NombreEn");
            var existentes = await repositorio.CategoriasAsync(ct);
            var nueva = CategoriaTratamiento.Crear(nombre, existentes.Select(x => x.Orden).DefaultIfEmpty(0).Max() + 1, nombreEn);
            if (existentes.Any(x => x.Slug == nueva.Slug)) throw new ReglaDeNegocioException("Ya existe una categoría con ese nombre.");
            repositorio.Agregar(nueva);
        }
        await unidad.GuardarCambiosAsync(ct);
    });

    /// <summary>Sube (-1) o baja (+1) una categoría un puesto en la web y deja las posiciones 1, 2, 3…</summary>
    public async Task<Resultado> MoverCategoriaAsync(int id, int desplazamiento, CancellationToken ct = default)
    {
        var categorias = await repositorio.CategoriasAsync(ct);
        if (categorias.All(x => x.Id != id)) return Error.NoEncontrado("La categoría");
        var posicion = 1;
        foreach (var c in OrdenWeb.Mover(categorias, x => x.Id, id, desplazamiento)) c.Colocar(posicion++);
        await unidad.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>Sube (-1) o baja (+1) un servicio un puesto dentro de su categoría en la web.</summary>
    public async Task<Resultado> MoverTratamientoAsync(int id, int desplazamiento, CancellationToken ct = default)
    {
        var servicio = await servicios.ObtenerAsync(id, ct);
        if (servicio?.CategoriaPublicaId is not { } categoria) return Error.NoEncontrado("El servicio");
        var ids = (await servicios.ListarAsync(null, true, ct)).Where(s => s.CategoriaPublicaId == categoria).Select(s => s.Id).ToList();
        var hermanos = (await servicios.ObtenerVariosAsync(ids, ct)).OrderBy(s => s.OrdenPublico).ThenBy(s => s.Id);
        var posicion = 1;
        foreach (var s in OrdenWeb.Mover(hermanos, x => x.Id, id, desplazamiento)) s.ColocarEnWeb(posicion++);
        await unidad.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }
    public async Task<Resultado> EliminarTratamientoAsync(int id, CancellationToken ct = default) {
        var s = await servicios.ObtenerAsync(id, ct); if (s is null) return Error.NoEncontrado("El servicio");
        s.ConfigurarPublicacion(null, null, s.TipoPrecioPublico, s.DuracionMinutos, s.OrdenPublico, false, false);
        await unidad.GuardarCambiosAsync(ct); return Resultado.Ok();
    }
    public async Task<Resultado> EliminarCategoriaAsync(int id, CancellationToken ct = default) {
        var c = await repositorio.CategoriaAsync(id, ct); if (c is null) return Error.NoEncontrado("La categoría");
        if ((await servicios.ListarAsync(null, true, ct)).Any(x => x.CategoriaPublicaId == id)) return new Error("Mueve o retira antes los servicios de esta categoría.");
        repositorio.Eliminar(c); await unidad.GuardarCambiosAsync(ct); return Resultado.Ok();
    }
}
