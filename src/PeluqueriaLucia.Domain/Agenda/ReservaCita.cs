namespace PeluqueriaLucia.Domain.Agenda;

public enum EstadoCita { Pendiente = 0, Confirmada = 1, Rechazada = 2, Cancelada = 3, Completada = 4, PendienteAceptacion = 5, NoPresentada = 6 }
public enum CanalConfirmacion { Correo = 0, WhatsApp = 1 }
/// <summary>Web: solicitud del formulario público. Panel: cita dada por el salón (teléfono, en persona). Bloqueo: tiempo reservado sin cliente.</summary>
public enum OrigenCita { Web = 0, Panel = 1, Bloqueo = 2 }
public enum EstadoNotificacion { Pendiente, Enviando, AceptadoServidor, Error, Incierto, Manual, Obsoleto }

/// <summary>Horas locales del salón; fechas de auditoría UTC. Una solicitud no es una cita confirmada.</summary>
public sealed class ReservaCita
{
    public Guid Id { get; set; }
    public Guid SolicitudId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public PeluqueriaLucia.Domain.Comun.Sexo? Sexo { get; set; }
    public string Telefono { get; set; } = "";
    public string Correo { get; set; } = "";
    public CanalConfirmacion Canal { get; set; }
    public OrigenCita Origen { get; set; }
    public string? ConfirmacionHash { get; set; }
    public DateTime? AceptadaUtc { get; set; }
    public int? ClienteId { get; set; }
    public PeluqueriaLucia.Domain.Clientes.Cliente? Cliente { get; set; }
    public bool PendienteVinculacion { get; set; }
    public int? ServicioId { get; set; }
    public string ServicioNombre { get; set; } = "Consulta en la peluquería";

    /// <summary>
    /// La cita es de un servicio con dualidad: no ocupa la agenda, así que otras citas pueden coincidir con ella (y ella con
    /// otras). Se toma del servicio al dar la cita y cambia con él mientras la cita no ha terminado. El tiempo bloqueado
    /// ocupa siempre.
    /// </summary>
    public bool Dualidad { get; set; }

    public EstadoCita Estado { get; set; }
    public DateTime CreadaUtc { get; set; }
    public DateTime ActualizadaUtc { get; set; }
    public DateTime CaducaUtc { get; set; }
    public string IpHash { get; set; } = "";
    public bool TelefonoValidado { get; set; }
    public int? AdministradorId { get; set; }
    public string NotaInterna { get; set; } = "";
    public byte[] Version { get; set; } = [];
}

public sealed class TramoAgenda
{
    public int Id { get; set; }
    public int IntervaloMinutos { get; set; } = 15;
    public DayOfWeek Dia { get; set; }
    public TimeOnly Desde { get; set; }
    public TimeOnly Hasta { get; set; }
}
public sealed class CierreAgenda
{
    public int Id { get; set; }
    public DateOnly Dia { get; set; }
    public string Motivo { get; set; } = "";
}
public sealed class MensajeCita
{
    public Guid Id { get; set; }
    public Guid ReservaId { get; set; }
    public string Destinatario { get; set; } = "";
    public CanalConfirmacion Canal { get; set; }
    public string? TokenProtegido { get; set; }
    public string Texto { get; set; } = "";
    public EstadoNotificacion Estado { get; set; }
    public DateTime CreadoUtc { get; set; }
    public DateTime? ProcesadoUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
