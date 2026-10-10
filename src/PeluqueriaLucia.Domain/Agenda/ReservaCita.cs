namespace PeluqueriaLucia.Domain.Agenda;

public enum EstadoCita { Pendiente, Confirmada, Rechazada, Cancelada, Completada }
public enum EstadoSms { Pendiente, Enviando, AceptadoProveedor, Error, Incierto, Manual, Obsoleto, Entregado }

/// <summary>Horas locales del salón; fechas de auditoría UTC. Una solicitud no es una cita confirmada.</summary>
public sealed class ReservaCita
{
    public Guid Id { get; set; }
    public Guid SolicitudId { get; set; }
    public DateTime Inicio { get; set; }
    public DateTime Fin { get; set; }
    public string Nombre { get; set; } = "";
    public string Telefono { get; set; } = "";
    public int? ServicioId { get; set; }
    public string ServicioNombre { get; set; } = "Consulta con el equipo";
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
    public string Telefono { get; set; } = "";
    public string Texto { get; set; } = "";
    public EstadoSms Estado { get; set; }
    public DateTime CreadoUtc { get; set; }
    public DateTime? ProcesadoUtc { get; set; }
    public string? ProveedorId { get; set; }
    public byte[] Version { get; set; } = [];
}
public sealed class SmsEntrante
{
    public Guid Id { get; set; }
    public string ProveedorId { get; set; } = "";
    public string Telefono { get; set; } = "";
    public string Texto { get; set; } = "";
    public DateTime RecibidoUtc { get; set; }
}
