using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Web.Models;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous, EnableRateLimiting(LimitesDePeticiones.Publico), ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class AgendaController(IAgendaService agenda) : Controller
{
    [HttpGet("/citas")]
    public async Task<IActionResult> Index(DateOnly? dia, int? servicioId, CancellationToken ct) =>
        View(await PaginaAsync(new ReservaFormulario { Dia = Limitar(dia), ServicioId=servicioId }, ct));
    [HttpGet("/citas/disponibilidad")]
    public async Task<IActionResult> Disponibilidad(DateOnly dia, int? servicioId, CancellationToken ct) => Json(await agenda.DisponibilidadAsync(Limitar(dia), servicioId, ct));
    [HttpPost("/citas"), EnableRateLimiting(LimitesDePeticiones.Reservas), RequestSizeLimit(16384)]
    public async Task<IActionResult> Index([Bind(Prefix="Formulario")] ReservaFormulario formulario, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(formulario.Web)) return BadRequest();
        if (formulario.Dia < agenda.Hoy || formulario.Dia > agenda.Hoy.AddDays(90)) ModelState.AddModelError("Formulario.Dia", "Selecciona un día dentro de los próximos 90 días.");
        if (ModelState.IsValid)
        {
            var r = await agenda.SolicitarAsync(new(formulario.SolicitudId, formulario.Dia, formulario.Hora!.Value, formulario.Telefono, formulario.Nombre, formulario.ServicioId, IpCliente.De(HttpContext)), ct);
            if (r.Exito) { TempData["cita-solicitada"] = true; return RedirectToAction(nameof(Recibida)); }
            ModelState.AddModelError(r.Error!.Campo is null ? "" : "Formulario." + r.Error.Campo, r.Error.Mensaje);
        }
        Response.StatusCode=400;
        return View(await PaginaAsync(formulario, ct));
    }
    [HttpGet("/citas/recibida")]
    public IActionResult Recibida() => TempData["cita-solicitada"] is true ? View() : RedirectToAction(nameof(Index));
    private DateOnly Limitar(DateOnly? dia) => dia is null || dia < agenda.Hoy || dia > agenda.Hoy.AddDays(90) ? agenda.Hoy : dia.Value;
    private async Task<ReservaPagina> PaginaAsync(ReservaFormulario f, CancellationToken ct) => new(f, await agenda.ServiciosAsync(ct), await agenda.DisponibilidadAsync(Limitar(f.Dia), f.ServicioId, ct), agenda.Hoy);
}
