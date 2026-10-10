using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class VisitasController(
    VisitasService visitas,
    ClientesService clientes,
    CatalogoService catalogo,
    PeluqueriaLucia.Application.Publico.ResenasService resenas,
    TimeProvider reloj,
    IOptions<DatosNegocio> negocio) : AdminController
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
        if (formulario.Servicios.Count == 0)
            ModelState.AddModelError(nameof(formulario.Servicios), "Elige al menos un servicio.");

        if (ModelState.IsValid)
        {
            var datos = new NuevaVisita(formulario.ClienteId!.Value, formulario.Fecha!.Value, formulario.FormaPago, formulario.Servicios);
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

        return View(new VisitaDetalleModelo(visita, new CobroFormulario { FormaPago = visita.FormaPago }));
    }

    [HttpPost]
    public async Task<IActionResult> Cobrar(int id, CobroFormulario cobro, CancellationToken ct)
    {
        var resultado = await visitas.CobrarAsync(id, cobro.FormaPago, cobro.Efectivo, cobro.Tarjeta, ct);
        if (resultado.Exito)
        {
            this.AvisarExito("Visita cobrada.");
            return RedirectToAction(nameof(Ticket), new { id });
        }

        var visita = await visitas.ObtenerAsync(id, ct);
        if (visita is null)
            return NotFound();

        ModelState.AgregarError(resultado.Error!);
        return View(nameof(Detalle), new VisitaDetalleModelo(visita, cobro));
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
        return visita is null ? NotFound() : View(new TicketModelo(visita, negocio.Value, await resenas.CodigoTicketAsync(id, ct)));
    }

    private async Task<NuevaVisitaModelo> ModeloNuevaAsync(NuevaVisitaFormulario formulario, CancellationToken ct)
    {
        var cliente = formulario.ClienteId is { } id ? await clientes.ObtenerAsync(id, ct) : null;
        var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
        return new NuevaVisitaModelo(formulario, cliente, servicios);
    }
}
