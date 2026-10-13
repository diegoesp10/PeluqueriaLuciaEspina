using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Web.Models;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous, EnableRateLimiting(LimitesDePeticiones.Publico), ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class AgendaController(IAgendaService agenda, IComunicacionesCitas comunicaciones, VerificacionContactoService verificacion, Textos textos) : Controller
{
    [HttpGet("/citas")]
    public async Task<IActionResult> Index(DateOnly? dia, int? servicioId, CancellationToken ct)
    {
        ViewData["ContactoVerificable"] = verificacion.Disponible;
        return View(await PaginaAsync(new ReservaFormulario { Dia = Limitar(dia), ServicioId=servicioId }, ct));
    }
    [HttpGet("/citas/disponibilidad")]
    public async Task<IActionResult> Disponibilidad(DateOnly dia, int? servicioId, CancellationToken ct) => Json(await agenda.DisponibilidadAsync(Limitar(dia), servicioId, ct));
    [HttpPost("/citas"), EnableRateLimiting(LimitesDePeticiones.Reservas), RequestSizeLimit(16384)]
    public async Task<IActionResult> Index([Bind(Prefix="Formulario")] ReservaFormulario formulario, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(formulario.Web)) return BadRequest();
        if (formulario.Dia < agenda.Hoy || formulario.Dia > agenda.Hoy.AddDays(90)) ModelState.AddModelError("Formulario.Dia", textos["Selecciona un día dentro de los próximos 90 días."]);
        if (ModelState.IsValid)
        {
            var r = await agenda.SolicitarAsync(new(formulario.SolicitudId, formulario.Dia, formulario.Hora!.Value, formulario.Telefono, formulario.Nombre, formulario.ServicioId, IpCliente.De(HttpContext), formulario.Correo, formulario.Canal!.Value, formulario.Apellido, formulario.Sexo), ct);
            if (r.Exito) { TempData["cita-solicitada"] = true; return RedirectToAction(nameof(Recibida)); }
            ModelState.AddModelError(r.Error!.Campo is null ? "" : "Formulario." + r.Error.Campo, textos[r.Error.Mensaje]);
        }
        Response.StatusCode=400;
        ViewData["ContactoVerificable"] = verificacion.Disponible;
        return View(await PaginaAsync(formulario, ct));
    }
    [HttpGet("/citas/recibida")]
    public IActionResult Recibida() => TempData["cita-solicitada"] is true ? View() : RedirectToAction(nameof(Index));
    [HttpGet("/citas/confirmar/{token}"), ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> Confirmar(string token, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var propuesta = await comunicaciones.ConsultarPropuestaAsync(token, ct);
        if (propuesta is null) Response.StatusCode = 410;
        return View(new AceptacionPagina(token, propuesta));
    }
    [HttpPost("/citas/confirmar/{token}"), RequestSizeLimit(4096)]
    public async Task<IActionResult> Aceptar(string token, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var resultado = await comunicaciones.AceptarAsync(token, ct);
        if (resultado.Exito) { TempData["cita-aceptada"] = true; return RedirectToAction(nameof(Aceptada)); }
        ModelState.AddModelError("", textos[resultado.Error!.Mensaje]); Response.StatusCode = 410;
        return View("Confirmar", new AceptacionPagina(token, null));
    }
    [HttpGet("/citas/aceptada")]
    public IActionResult Aceptada() => TempData["cita-aceptada"] is true ? View() : RedirectToAction(nameof(Index));
    [HttpPost("/citas/contacto/solicitar"), EnableRateLimiting(LimitesDePeticiones.ContactoPublico), RequestSizeLimit(4096)]
    public async Task<IActionResult> SolicitarContacto(string? telefono, CancellationToken ct) =>
        Json(new { id = await verificacion.SolicitarAsync(telefono,IpCliente.De(HttpContext),ct) });
    [HttpPost("/citas/contacto/verificar"), EnableRateLimiting(LimitesDePeticiones.VerificacionContacto), RequestSizeLimit(4096)]
    public async Task<IActionResult> VerificarContacto(Guid id, string? codigo, CancellationToken ct)
    {
        var r=await verificacion.VerificarAsync(id,codigo,ct);
        return r.Exito ? Json(r.Valor) : BadRequest(new { mensaje=textos[r.Error!.Mensaje] });
    }
    private DateOnly Limitar(DateOnly? dia) => dia is null || dia < agenda.Hoy || dia > agenda.Hoy.AddDays(90) ? agenda.Hoy : dia.Value;
    private async Task<ReservaPagina> PaginaAsync(ReservaFormulario f, CancellationToken ct) => new(f, await ServiciosEnIdiomaAsync(ct), await agenda.DisponibilidadAsync(Limitar(f.Dia), f.ServicioId, ct), agenda.Hoy);

    /// <summary>Servicios con el nombre en el idioma de la página (en inglés si está traducido; si no, el español).</summary>
    private async Task<IReadOnlyList<ServicioAgenda>> ServiciosEnIdiomaAsync(CancellationToken ct)
    {
        var ingles = Idioma.EsIngles(HttpContext);
        return (await agenda.ServiciosAsync(ct)).Select(s => s with { Nombre = s.NombreEnIdioma(ingles) }).ToList();
    }
}
