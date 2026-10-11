using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Web.Models;

public sealed class ReservaFormulario
{
    public Guid SolicitudId { get; set; } = Guid.NewGuid();
    [Required] public DateOnly Dia { get; set; }
    [Required] public TimeOnly? Hora { get; set; }
    [Required, StringLength(30), Display(Name="Móvil de contacto")] public string Telefono { get; set; } = "";
    [Required, EmailAddress, StringLength(254), Display(Name="Correo electrónico")] public string Correo { get; set; } = "";
    [Required, EnumDataType(typeof(CanalConfirmacion)), Display(Name="Canal de confirmación")] public CanalConfirmacion? Canal { get; set; }
    [Required(ErrorMessage="Escribe tu nombre o apodo."), StringLength(80)] public string? Nombre { get; set; }
    public int? ServicioId { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage="Necesitamos tu permiso para contactar sobre esta cita.")]
    public bool Consentimiento { get; set; }
    public string? Web { get; set; }
}
public sealed record ReservaPagina(ReservaFormulario Formulario, IReadOnlyList<ServicioAgenda> Servicios, DisponibilidadAgenda Disponibilidad, DateOnly Hoy);
public sealed record AgendaPanel(DateOnly Dia, IReadOnlyList<ReservaCita> Citas, IReadOnlyList<ComunicacionCita> Mensajes, bool CorreoConfigurado, bool EnlaceConfigurado, AvisoAgenda Avisos);
public sealed record AceptacionPagina(string Token, PropuestaCita? Propuesta);
public sealed record HorariosPagina(IReadOnlyList<TramoAgenda> Horarios, IReadOnlyList<CierreAgenda> Cierres, DateOnly Hoy);
