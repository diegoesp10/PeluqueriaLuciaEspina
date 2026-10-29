using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Infraestructura.Impresion;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class VisitasController(
    VisitasService visitas,
    ClientesService clientes,
    CatalogoService catalogo,
    ProductosService productos,
    PeluqueriaLucia.Application.Publico.ResenasService resenas,
    TimeProvider reloj,
    IOptions<DatosNegocio> negocio,
    IOptions<ImpresoraTickets> impresora,
    IWebHostEnvironment entorno) : AdminController
{
    private const string PreciosServicios = nameof(VisitaFormulario.PreciosServicios) + "[";
    private const string PreciosProductos = nameof(VisitaFormulario.PreciosProductos) + "[";

    private DateTime Ahora => reloj.GetLocalNow().DateTime;

    private DateOnly Hoy => DateOnly.FromDateTime(Ahora);

    [HttpGet]
    public async Task<IActionResult> Index(Periodo periodo = Periodo.Dia, DateOnly? fecha = null, int pagina = 1, CancellationToken ct = default)
    {
        var dia = fecha ?? Hoy;
        var filtro = VisitasIndiceModelo.Filtro(periodo, dia);

        // Secuencial a propósito: el contexto de base de datos no admite consultas simultáneas
        var listado = await visitas.ListarAsync(filtro, pagina, ct);
        var resumen = await visitas.ResumirAsync(filtro, ct);
        var porCobrar = periodo == Periodo.Dia ? await visitas.CitasPorCobrarAsync(dia, ct) : [];
        return View(new VisitasIndiceModelo(periodo, dia, Hoy, listado, resumen, porCobrar));
    }

    [HttpGet]
    public async Task<IActionResult> Exportar(Periodo periodo = Periodo.Dia, DateOnly? fecha = null, CancellationToken ct = default)
    {
        var dia = fecha ?? Hoy;
        var resultado = await visitas.ExportarAsync(VisitasIndiceModelo.Filtro(periodo, dia), ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Index), new { periodo, fecha = dia.ToString("yyyy-MM-dd") });
        }

        var archivo = resultado.Valor!;
        return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
    }

    [HttpGet]
    public async Task<IActionResult> Nueva(int? clienteId, CancellationToken ct)
    {
        var formulario = new NuevaVisitaFormulario
        {
            ClienteId = clienteId,
            Fecha = Ahora.AddSeconds(-Ahora.Second).AddMilliseconds(-Ahora.Millisecond),
        };
        return View(await ModeloNuevaAsync(formulario, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Nueva(NuevaVisitaFormulario formulario, CancellationToken ct)
    {
        // Las filas de producto sin producto elegido (quitadas a medias) no cuentan
        formulario.Productos.RemoveAll(p => p.ProductoId <= 0);
        if (formulario.Servicios.Count == 0 && formulario.Productos.Count == 0)
            ModelState.AddModelError(nameof(formulario.Servicios), "Elige al menos un servicio o un producto.");
        // Unidades o precios no válidos en una fila: el mensaje va debajo de la lista de productos
        foreach (var mensaje in ModelState.Where(e => e.Key.StartsWith("Productos[", StringComparison.Ordinal))
                     .SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage).Distinct().ToList())
            ModelState.AddModelError(nameof(formulario.Productos), mensaje);

        if (ModelState.IsValid)
        {
            var datos = new NuevaVisita(formulario.ClienteId!.Value, formulario.Fecha!.Value, formulario.FormaPago, formulario.Servicios, formulario.Precios,
                [.. formulario.Productos.Select(p => new ProductoNuevaVisita(p.ProductoId, p.Cantidad, p.Precio, p.Uso))]);
            var resultado = await visitas.RegistrarAsync(datos, ct);
            if (resultado.Exito)
            {
                if (formulario.Accion == "cobrar")
                    return RedirectToAction(nameof(Detalle), new { id = resultado.Valor });

                this.AvisarExito("Visita guardada. Queda pendiente de cobro.");
                return RedirectToAction(nameof(Index), new { fecha = formulario.Fecha.Value.ToString("yyyy-MM-dd") });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View(await ModeloNuevaAsync(formulario, ct));
    }

    /// <summary>
    /// «Cobrar» en una cita confirmada de la agenda: abre su visita, con el servicio de la cita, para añadir lo que haga falta
    /// y cobrarla. Si ya la tenía (o el cliente tiene una visita ese día registrada aparte), abre esa.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DesdeCita(Guid reservaId, string? volver, CancellationToken ct)
    {
        IActionResult Volver() => Url.IsLocalUrl(volver) ? LocalRedirect(volver) : Redirect("/admin/agenda");
        Resultado<int> resultado;
        try
        {
            resultado = await visitas.AbrirDesdeCitaAsync(reservaId, ct);
        }
        catch (DbUpdateException)
        {
            // Dos «Cobrar» a la vez en la misma cita: una cita solo tiene una visita y la otra petición ya la ha abierto
            this.AvisarError("La visita de esta cita se acaba de abrir en otra pestaña. Vuelve a pulsar «Cobrar».");
            return Volver();
        }

        if (resultado.Exito)
            return RedirectToAction(nameof(Detalle), new { id = resultado.Valor });
        this.AvisarError(resultado.Error!.Mensaje);
        return Volver();
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        return View(await ModeloDetalleAsync(visita, new VisitaFormulario { FormaPago = visita.FormaPago }, ct));
    }

    /// <summary>Guarda los precios escritos sin cobrar (la visita sigue pendiente).</summary>
    [HttpPost]
    public Task<IActionResult> Guardar(int id, VisitaFormulario formulario, CancellationToken ct) =>
        EditarAsync(id, formulario, precios => visitas.GuardarPreciosAsync(id, precios, ct), "Precios guardados. La visita sigue pendiente de cobro.", null, ct);

    [HttpPost]
    public Task<IActionResult> AgregarServicio(int id, VisitaFormulario formulario, CancellationToken ct)
    {
        if (formulario.ServicioId is null)
            ModelState.AddModelError(nameof(formulario.ServicioId), "Elige un servicio de la lista.");
        return EditarAsync(id, formulario, precios => visitas.AgregarServicioAsync(id, formulario.ServicioId!.Value, formulario.PrecioServicio, precios, ct),
            "Servicio añadido a la visita.", "servicio", ct, nameof(formulario.ServicioId), nameof(formulario.PrecioServicio));
    }

    [HttpPost]
    public Task<IActionResult> QuitarServicio(int id, int lineaId, VisitaFormulario formulario, CancellationToken ct) =>
        EditarAsync(id, formulario, precios => visitas.QuitarServicioAsync(id, lineaId, precios, ct), "Servicio quitado de la visita.", null, ct);

    [HttpPost]
    public Task<IActionResult> AgregarProducto(int id, VisitaFormulario formulario, CancellationToken ct)
    {
        if (formulario.ProductoId is null)
            ModelState.AddModelError(nameof(formulario.ProductoId), "Elige un producto de la lista.");
        if (formulario.Cantidad is < 1 or > LineaProductoVisita.CantidadMaxima)
            ModelState.AddModelError(nameof(formulario.Cantidad), $"Indica entre 1 y {LineaProductoVisita.CantidadMaxima} unidades.");
        if (formulario.Uso is null)
            ModelState.AddModelError(nameof(formulario.Terminado), "¿Se ha terminado el producto usado en el servicio? Si se ha terminado sale del stock; si no, sigue en uso.");
        return EditarAsync(id, formulario,
            precios => visitas.AgregarProductoAsync(id, formulario.ProductoId!.Value, formulario.Cantidad, formulario.Precio, formulario.Uso!.Value, precios, ct),
            formulario.EnServicio ? "Producto usado en el servicio añadido a la visita." : "Producto añadido a la visita.", "producto", ct,
            nameof(formulario.ProductoId), nameof(formulario.Cantidad), nameof(formulario.Precio), nameof(formulario.EnServicio), nameof(formulario.Terminado));
    }

    [HttpPost]
    public Task<IActionResult> QuitarProducto(int id, int lineaId, VisitaFormulario formulario, CancellationToken ct) =>
        EditarAsync(id, formulario, precios => visitas.QuitarProductoAsync(id, lineaId, precios, ct), "Producto quitado de la visita.", null, ct);

    /// <summary>Cobra la visita con los precios que hay escritos en pantalla en ese momento.</summary>
    [HttpPost]
    public async Task<IActionResult> Cobrar(int id, VisitaFormulario formulario, CancellationToken ct)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        var precios = LeerPrecios(formulario, visita, nameof(formulario.FormaPago), nameof(formulario.Efectivo), nameof(formulario.Tarjeta));
        if (precios is not null)
        {
            try
            {
                var resultado = await visitas.CobrarAsync(id, formulario.FormaPago, formulario.Efectivo, formulario.Tarjeta, AdministradorId, precios, ct);
                if (resultado.Exito)
                {
                    this.AvisarExito("Visita cobrada.");
                    return RedirectToAction(nameof(Ticket), new { id });
                }

                ModelState.AgregarError(resultado.Error!);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otro cobro ha vendido a la vez alguno de los productos, o la cita ha cambiado en la agenda: no se ha cobrado
                // nada. Se vuelve a cargar la visita en otra petición, porque la de esta tiene en memoria el cobro sin guardar.
                this.AvisarError("El stock de algún producto o la cita acaban de cambiar. No se ha cobrado nada: revisa la visita y vuelve a cobrar.");
                return RedirectToAction(nameof(Detalle), new { id });
            }
        }

        return View(nameof(Detalle), await ModeloDetalleAsync(visita, formulario, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var fecha = (await visitas.ObtenerAsync(id, ct))?.Fecha;
        var resultado = await visitas.EliminarAsync(id, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return fecha is null ? RedirectToAction(nameof(Index)) : RedirectToAction(nameof(Detalle), new { id });
        }

        this.AvisarExito("Visita eliminada.");
        return RedirectToAction(nameof(Index), new { fecha = fecha?.ToString("yyyy-MM-dd") });
    }

    [HttpGet]
    public async Task<IActionResult> Ticket(int id, CancellationToken ct)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        return visita is null ? NotFound() : View(new TicketModelo(visita, negocio.Value, await resenas.CodigoTicketAsync(id, ct), impresora.Value));
    }

    /// <summary>
    /// El ticket en ESC/POS para la impresora térmica. El navegador lo descarga y lo manda por USB a la impresora con
    /// WebUSB (ver ticket.js), así que se imprime igual con la web publicada en Azure.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TicketTermico(int id, CancellationToken ct)
    {
        if (!impresora.Value.Configurada)
            return NotFound();
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        var modelo = new TicketModelo(visita, negocio.Value, await resenas.CodigoTicketAsync(id, ct), impresora.Value);
        var logo = impresora.Value.Logo is { Length: > 0 } ruta ? entorno.WebRootFileProvider.GetFileInfo(ruta) : null;
        await using var imagen = logo is { Exists: true, IsDirectory: false } ? logo.CreateReadStream() : null;
        return File(TicketEscPos.Construir(modelo, impresora.Value, imagen), "application/octet-stream");
    }

    /// <summary>
    /// Un cambio en una visita pendiente desde su pantalla: guarda los precios escritos y hace el cambio, todo a la vez. Si algo
    /// no vale, vuelve a la visita con lo escrito y el error; <paramref name="abierto"/> dice qué se estaba añadiendo.
    /// </summary>
    private async Task<IActionResult> EditarAsync(int id, VisitaFormulario formulario, Func<PreciosVisita, Task<Resultado>> cambio, string exito,
        string? abierto, CancellationToken ct, params string[] campos)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        if (LeerPrecios(formulario, visita, campos) is { } precios)
        {
            var resultado = await cambio(precios);
            if (resultado.Exito)
            {
                this.AvisarExito(exito);
                return RedirectToAction(nameof(Detalle), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View(nameof(Detalle), await ModeloDetalleAsync(visita, formulario, ct, abierto));
    }

    /// <summary>
    /// Los precios escritos en pantalla, o null si alguno no vale (con su error). Antes quita de ModelState lo que no es de
    /// esta acción ni un precio: por ejemplo, unidades a medio escribir para añadir un producto no impiden cobrar.
    /// </summary>
    private PreciosVisita? LeerPrecios(VisitaFormulario formulario, Visita visita, params string[] campos)
    {
        foreach (var clave in ModelState.Keys.Where(c => !c.StartsWith(PreciosServicios, StringComparison.Ordinal)
                     && !c.StartsWith(PreciosProductos, StringComparison.Ordinal) && !campos.Contains(c)).ToList())
            ModelState.Remove(clave);

        // Un mensaje por precio, con el nombre de lo que se cobra
        foreach (var linea in visita.Lineas)
            ComprobarPrecio(PreciosServicios, linea.Id, linea.Nombre, formulario.PreciosServicios);
        foreach (var linea in visita.Productos.Where(p => !p.Incluido))
            ComprobarPrecio(PreciosProductos, linea.Id, linea.Nombre, formulario.PreciosProductos);
        if (!ModelState.IsValid)
            return null;

        return new PreciosVisita(
            formulario.PreciosServicios.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value!.Value),
            formulario.PreciosProductos.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value!.Value));
    }

    private void ComprobarPrecio(string prefijo, int lineaId, string nombre, Dictionary<int, decimal?> precios)
    {
        var clave = $"{prefijo}{lineaId}]";
        var noVale = ModelState.TryGetValue(clave, out var entrada) && entrada.Errors.Count > 0;
        if (noVale)
            entrada!.Errors.Clear();
        if (noVale || (precios.TryGetValue(lineaId, out var precio) && precio is null))
            ModelState.AddModelError(clave, $"Escribe el precio de «{nombre}», por ejemplo 12,50.");
    }

    private async Task<NuevaVisitaModelo> ModeloNuevaAsync(NuevaVisitaFormulario formulario, CancellationToken ct)
    {
        var cliente = formulario.ClienteId is { } id ? await clientes.ObtenerAsync(id, ct) : null;
        var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
        return new NuevaVisitaModelo(formulario, cliente, servicios, await productos.ParaVentaAsync(ct));
    }

    private async Task<VisitaDetalleModelo> ModeloDetalleAsync(Visita visita, VisitaFormulario formulario, CancellationToken ct, string? abierto = null)
    {
        // Secuencial: el contexto de base de datos no admite consultas simultáneas
        var cita = visita.ReservaId is { } reservaId ? await visitas.CitaAsync(reservaId, ct) : null;
        if (visita.EstaCobrada)
            return new(visita, formulario, [], [], cita, await visitas.UsadosAsync(visita.Id, ct));

        var paraVenta = await productos.ParaVentaAsync(ct);
        var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
        return new(visita, formulario, paraVenta, servicios, cita, Abierto: abierto);
    }
}
