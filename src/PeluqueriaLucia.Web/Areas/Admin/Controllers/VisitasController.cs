using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Visitas;
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
        return View(new VisitasIndiceModelo(periodo, dia, Hoy, listado, resumen));
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
                [.. formulario.Productos.Select(p => new ProductoNuevaVisita(p.ProductoId, p.Cantidad, p.Precio))]);
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

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        return View(await ModeloDetalleAsync(visita, new CobroFormulario { FormaPago = visita.FormaPago }, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Cobrar(int id, CobroFormulario cobro, CancellationToken ct)
    {
        try
        {
            // Las filas sin producto elegido (quitadas a medias) no cuentan
            cobro.Usados.RemoveAll(u => u.ProductoId <= 0);
            // Unidades no válidas en una fila: un solo mensaje, debajo de la lista de productos utilizados
            var filas = ModelState.Where(e => e.Key.StartsWith("Usados[", StringComparison.Ordinal)).ToList();
            var invalidas = filas.SelectMany(e => e.Value!.Errors).Select(e => e.ErrorMessage).Distinct().ToList();
            if (invalidas.Count > 0)
            {
                filas.ForEach(e => ModelState.Remove(e.Key));
                invalidas.ForEach(m => ModelState.AddModelError(nameof(cobro.Usados), m));
                var actual = await visitas.ObtenerAsync(id, ct);
                return actual is null ? NotFound() : View(nameof(Detalle), await ModeloDetalleAsync(actual, cobro, ct));
            }

            var usados = cobro.Usados.Select(u => new ProductoUsadoCobro(u.ProductoId, u.Cantidad)).ToList();
            var resultado = await visitas.CobrarAsync(id, cobro.FormaPago, cobro.Efectivo, cobro.Tarjeta, AdministradorId, usados, ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Visita cobrada.");
                return RedirectToAction(nameof(Ticket), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro cobro ha vendido a la vez alguno de los productos: no se ha cobrado nada. Se vuelve a cargar la visita
            // en otra petición, porque la de esta tiene en memoria el cobro que no se ha guardado.
            this.AvisarError("El stock de algún producto acaba de cambiar con otro cobro. No se ha cobrado nada: revisa la visita y vuelve a cobrar.");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        return View(nameof(Detalle), await ModeloDetalleAsync(visita, cobro, ct));
    }

    [HttpPost]
    public async Task<IActionResult> AgregarProducto(int id, AgregarProductoFormulario agregar, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await visitas.AgregarProductoAsync(id, agregar.ProductoId!.Value, agregar.Cantidad, agregar.Precio, ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Producto añadido a la visita.");
                return RedirectToAction(nameof(Detalle), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        return View(nameof(Detalle), await ModeloDetalleAsync(visita, new CobroFormulario { FormaPago = visita.FormaPago }, ct, agregar));
    }

    [HttpPost]
    public async Task<IActionResult> QuitarProducto(int id, int lineaId, CancellationToken ct)
    {
        var resultado = await visitas.QuitarProductoAsync(id, lineaId, ct);
        if (resultado.Exito)
            this.AvisarExito("Producto quitado de la visita.");
        else
            this.AvisarError(resultado.Error!.Mensaje);

        return RedirectToAction(nameof(Detalle), new { id });
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

    private async Task<NuevaVisitaModelo> ModeloNuevaAsync(NuevaVisitaFormulario formulario, CancellationToken ct)
    {
        var cliente = formulario.ClienteId is { } id ? await clientes.ObtenerAsync(id, ct) : null;
        var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
        return new NuevaVisitaModelo(formulario, cliente, servicios, await productos.ParaVentaAsync(ct));
    }

    private async Task<VisitaDetalleModelo> ModeloDetalleAsync(Domain.Visitas.Visita visita, CobroFormulario cobro, CancellationToken ct, AgregarProductoFormulario? agregar = null) =>
        visita.EstaCobrada
            ? new(visita, cobro, [], agregar, await visitas.UsadosAsync(visita.Id, ct))
            : new(visita, cobro, await productos.ParaVentaAsync(ct), agregar);
}
