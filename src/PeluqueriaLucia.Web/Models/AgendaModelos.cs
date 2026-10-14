using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Web.Models;

public sealed class ReservaFormulario
{
    public Guid SolicitudId { get; set; } = Guid.NewGuid();
    [Required(ErrorMessage="Elige un día.")] public DateOnly Dia { get; set; }
    [Required(ErrorMessage="Elige una hora.")] public TimeOnly? Hora { get; set; }
    [Required(ErrorMessage="Escribe tu móvil."), StringLength(30, ErrorMessage="Revisa el móvil."), Display(Name="Móvil de contacto")] public string Telefono { get; set; } = "";
    [Required(ErrorMessage="Escribe tu correo electrónico."), EmailAddress(ErrorMessage="Introduce un correo electrónico válido."), StringLength(254, ErrorMessage="Introduce un correo electrónico válido."), Display(Name="Correo electrónico")] public string Correo { get; set; } = "";
    [Required(ErrorMessage="Elige correo electrónico o WhatsApp."), EnumDataType(typeof(CanalConfirmacion), ErrorMessage="Elige correo electrónico o WhatsApp."), Display(Name="Canal de confirmación")] public CanalConfirmacion? Canal { get; set; }
    [Required(ErrorMessage="Escribe tu nombre o apodo."), StringLength(100, ErrorMessage="El nombre admite hasta 100 caracteres.")] public string? Nombre { get; set; }
    [Required(ErrorMessage="Escribe tus apellidos."), StringLength(100, ErrorMessage="Los apellidos admiten hasta 100 caracteres.")] public string Apellido { get; set; } = "";
    [Required(ErrorMessage="Elige una opción."), EnumDataType(typeof(PeluqueriaLucia.Domain.Comun.Sexo), ErrorMessage="Elige una opción.")] public PeluqueriaLucia.Domain.Comun.Sexo? Sexo { get; set; }
    public int? ServicioId { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage="Necesitamos tu permiso para contactar sobre esta cita.")]
    public bool Consentimiento { get; set; }
    public string? Web { get; set; }
}
public sealed record ReservaPagina(ReservaFormulario Formulario, IReadOnlyList<ServicioAgenda> Servicios, DisponibilidadAgenda Disponibilidad, DateOnly Hoy);
/// <summary>Qué citas enseña el día elegido: abiertas (solicitudes, propuestas y confirmadas), canceladas (y rechazadas, no presentadas o caducadas), completadas o todas.</summary>
public enum FiltroCitas { Abiertas, Canceladas, Completadas, Todas }

/// <summary>Resumen de un día del calendario del panel.</summary>
public sealed record DiaCalendario(DateOnly Dia, int Abiertas, int Canceladas, int Completadas, int Bloqueos, string? Cierre)
{
    public int Total => Abiertas + Canceladas + Completadas + Bloqueos;
}

/// <summary>Calendario mensual del panel con el detalle del día elegido y las solicitudes pendientes de cualquier fecha.</summary>
public sealed record AgendaCalendario(DateOnly Dia, DateOnly Hoy, IReadOnlyList<DiaCalendario> Dias, IReadOnlyList<ReservaCita> CitasDia,
    IReadOnlyList<ReservaCita> Pendientes, FiltroCitas Filtro, AvisoAgenda Avisos, DateTime Utc)
{
    public DateOnly InicioMes => new(Dia.Year, Dia.Month, 1);

    public DateOnly MesAnterior => InicioMes.AddMonths(-1);

    public DateOnly MesSiguiente => InicioMes.AddMonths(1);

    /// <summary>Huecos en blanco antes del día 1 para que la cuadrícula empiece en lunes.</summary>
    public int Desplazamiento => ((int)InicioMes.DayOfWeek + 6) % 7;

    public static bool Caducada(ReservaCita c, DateTime utc) => c.Estado is EstadoCita.Pendiente or EstadoCita.PendienteAceptacion && c.CaducaUtc <= utc;

    public static FiltroCitas GrupoDe(ReservaCita c, DateTime utc) => c.Estado switch
    {
        EstadoCita.Completada => FiltroCitas.Completadas,
        EstadoCita.Cancelada or EstadoCita.Rechazada or EstadoCita.NoPresentada => FiltroCitas.Canceladas,
        _ => Caducada(c, utc) ? FiltroCitas.Canceladas : FiltroCitas.Abiertas,
    };

    public static string TextoEstado(ReservaCita c, DateTime utc) => c.Origen == OrigenCita.Bloqueo ? "Bloqueo" : Caducada(c, utc) ? "Caducada" : c.Estado switch
    {
        EstadoCita.Pendiente => "Solicitud pendiente",
        EstadoCita.PendienteAceptacion => "Pendiente de aceptación",
        EstadoCita.NoPresentada => "No acudió",
        var e => e.ToString(),
    };
}

/// <summary>Avisos a clientes: pendientes de revisar y enviar, enviados o todos.</summary>
public sealed record ComunicacionesPagina(IReadOnlyList<ComunicacionCita> Mensajes, string Filtro, int Pendientes, bool CorreoConfigurado, bool EnlaceConfigurado, bool EnvioAutomatico);
public sealed record AceptacionPagina(string Token, PropuestaCita? Propuesta);
/// <summary>Cita que da el salón desde el panel, o tiempo bloqueado.</summary>
public sealed class CitaManualFormulario
{
    [Required(ErrorMessage="Elige el día.")] public DateOnly? Dia { get; set; }
    [Required(ErrorMessage="Elige la hora.")] public TimeOnly? Hora { get; set; }
    [Range(5, 600, ErrorMessage="La duración debe ser de 5 a 600 minutos.")] public int? Minutos { get; set; }
    public int? ClienteId { get; set; }
    [StringLength(100)] public string? Nombre { get; set; }
    [StringLength(30)] public string? Telefono { get; set; }
    public int? ServicioId { get; set; }
    [StringLength(500)] public string? Nota { get; set; }
    public bool Bloqueo { get; set; }
}
public sealed record CitaManualPagina(CitaManualFormulario Formulario, PeluqueriaLucia.Domain.Clientes.Cliente? Cliente, string? Busqueda,
    IReadOnlyList<PeluqueriaLucia.Domain.Clientes.Cliente> Encontrados, IReadOnlyList<ServicioAgenda> Servicios, DisponibilidadAgenda? Dia);
public sealed record HorariosPagina(IReadOnlyList<TramoAgenda> Horarios, IReadOnlyList<CierreAgenda> Cierres, DateOnly Hoy);
