using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Application.Publico;

public sealed record DatosTratamiento(int CategoriaId, string Nombre, string? Descripcion, decimal? Precio, TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado);
public sealed class ContenidoService(IContenidoRepositorio repositorio, IUnidadDeTrabajo unidad)
{
    public Task<IReadOnlyList<CategoriaTratamiento>> CategoriasAsync(CancellationToken ct = default) => repositorio.CategoriasAsync(ct);
    public Task<IReadOnlyList<TratamientoPublico>> TratamientosAsync(bool publicados, CancellationToken ct = default) => repositorio.TratamientosAsync(publicados, ct);
    public Task<TratamientoPublico?> TratamientoAsync(int id, CancellationToken ct = default) => repositorio.TratamientoAsync(id, ct);
    public Task<CategoriaTratamiento?> CategoriaAsync(int id, CancellationToken ct = default) => repositorio.CategoriaAsync(id, ct);
    public async Task<Resultado> GuardarTratamientoAsync(int? id, DatosTratamiento datos, CancellationToken ct = default) => await Resultado.ProtegerAsync(async () => {
        if (await repositorio.CategoriaAsync(datos.CategoriaId, ct) is null) throw new Domain.Comun.ReglaDeNegocioException("Elige una categoría válida.");
        if (id is { } clave) {
            var t = await repositorio.TratamientoAsync(clave, ct) ?? throw new Domain.Comun.ReglaDeNegocioException("El tratamiento no existe.");
            t.Editar(datos.CategoriaId, datos.Nombre, datos.Descripcion, datos.Precio, datos.TipoPrecio, datos.DuracionMinutos, datos.Orden, datos.Publicado, datos.Destacado);
        } else repositorio.Agregar(TratamientoPublico.Crear(datos.CategoriaId, datos.Nombre, datos.Descripcion, datos.Precio, datos.TipoPrecio, datos.DuracionMinutos, datos.Orden, datos.Publicado, datos.Destacado));
        await unidad.GuardarCambiosAsync(ct);
    });
    public async Task<Resultado> GuardarCategoriaAsync(int? id, string nombre, int orden, CancellationToken ct = default) => await Resultado.ProtegerAsync(async () => {
        if (id is { } clave) {
            var c = await repositorio.CategoriaAsync(clave, ct) ?? throw new Domain.Comun.ReglaDeNegocioException("La categoría no existe."); c.Editar(nombre, orden);
        } else {
            var nueva = CategoriaTratamiento.Crear(nombre, orden);
            if ((await repositorio.CategoriasAsync(ct)).Any(x => x.Slug == nueva.Slug)) throw new Domain.Comun.ReglaDeNegocioException("Ya existe una categoría con ese nombre.");
            repositorio.Agregar(nueva);
        }
        await unidad.GuardarCambiosAsync(ct);
    });
    public async Task<Resultado> EliminarTratamientoAsync(int id, CancellationToken ct = default) {
        var t = await repositorio.TratamientoAsync(id, ct); if (t is null) return Error.NoEncontrado("El tratamiento");
        repositorio.Eliminar(t); await unidad.GuardarCambiosAsync(ct); return Resultado.Ok();
    }
    public async Task<Resultado> EliminarCategoriaAsync(int id, CancellationToken ct = default) {
        var c = await repositorio.CategoriaAsync(id, ct); if (c is null) return Error.NoEncontrado("La categoría");
        if ((await repositorio.TratamientosAsync(false, ct)).Any(x => x.CategoriaId == id)) return new Error("Mueve o elimina antes los tratamientos de esta categoría.");
        repositorio.Eliminar(c); await unidad.GuardarCambiosAsync(ct); return Resultado.Ok();
    }
}
