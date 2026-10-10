using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public sealed record ServicioAgenda(int Id, string Nombre, int Minutos);
public sealed record DisponibilidadAgenda(DateOnly Dia, int Minutos, IReadOnlyList<TimeOnly> Horas);
public sealed record SolicitarCita(Guid SolicitudId, DateOnly Dia, TimeOnly Hora, string Telefono, string? Nombre, int? ServicioId, string Ip);
public sealed record GestionarCita(Guid Id, string Version, EstadoCita Estado, int Minutos, bool TelefonoValidado, string? Nota, int AdministradorId);
public sealed record AvisoAgenda(int Pendientes, DateTime? UltimaUtc);
public interface IAgendaService
{
    DateOnly Hoy { get; }
    Task<IReadOnlyList<ServicioAgenda>> ServiciosAsync(CancellationToken ct);
    Task<DisponibilidadAgenda> DisponibilidadAsync(DateOnly dia, int? servicioId, CancellationToken ct);
    Task<Resultado<Guid>> SolicitarAsync(SolicitarCita datos, CancellationToken ct);
    Task<Resultado> GestionarAsync(GestionarCita datos, CancellationToken ct);
    Task<IReadOnlyList<ReservaCita>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    Task<IReadOnlyList<MensajeCita>> MensajesAsync(CancellationToken ct);
    Task<AvisoAgenda> AvisosAsync(CancellationToken ct);
    Task<IReadOnlyList<ReservaCita>> PendientesAsync(CancellationToken ct);
    Task<IReadOnlyList<TramoAgenda>> HorariosAsync(CancellationToken ct);
    Task<IReadOnlyList<CierreAgenda>> CierresAsync(CancellationToken ct);
    Task<Resultado> GuardarHorarioAsync(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct);
    Task<Resultado> EliminarHorarioAsync(int id, CancellationToken ct);
    Task<Resultado> CerrarDiaAsync(DateOnly dia, string motivo, CancellationToken ct);
    Task<Resultado> AbrirDiaAsync(int id, CancellationToken ct);
    Task<Resultado> MarcarSmsManualAsync(Guid id, CancellationToken ct);
}
