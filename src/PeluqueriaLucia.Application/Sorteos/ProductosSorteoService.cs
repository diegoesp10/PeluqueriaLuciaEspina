using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Sorteos;

namespace PeluqueriaLucia.Application.Sorteos;

/// <summary>Productos de sorteo guardados.</summary>
public interface IProductoSorteoRepositorio
{
    Task<ProductoSorteo?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Todos (también los eliminados), por nombre, con cuántas veces han estado en sorteos.</summary>
    Task<IReadOnlyList<ResumenProductoSorteo>> ResumenAsync(CancellationToken ct = default);

    /// <summary>Los que se pueden añadir a un sorteo (no eliminados), por nombre.</summary>
    Task<IReadOnlyList<ProductoSorteo>> ActivosAsync(CancellationToken ct = default);

    /// <summary>Uno no eliminado con ese nombre (sin distinguir mayúsculas), salvo <paramref name="excepto"/>.</summary>
    Task<ProductoSorteo?> ActivoPorNombreAsync(string nombre, int? excepto, CancellationToken ct = default);

    /// <summary>Los sorteos en los que ha estado como premio, lo último primero, con sus premios y extracciones (solo lectura).</summary>
    Task<IReadOnlyList<Sorteo>> SorteosAsync(int id, CancellationToken ct = default);

    void Agregar(ProductoSorteo producto);
}

/// <param name="EnMarcha">Sorteos abiertos en los que es premio.</param>
/// <param name="Sorteados">Sorteos realizados en los que fue premio.</param>
/// <param name="Entregados">De esos, en cuántos el ganador ya lo ha recibido.</param>
public sealed record ResumenProductoSorteo(ProductoSorteo Producto, int EnMarcha, int Sorteados, int Entregados);

/// <summary>
/// Productos de sorteo: lo que el salón sortea y no es de la peluquería (una cena, unas entradas…). Se dan de alta con su
/// nombre y su descripción y se añaden a los sorteos junto a servicios y productos de la tienda.
/// </summary>
public sealed class ProductosSorteoService(IProductoSorteoRepositorio productos, ISorteoRepositorio sorteos, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    /// <summary>Hora del salón, aunque el servidor esté en otra zona.</summary>
    private DateTime Ahora => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), AgendaService.Zona).DateTime;

    public Task<IReadOnlyList<ResumenProductoSorteo>> ListarAsync(CancellationToken ct = default) => productos.ResumenAsync(ct);

    public Task<IReadOnlyList<ProductoSorteo>> ActivosAsync(CancellationToken ct = default) => productos.ActivosAsync(ct);

    public Task<ProductoSorteo?> ObtenerAsync(int id, CancellationToken ct = default) => productos.ObtenerAsync(id, ct);

    /// <summary>Los sorteos en los que ha estado: cuándo, quién ganó y si se le entregó.</summary>
    public Task<IReadOnlyList<Sorteo>> HistorialAsync(int id, CancellationToken ct = default) => productos.SorteosAsync(id, ct);

    public Task<Resultado<int>> CrearAsync(FichaProductoSorteo ficha, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var producto = ProductoSorteo.Crear(ficha, Ahora);
            if (await productos.ActivoPorNombreAsync(producto.Nombre, null, ct) is not null)
                return new Error($"Ya hay un producto de sorteo llamado «{producto.Nombre}».", nameof(ProductoSorteo.Nombre));
            productos.Agregar(producto);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(producto.Id);
        });

    /// <summary>
    /// Guarda los cambios y los lleva a los sorteos aún abiertos que lo tienen como premio (los realizados conservan lo que
    /// se sorteó). Devuelve en cuántos sorteos abiertos se ha puesto al día.
    /// </summary>
    public async Task<Resultado<int>> EditarAsync(int id, FichaProductoSorteo ficha, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto de sorteo");
        return await Resultado.ProtegerAsync<int>(async () =>
        {
            producto.Editar(ficha, Ahora);
            if (producto.Activo && await productos.ActivoPorNombreAsync(producto.Nombre, id, ct) is not null)
                return new Error($"Ya hay otro producto de sorteo llamado «{producto.Nombre}».", nameof(ProductoSorteo.Nombre));
            var puestosAlDia = 0;
            foreach (var sorteo in await sorteos.AbiertosConProductoSorteoAsync(id, ct))
            {
                if (sorteo.ActualizarPremio(DatosPremio.De(producto), Ahora))
                    puestosAlDia++;
            }

            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(puestosAlDia);
        });
    }

    /// <summary>
    /// Borrado lógico: deja de ofrecerse para nuevos sorteos. Los sorteos en los que ya está lo conservan. Devuelve en
    /// cuántos sorteos abiertos sigue (para avisar).
    /// </summary>
    public async Task<Resultado<int>> EliminarAsync(int id, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto de sorteo");
        return await Resultado.ProtegerAsync<int>(async () =>
        {
            producto.Eliminar(Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok((await sorteos.AbiertosConProductoSorteoAsync(id, ct)).Count);
        });
    }

    public async Task<Resultado> RecuperarAsync(int id, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return Error.NoEncontrado("El producto de sorteo");
        if (!producto.Activo && await productos.ActivoPorNombreAsync(producto.Nombre, id, ct) is not null)
            return new Error($"Ya hay otro producto de sorteo llamado «{producto.Nombre}». Cámbiale el nombre a uno de los dos antes de recuperarlo.");
        return await Resultado.ProtegerAsync(async () =>
        {
            producto.Recuperar(Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }
}
