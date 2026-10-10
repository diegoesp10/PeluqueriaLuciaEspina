using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class AgendaController(IAgendaService agenda, ISmsCitas sms) : AdminController
{
    [HttpGet("/admin/agenda")]
    public async Task<IActionResult> Index(DateOnly? dia, CancellationToken ct)
    {
        var d = dia is null || dia < agenda.Hoy.AddDays(-365) || dia > agenda.Hoy.AddDays(365) ? agenda.Hoy : dia.Value;
        var citas=(await agenda.ListarAsync(d, d.AddDays(6), ct)).Concat(await agenda.PendientesAsync(ct)).DistinctBy(x=>x.Id).OrderBy(x=>x.Inicio).ToList();
        return View(new AgendaPanel(d, citas, await agenda.MensajesAsync(ct), await sms.EntrantesAsync(ct), sms.Activo, await agenda.AvisosAsync(ct)));
    }
    [HttpGet("/admin/agenda/avisos"), ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> Avisos(CancellationToken ct) => Json(await agenda.AvisosAsync(ct));
    [HttpPost("/admin/agenda/gestionar")]
    public async Task<IActionResult> Gestionar(Guid id, string version, EstadoCita estado, int minutos, bool telefonoValidado, string? nota, DateOnly? dia, CancellationToken ct)
    {
        if (!ModelState.IsValid) this.AvisarError("Revisa los datos de la reserva.");
        else Avisar(await agenda.GestionarAsync(new(id,version,estado,minutos,telefonoValidado,nota,AdministradorId),ct), "Reserva actualizada. Revisa el aviso al cliente en Comunicaciones.");
        return RedirectToAction(nameof(Index),new { dia=dia?.ToString("yyyy-MM-dd") });
    }
    [HttpGet("/admin/agenda/horarios")]
    public async Task<IActionResult> Horarios(CancellationToken ct) => View(new HorariosPagina(await agenda.HorariosAsync(ct),await agenda.CierresAsync(ct),agenda.Hoy));
    [HttpPost("/admin/agenda/horarios")]
    public async Task<IActionResult> Horario(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct)
    { if (ModelState.IsValid) Avisar(await agenda.GuardarHorarioAsync(id,dia,desde,hasta,ct),"Horario guardado."); else this.AvisarError("Revisa las horas."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/eliminar-horario")]
    public async Task<IActionResult> EliminarHorario(int id,CancellationToken ct) { Avisar(await agenda.EliminarHorarioAsync(id,ct),"Tramo eliminado."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/cerrar-dia")]
    public async Task<IActionResult> CerrarDia(DateOnly dia,string? motivo,CancellationToken ct) { if(ModelState.IsValid) Avisar(await agenda.CerrarDiaAsync(dia,motivo??"",ct),"Día cerrado."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/abrir-dia")]
    public async Task<IActionResult> AbrirDia(int id,CancellationToken ct) { Avisar(await agenda.AbrirDiaAsync(id,ct),"Día reabierto."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/sms-manual")]
    public async Task<IActionResult> SmsManual(Guid id,CancellationToken ct) { Avisar(await agenda.MarcarSmsManualAsync(id,ct),"Aviso marcado como comunicado personalmente."); return RedirectToAction(nameof(Index)); }
    private void Avisar(Resultado r,string mensaje) { if(r.Exito) this.AvisarExito(mensaje); else this.AvisarError(r.Error!.Mensaje); }
}
