using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class AgendaController(IAgendaService agenda, IComunicacionesCitas comunicaciones, ClientesAgendaService clientesAgenda) : AdminController
{
    [HttpGet("/admin/agenda")]
    public async Task<IActionResult> Index(DateOnly? dia, CancellationToken ct)
    {
        var d = dia is null || dia < agenda.Hoy.AddDays(-365) || dia > agenda.Hoy.AddDays(365) ? agenda.Hoy : dia.Value;
        var citas=(await agenda.ListarAsync(d, d.AddDays(6), ct)).Concat(await agenda.PendientesAsync(ct)).DistinctBy(x=>x.Id).OrderBy(x=>x.Inicio).ToList();
        ViewData["ClientesPendientes"] = await clientesAgenda.PendientesAsync(ct);
        var conflictos = await clientesAgenda.ConflictosAsync(ct);
        ViewData["ConflictosClientes"] = conflictos;
        var candidatos = new Dictionary<Guid, List<PeluqueriaLucia.Domain.Clientes.Cliente>>();
        foreach(var conflicto in conflictos) candidatos[conflicto.Id] = await clientesAgenda.CandidatosAsync(conflicto,ct);
        ViewData["CandidatosClientes"] = candidatos;
        return View(new AgendaPanel(d, citas, await comunicaciones.ListarAsync(ct), comunicaciones.CorreoConfigurado, comunicaciones.EnlaceConfigurado, await agenda.AvisosAsync(ct)));
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
    public async Task<IActionResult> Horario(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct, int intervaloMinutos = 15)
    { if (ModelState.IsValid) Avisar(await agenda.GuardarHorarioAsync(id,dia,desde,hasta,ct,intervaloMinutos),"Horario guardado."); else this.AvisarError("Revisa las horas."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/eliminar-horario")]
    public async Task<IActionResult> EliminarHorario(int id,CancellationToken ct) { Avisar(await agenda.EliminarHorarioAsync(id,ct),"Tramo eliminado."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/cerrar-dia")]
    public async Task<IActionResult> CerrarDia(DateOnly dia,string? motivo,CancellationToken ct) { if(ModelState.IsValid) Avisar(await agenda.CerrarDiaAsync(dia,motivo??"",ct),"Día cerrado."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/abrir-dia")]
    public async Task<IActionResult> AbrirDia(int id,CancellationToken ct) { Avisar(await agenda.AbrirDiaAsync(id,ct),"Día reabierto."); return RedirectToAction(nameof(Horarios)); }
    [HttpPost("/admin/agenda/whatsapp-enviado")]
    public async Task<IActionResult> WhatsAppEnviado(Guid id,CancellationToken ct) { Avisar(await comunicaciones.MarcarWhatsAppAsync(id,ct),"Mensaje marcado como enviado. La cita sigue pendiente de aceptación del cliente."); return RedirectToAction(nameof(Index)); }
    [HttpPost("/admin/agenda/reintentar-correo")]
    public async Task<IActionResult> ReintentarCorreo(Guid id,CancellationToken ct) { Avisar(await comunicaciones.ReintentarCorreoAsync(id,ct),"Correo preparado para reintentar."); return RedirectToAction(nameof(Index)); }
    [HttpPost("/admin/agenda/falta")]
    public async Task<IActionResult> Falta(Guid id, string version, string? nota, CancellationToken ct)
    { if (ModelState.IsValid) Avisar(await clientesAgenda.RegistrarFaltaAsync(id,version,nota,AdministradorId,ct),"Falta registrada en el historial del cliente."); else this.AvisarError("Revisa los datos."); return RedirectToAction(nameof(Index)); }
    [HttpPost("/admin/agenda/corregir-falta")]
    public async Task<IActionResult> CorregirFalta(int id, string? motivo, int clienteId, CancellationToken ct)
    { if (ModelState.IsValid) Avisar(await clientesAgenda.CorregirFaltaAsync(id,motivo,AdministradorId,ct),"Falta corregida. Se conserva el registro de la rectificación."); else this.AvisarError("Revisa los datos."); return RedirectToAction("Detalle","Clientes",new { id=clienteId }); }
    [HttpPost("/admin/agenda/vincular-cliente")]
    public async Task<IActionResult> VincularCliente(Guid id, string version, int clienteId, bool contactoVerificado, CancellationToken ct)
    { if (ModelState.IsValid) Avisar(await clientesAgenda.ResolverAsync(id,version,clienteId,contactoVerificado,AdministradorId,ct),"Cita vinculada a la ficha revisada."); else this.AvisarError("Revisa los datos."); return RedirectToAction(nameof(Index)); }
    private void Avisar(Resultado r,string mensaje) { if(r.Exito) this.AvisarExito(mensaje); else this.AvisarError(r.Error!.Mensaje); }
}
