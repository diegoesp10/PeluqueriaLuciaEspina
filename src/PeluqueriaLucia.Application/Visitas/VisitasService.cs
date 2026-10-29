using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Comun;
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

    /// <summary>Las visitas de un día, sin paginar (para saber cuáles son de las citas de la agenda de ese día).</summary>
    public Task<IReadOnlyList<Visita>> DelDiaAsync(DateOnly dia, CancellationToken ct = default) =>
        visitas.ListarTodasAsync(FiltroVisitas.Dia(dia), ct);

    /// <summary>La cita de la agenda que se cobra con una visita.</summary>
    public Task<ReservaCita?> CitaAsync(Guid reservaId, CancellationToken ct = default) => visitas.CitaAsync(reservaId, ct);

    /// <summary>Citas confirmadas de un día que aún no tienen visita: las que quedan por cobrar.</summary>
    public Task<IReadOnlyList<ReservaCita>> CitasPorCobrarAsync(DateOnly dia, CancellationToken ct = default) =>
        visitas.CitasPorCobrarAsync(dia, ct);

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
            var pedidosVisita = pedidos.Select(p => new ProductoPedido(existencias.First(e => e.Id == p.ProductoId), p.Cantidad, p.PrecioUnitario, p.Uso)).ToList();
            var visita = Visita.Registrar(cliente, datos.Fecha, datos.FormaPago, ordenados, datos.Precios, pedidosVisita);
            visitas.Agregar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(visita.Id);
        });

    /// <summary>
    /// La visita con la que se cobra una cita confirmada (o completada) de la agenda: es el paso de «Completar y cobrar», y la
    /// cita solo queda completada al cobrarla. Si ya la tiene, esa. Si no, la que el cliente tenga ese día registrada aparte,
    /// que queda enlazada a la cita; y si no hay ninguna, una nueva con el servicio de la cita. Una cita dada sin ficha recibe
    /// la de su teléfono o una nueva por completar. Devuelve el número de la visita para editarla y cobrarla.
    /// </summary>
    public Task<Resultado<int>> AbrirDesdeCitaAsync(Guid reservaId, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var cita = await visitas.CitaAsync(reservaId, ct);
            if (cita is null)
                return Error.NoEncontrado("La cita");
            if (cita.Origen == OrigenCita.Bloqueo || cita.Estado is not (EstadoCita.Confirmada or EstadoCita.Completada))
                return new Error("Solo se cobran las citas confirmadas.");
            if (cita.PendienteVinculacion)
                return new Error("Antes de cobrarla, vincula la cita a la ficha del cliente (arriba, en «Contactos que necesitan revisión»).");

            if (await visitas.DeCitaAsync(reservaId, ct) is { } suya)
                return Resultado.Ok(suya.Id);

            if (cita.ClienteId is { } clienteId && await visitas.SinCitaDelDiaAsync(clienteId, cita.Inicio, ct) is { } aparte)
            {
                aparte.VincularCita(reservaId);
                if (aparte.EstaCobrada)
                    Completar(cita, null);
                await unidadDeTrabajo.GuardarCambiosAsync(ct);
                return Resultado.Ok(aparte.Id);
            }

            var cliente = cita.ClienteId is { } id ? await clientes.ObtenerAsync(id, ct) : await FichaParaCitaAsync(cita, ct);
            if (cliente is null)
                return new Error("La ficha del cliente de esta cita ya no existe.");
            var servicio = cita.ServicioId is { } servicioId ? await servicios.ObtenerAsync(servicioId, ct) : null;
            var visita = Visita.DesdeCita(cliente, reservaId, cita.Inicio, servicio);
            visitas.Agregar(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(visita.Id);
        });

    /// <summary>Guarda los precios cambiados de una visita pendiente, sin cobrarla.</summary>
    public Task<Resultado> GuardarPreciosAsync(int id, PreciosVisita precios, CancellationToken ct = default) =>
        EditarAsync(id, precios, _ => Task.CompletedTask, ct);

    /// <summary>Añade un servicio del catálogo a una visita pendiente, a su precio o al indicado.</summary>
    public Task<Resultado> AgregarServicioAsync(int id, int servicioId, decimal? precio, PreciosVisita? precios = null, CancellationToken ct = default) =>
        EditarAsync(id, precios, async visita =>
        {
            var servicio = await servicios.ObtenerAsync(servicioId, ct)
                ?? throw new ReglaDeNegocioException("Elige un servicio de la lista.", "ServicioId");
            visita.AgregarServicio(servicio, precio);
        }, ct);

    public Task<Resultado> QuitarServicioAsync(int id, int lineaId, PreciosVisita? precios = null, CancellationToken ct = default) =>
        EditarAsync(id, precios, visita =>
        {
            visita.QuitarServicio(lineaId);
            return Task.CompletedTask;
        }, ct);

    /// <summary>
    /// Añade un producto a una visita pendiente: vendido, o usado en el servicio (incluido, a 0,00 €). Si ya estaba igual,
    /// suma unidades.
    /// </summary>
    public Task<Resultado> AgregarProductoAsync(int id, int productoId, int cantidad, decimal? precioUnitario, UsoProducto uso = UsoProducto.Venta,
        PreciosVisita? precios = null, CancellationToken ct = default) =>
        EditarAsync(id, precios, async visita =>
        {
            var producto = await productos.ObtenerAsync(productoId, ct)
                ?? throw new ReglaDeNegocioException("Elige un producto de la lista.", "ProductoId");
            visita.AgregarProducto(producto, cantidad, precioUnitario, uso);
        }, ct);

    public Task<Resultado> QuitarProductoAsync(int id, int lineaId, PreciosVisita? precios = null, CancellationToken ct = default) =>
        EditarAsync(id, precios, visita =>
        {
            visita.QuitarProducto(lineaId);
            return Task.CompletedTask;
        }, ct);

    /// <summary>
    /// Cobra la visita con los <paramref name="precios"/> cambiados en el último momento y, en la misma operación, descuenta
    /// del stock lo vendido y lo usado en el servicio que se ha terminado (si falta, queda en negativo) y da por completada su
    /// cita de la agenda. Si otro cobro ha movido a la vez el mismo producto, o la cita ha cambiado, el guardado falla por
    /// concurrencia y no se cobra nada (quien llama lo avisa).
    /// </summary>
    public async Task<Resultado> CobrarAsync(int id, FormaPago formaPago, decimal? efectivo, decimal? tarjeta, int? administradorId = null,
        PreciosVisita? precios = null, CancellationToken ct = default)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        var ids = visita.Productos.Where(p => p.DescuentaStock).Select(p => p.ProductoId).Distinct().ToList();
        IReadOnlyList<Producto> existencias = ids.Count == 0 ? [] : await productos.ObtenerVariosAsync(ids, ct);
        var cita = visita.ReservaId is { } reservaId ? await visitas.CitaAsync(reservaId, ct) : null;

        return await Resultado.ProtegerAsync(async () =>
        {
            if (precios is not null)
                visita.CambiarPrecios(precios.Servicios, precios.Productos);
            var movimientos = visita.Cobrar(formaPago, efectivo, tarjeta, existencias, reloj.GetLocalNow().DateTime, administradorId);
            productos.AgregarMovimientos(movimientos);
            if (cita is not null)
                Completar(cita, administradorId);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>
    /// Productos gastados en el servicio de una visita cobrada (movimientos de uso enlazados a ella). Las visitas cobradas
    /// antes de poner los productos usados en la visita solo los tienen ahí.
    /// </summary>
    public Task<IReadOnlyList<MovimientoStock>> UsadosAsync(int id, CancellationToken ct = default) => productos.UsadosEnVisitaAsync(id, ct);

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

    /// <summary>
    /// Un cambio en una visita pendiente: primero los precios escritos en pantalla (para no perderlos al añadir o quitar
    /// algo) y después el cambio. Se guarda todo a la vez, o nada.
    /// </summary>
    private async Task<Resultado> EditarAsync(int id, PreciosVisita? precios, Func<Visita, Task> cambio, CancellationToken ct)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return Error.NoEncontrado("La visita");

        return await Resultado.ProtegerAsync(async () =>
        {
            if (precios is not null)
                visita.CambiarPrecios(precios.Servicios, precios.Productos);
            await cambio(visita);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    /// <summary>
    /// La ficha de una cita dada en el salón sin ella (solo con un nombre y, quizá, un teléfono): la única con ese teléfono o,
    /// si no la hay, una nueva por completar con los datos de la cita, como las que crea la web. La cita queda vinculada.
    /// </summary>
    private async Task<Domain.Clientes.Cliente> FichaParaCitaAsync(ReservaCita cita, CancellationToken ct)
    {
        var mismoTelefono = string.IsNullOrWhiteSpace(cita.Telefono) ? [] : await clientes.PorTelefonoAsync(cita.Telefono, ct);
        var ficha = mismoTelefono.Count == 1 ? mismoTelefono[0] : null;
        if (ficha is null)
        {
            ficha = Domain.Clientes.Cliente.CrearDesdeReserva(string.IsNullOrWhiteSpace(cita.Nombre) ? "Cliente de la agenda" : cita.Nombre,
                cita.Correo, cita.Telefono, reloj.GetUtcNow().UtcDateTime, cita.Apellido, cita.Sexo);
            clientes.Agregar(ficha);
        }

        cita.Cliente = ficha;
        cita.ActualizadaUtc = reloj.GetUtcNow().UtcDateTime;
        return ficha;
    }

    /// <summary>Con su visita cobrada, la cita confirmada de la agenda queda completada.</summary>
    private void Completar(ReservaCita cita, int? administradorId)
    {
        if (cita.Estado != EstadoCita.Confirmada)
            return;
        cita.Estado = EstadoCita.Completada;
        cita.AdministradorId = administradorId ?? cita.AdministradorId;
        cita.ActualizadaUtc = reloj.GetUtcNow().UtcDateTime;
    }
}
