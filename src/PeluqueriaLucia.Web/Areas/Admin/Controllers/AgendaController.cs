using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class AgendaController(IAgendaService agenda, ClientesAgendaService clientesAgenda, ClientesService clientes) : AdminController
{
    /// <summary>Calendario del mes con el detalle del día elegido. «estado» filtra el día: abiertas, canceladas, completadas o todas.</summary>
    [HttpGet("/admin/agenda")]
    public async Task<IActionResult> Index(DateOnly? dia, FiltroCitas estado = FiltroCitas.Abiertas, CancellationToken ct = default)
    {
        var d = dia is null || dia < agenda.Hoy.AddDays(-365) || dia > agenda.Hoy.AddDays(365) ? agenda.Hoy : dia.Value;
        if (!Enum.IsDefined(estado)) estado = FiltroCitas.Abiertas;
        var utc = DateTime.UtcNow;
        var inicioMes = new DateOnly(d.Year, d.Month, 1);
        var finMes = inicioMes.AddMonths(1).AddDays(-1);
        var citasMes = await agenda.ListarAsync(inicioMes, finMes, ct);
        var cierres = (await agenda.CierresAsync(ct)).Where(c => c.Dia >= inicioMes && c.Dia <= finMes).ToDictionary(c => c.Dia, c => c.Motivo);
        var porDia = citasMes.GroupBy(c => DateOnly.FromDateTime(c.Inicio)).ToDictionary(g => g.Key, g => g.ToList());
        var dias = new List<DiaCalendario>();
        for (var dd = inicioMes; dd <= finMes; dd = dd.AddDays(1))
        {
            var lista = porDia.GetValueOrDefault(dd, []);
            var bloqueos = lista.Count(c => c.Origen == OrigenCita.Bloqueo);
            var citas = lista.Where(c => c.Origen != OrigenCita.Bloqueo).ToList();
            dias.Add(new(dd, citas.Count(c => AgendaCalendario.GrupoDe(c, utc) == FiltroCitas.Abiertas), citas.Count(c => AgendaCalendario.GrupoDe(c, utc) == FiltroCitas.Canceladas),
                citas.Count(c => AgendaCalendario.GrupoDe(c, utc) == FiltroCitas.Completadas), bloqueos, cierres.GetValueOrDefault(dd)));
        }
        var delDia = porDia.GetValueOrDefault(d, []).Where(c => estado == FiltroCitas.Todas || c.Origen == OrigenCita.Bloqueo && estado == FiltroCitas.Abiertas || AgendaCalendario.GrupoDe(c, utc) == estado)
            .OrderBy(c => c.Inicio).ToList();
        var pendientes = (await agenda.PendientesAsync(ct)).Where(c => DateOnly.FromDateTime(c.Inicio) != d).OrderBy(c => c.Inicio).ToList();
        ViewData["ClientesPendientes"] = await clientesAgenda.PendientesAsync(ct);
        var conflictos = await clientesAgenda.ConflictosAsync(ct);
        ViewData["ConflictosClientes"] = conflictos;
        var candidatos = new Dictionary<Guid, List<PeluqueriaLucia.Domain.Clientes.Cliente>>();
        foreach(var conflicto in conflictos) candidatos[conflicto.Id] = await clientesAgenda.CandidatosAsync(conflicto,ct);
        ViewData["CandidatosClientes"] = candidatos;
        return View(new AgendaCalendario(d, agenda.Hoy, dias, delDia, pendientes, estado, await agenda.AvisosAsync(ct), utc));
    }
    [HttpPost("/admin/agenda/eliminar")]
    public async Task<IActionResult> Eliminar(Guid id, DateOnly? dia, FiltroCitas estado, CancellationToken ct)
    {
        Avisar(await agenda.EliminarCitaAsync(id, ct), "Cita eliminada de la agenda.");
        return RedirectToAction(nameof(Index), new { dia = dia?.ToString("yyyy-MM-dd"), estado });
    }
    [HttpGet("/admin/agenda/avisos"), ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
    public async Task<IActionResult> Avisos(CancellationToken ct) => Json(await agenda.AvisosAsync(ct));
    [HttpPost("/admin/agenda/gestionar")]
    public async Task<IActionResult> Gestionar(Guid id, string version, EstadoCita estado, int minutos, bool telefonoValidado, string? nota, DateOnly? dia, CancellationToken ct, bool confirmarDirecto = false)
    {
        if (!ModelState.IsValid) this.AvisarError("Revisa los datos de la reserva.");
        else Avisar(await agenda.GestionarAsync(new(id,version,estado,minutos,telefonoValidado,nota,AdministradorId,confirmarDirecto),ct), "Reserva actualizada.");
        return RedirectToAction(nameof(Index),new { dia=dia?.ToString("yyyy-MM-dd") });
    }
    /// <summary>Cita dada por teléfono o en persona, o tiempo bloqueado. Busca la ficha del cliente sin JavaScript.</summary>
    [HttpGet("/admin/agenda/nueva")]
    public async Task<IActionResult> Nueva(DateOnly? dia, TimeOnly? hora, int? clienteId, string? q, bool bloqueo, CancellationToken ct)
    {
        var formulario = new CitaManualFormulario { Dia = dia ?? agenda.Hoy, Hora = hora, ClienteId = clienteId, Bloqueo = bloqueo };
        return View(await PaginaNuevaAsync(formulario, q, ct));
    }
    [HttpPost("/admin/agenda/nueva")]
    public async Task<IActionResult> Nueva(CitaManualFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var f = formulario;
            var resultado = await agenda.CrearCitaAsync(new(f.Dia!.Value, f.Hora!.Value, f.Minutos, f.Bloqueo ? null : f.ClienteId,
                f.Nombre, f.Telefono, f.Bloqueo ? null : f.ServicioId, f.Nota, f.Bloqueo, AdministradorId), ct);
            if (resultado.Exito)
            {
                this.AvisarExito(f.Bloqueo ? "Tiempo bloqueado en la agenda." : "Cita guardada en la agenda.");
                return RedirectToAction(nameof(Index), new { dia = f.Dia.Value.ToString("yyyy-MM-dd") });
            }
            ModelState.AddModelError(resultado.Error!.Campo ?? string.Empty, resultado.Error.Mensaje);
        }
        return View(await PaginaNuevaAsync(formulario, null, ct));
    }
    private async Task<CitaManualPagina> PaginaNuevaAsync(CitaManualFormulario f, string? q, CancellationToken ct)
    {
        var cliente = f.ClienteId is { } id ? await clientes.ObtenerAsync(id, ct) : null;
        var encontrados = string.IsNullOrWhiteSpace(q) ? [] : await clientes.SugerirAsync(q, ct);
        var dia = f.Dia is { } d ? await agenda.DisponibilidadAsync(d, null, ct) : null;
        return new(f, cliente, q, encontrados, await agenda.ServiciosPanelAsync(ct), dia);
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
