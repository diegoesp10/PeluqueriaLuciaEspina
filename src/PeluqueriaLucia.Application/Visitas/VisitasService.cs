using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Application.Visitas;

public sealed class VisitasService(
    IVisitaRepositorio visitas,
    IClienteRepositorio clientes,
    IServicioRepositorio servicios,
    IProductoRepositorio productos,
    IExportadorVisitas exportador,
    IUnidadDeTrabajo unidadDeTrabajo,
    TimeProvider reloj)
{
    public const int TamanoPagina = 30;

    public Task<Pagina<Visita>> ListarAsync(FiltroVisitas filtro, int pagina, CancellationToken ct = default) =>
        visitas.ListarAsync(filtro, Pagina<Visita>.Normalizar(pagina), TamanoPagina, ct);

    public Task<ResumenVisitas> ResumirAsync(FiltroVisitas filtro, CancellationToken ct = default) =>
        visitas.ResumirAsync(filtro, ct);

    public Task<Visita?> ObtenerAsync(int id, CancellationToken ct = default) => visitas.ObtenerAsync(id, ct);

    public Task<Resultado<int>> RegistrarAsync(NuevaVisita datos, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var cliente = await clientes.ObtenerAsync(datos.ClienteId, ct);
            if (cliente is null)
                return new Error("Elige un cliente.", "ClienteId");

            var ids = datos.ServicioIds.Distinct().ToList();
            var elegidos = await servicios.ObtenerVariosAsync(ids, ct);
            if (elegidos.Count != ids.Count)
                return new Error("Alguno de los servicios elegidos ya no existe. Vuelve a cargar la página.", "Servicios");

            var pedidos = datos.Productos ?? [];
            var idsProductos = pedidos.Select(p => p.ProductoId).Distinct().ToList();
            IReadOnlyList<Producto> existencias = idsProductos.Count == 0 ? [] : await productos.ObtenerVariosAsync(idsProductos, ct);
            if (existencias.Count != idsProductos.Count)
                return new Error("Alguno de los productos elegidos ya no existe. Vuelve a cargar la página.", "Productos");

            // Se respeta el orden en que se marcaron para que el ticket salga igual que en pantalla
            var ordenados = ids.Select(id => elegidos.First(s => s.Id == id)).ToList();
            var vendidos = pedidos.Select(p => new ProductoPedido(existencias.First(e => e.Id == p.ProductoId), p.Cantidad, p.PrecioUnitario)).ToList();
            var visita = Visita.Registrar(cliente, datos.Fecha, datos.FormaPago, ordenados, datos.Precios, vendidos);
            visitas.Agregar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(visita.Id);
        });

    /// <summary>
    /// Cobra la visita y, en la misma operación, descuenta del stock los productos vendidos y los <paramref name="usados"/>
    /// en el servicio (estos no se cobran). Si falta stock, queda en negativo. Si otro cobro ha movido a la vez el mismo
    /// producto, el guardado falla por concurrencia y no se cobra nada (quien llama lo avisa).
    /// </summary>
    public async Task<Resultado> CobrarAsync(int id, FormaPago formaPago, decimal? efectivo, decimal? tarjeta, int? administradorId = null,
        IReadOnlyCollection<ProductoUsadoCobro>? usados = null, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        usados ??= [];
        var ids = visita.Productos.Select(p => p.ProductoId).Concat(usados.Select(u => u.ProductoId)).Distinct().ToList();
        IReadOnlyList<Producto> existencias = ids.Count == 0 ? [] : await productos.ObtenerVariosAsync(ids, ct);
        if (usados.Any(u => existencias.All(e => e.Id != u.ProductoId)))
            return new Error("Alguno de los productos utilizados ya no existe. Vuelve a cargar la página.", "Usados");

        return await Resultado.ProtegerAsync(async () =>
        {
            var gastados = usados.Select(u => new ProductoUsado(existencias.First(e => e.Id == u.ProductoId), u.Cantidad)).ToList();
            var movimientos = visita.Cobrar(formaPago, efectivo, tarjeta, existencias, reloj.GetLocalNow().DateTime, administradorId, gastados);
            productos.AgregarMovimientos(movimientos);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>Productos gastados en el servicio de una visita cobrada (movimientos de uso enlazados a ella).</summary>
    public Task<IReadOnlyList<MovimientoStock>> UsadosAsync(int id, CancellationToken ct = default) => productos.UsadosEnVisitaAsync(id, ct);

    /// <summary>Añade un producto a una visita pendiente (o suma unidades si ya estaba al mismo precio).</summary>
    public async Task<Resultado> AgregarProductoAsync(int id, int productoId, int cantidad, decimal? precioUnitario, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");
        var producto = await productos.ObtenerAsync(productoId, ct);
        if (producto is null)
            return new Error("Elige un producto de la lista.", "ProductoId");

        return await Resultado.ProtegerAsync(async () =>
        {
            visita.AgregarProducto(producto, cantidad, precioUnitario);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado> QuitarProductoAsync(int id, int lineaId, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        return await Resultado.ProtegerAsync(async () =>
        {
            visita.QuitarProducto(lineaId);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        return await Resultado.ProtegerAsync(async () =>
        {
            visita.ComprobarQueSePuedeEliminar();
            visitas.Eliminar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public async Task<Resultado<ArchivoGenerado>> ExportarAsync(FiltroVisitas filtro, CancellationToken ct = default)
    {
        var seleccion = await visitas.ListarTodasAsync(filtro, ct);
        if (seleccion.Count == 0)
            return new Error($"No hay visitas que exportar ({filtro.Descripcion}).");

        return Resultado.Ok(exportador.Exportar(seleccion, filtro.Descripcion));
    }
}
