using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public sealed record ServicioAgenda(int Id, string Nombre, int Minutos, string? NombreEn = null)
{
    /// <summary>Nombre que ve el visitante: en inglés si hay traducción; si no, el español.</summary>
    public string NombreEnIdioma(bool ingles) => ingles && NombreEn is { Length: > 0 } ? NombreEn : Nombre;
}
public sealed record DisponibilidadAgenda(DateOnly Dia, int Minutos, IReadOnlyList<TimeOnly> Horas, IReadOnlyList<TimeOnly>? Ocupadas = null);
public sealed record SolicitarCita(Guid SolicitudId, DateOnly Dia, TimeOnly Hora, string Telefono, string? Nombre, int? ServicioId, string Ip, string Correo, CanalConfirmacion Canal, string? Apellido = null, PeluqueriaLucia.Domain.Comun.Sexo? Sexo = null);
/// <param name="ConfirmarDirecto">La cita ya se ha acordado por teléfono: se confirma sin esperar al enlace de aceptación.</param>
public sealed record GestionarCita(Guid Id, string Version, EstadoCita Estado, int Minutos, bool TelefonoValidado, string? Nota, int AdministradorId, bool ConfirmarDirecto = false);
/// <summary>Cita que da el propio salón (teléfono, en persona) o tiempo bloqueado sin cliente. Nace confirmada.</summary>
public sealed record CitaManual(DateOnly Dia, TimeOnly Hora, int? Minutos, int? ClienteId, string? Nombre, string? Telefono, int? ServicioId, string? Nota, bool Bloqueo, int AdministradorId);
public sealed record AvisoAgenda(int Pendientes, DateTime? UltimaUtc);
public interface IAgendaService
{
    DateOnly Hoy { get; }
    Task<IReadOnlyList<ServicioAgenda>> ServiciosAsync(CancellationToken ct);
    Task<DisponibilidadAgenda> DisponibilidadAsync(DateOnly dia, int? servicioId, CancellationToken ct);
    Task<Resultado<Guid>> SolicitarAsync(SolicitarCita datos, CancellationToken ct);
    Task<Resultado> GestionarAsync(GestionarCita datos, CancellationToken ct);
    Task<IReadOnlyList<ServicioAgenda>> ServiciosPanelAsync(CancellationToken ct);
    Task<Resultado<Guid>> CrearCitaAsync(CitaManual datos, CancellationToken ct);
    Task<IReadOnlyList<ReservaCita>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    /// <summary>Quita de la agenda una cita ya cerrada (cancelada, rechazada, no presentada o caducada) con sus avisos. Las activas se cancelan antes.</summary>
    Task<Resultado> EliminarCitaAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<MensajeCita>> MensajesAsync(CancellationToken ct);
    Task<AvisoAgenda> AvisosAsync(CancellationToken ct);
    Task<IReadOnlyList<ReservaCita>> PendientesAsync(CancellationToken ct);
    Task<IReadOnlyList<TramoAgenda>> HorariosAsync(CancellationToken ct);
    Task<IReadOnlyList<CierreAgenda>> CierresAsync(CancellationToken ct);
    Task<Resultado> GuardarHorarioAsync(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct, int intervaloMinutos = 15);
    Task<Resultado> EliminarHorarioAsync(int id, CancellationToken ct);
    Task<Resultado> CerrarDiaAsync(DateOnly dia, string motivo, CancellationToken ct);
    Task<Resultado> AbrirDiaAsync(int id, CancellationToken ct);
}
