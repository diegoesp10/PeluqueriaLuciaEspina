using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Domain.Comun;
namespace PeluqueriaLucia.Application.Publico;

public sealed record DatosTratamiento(int ServicioId, int CategoriaId, string? Descripcion, TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado);
public sealed class ContenidoService(IContenidoRepositorio repositorio, IServicioRepositorio servicios, IUnidadDeTrabajo unidad)
{
    public Task<IReadOnlyList<CategoriaTratamiento>> CategoriasAsync(CancellationToken ct = default) => repositorio.CategoriasAsync(ct);
    public async Task<IReadOnlyList<TratamientoWeb>> TratamientosAsync(bool publicados, CancellationToken ct = default) =>
        (await servicios.ListarAsync(null, true, ct)).Where(s => s.CategoriaPublicaId != null && (!publicados || s.Publicado && s.Activo))
        .OrderBy(s => s.OrdenPublico).ThenBy(s => s.Id).Select(TratamientoWeb.Desde).ToList();
    public async Task<TratamientoWeb?> TratamientoAsync(int id, CancellationToken ct = default) =>
        await servicios.ObtenerAsync(id, ct) is { CategoriaPublicaId: not null } s ? TratamientoWeb.Desde(s) : null;
    public Task<CategoriaTratamiento?> CategoriaAsync(int id, CancellationToken ct = default) => repositorio.CategoriaAsync(id, ct);
    public Task<Resultado> GuardarTratamientoAsync(int? id, DatosTratamiento datos, CancellationToken ct = default) => Resultado.ProtegerAsync(async () => {
        if (await repositorio.CategoriaAsync(datos.CategoriaId, ct) is null) throw new ReglaDeNegocioException("Elige una categoría válida.");
        var servicio = await servicios.ObtenerAsync(id ?? datos.ServicioId, ct) ?? throw new ReglaDeNegocioException("Selecciona un servicio existente del catálogo.");
        if (!servicio.Activo) throw new ReglaDeNegocioException("Reactiva el servicio antes de publicarlo.");
        servicio.ConfigurarPublicacion(datos.CategoriaId, datos.Descripcion, datos.TipoPrecio, datos.DuracionMinutos, datos.Orden, datos.Publicado, datos.Destacado);
        await unidad.GuardarCambiosAsync(ct);
    });
    public Task<Resultado> GuardarCategoriaAsync(int? id, string nombre, int orden, CancellationToken ct = default) => Resultado.ProtegerAsync(async () => {
        if (id is { } clave) {
            var c = await repositorio.CategoriaAsync(clave, ct) ?? throw new ReglaDeNegocioException("La categoría no existe."); c.Editar(nombre, orden);
        } else {
            var nueva = CategoriaTratamiento.Crear(nombre, orden);
            if ((await repositorio.CategoriasAsync(ct)).Any(x => x.Slug == nueva.Slug)) throw new ReglaDeNegocioException("Ya existe una categoría con ese nombre.");
            repositorio.Agregar(nueva);
        }
        await unidad.GuardarCambiosAsync(ct);
    });
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
