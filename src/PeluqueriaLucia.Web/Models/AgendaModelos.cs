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
    [StringLength(80)] public string? Nombre { get; set; }
    public int? ServicioId { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage="Necesitamos tu permiso para contactar sobre esta cita.")]
    public bool Consentimiento { get; set; }
    public string? Web { get; set; }
}
public sealed record ReservaPagina(ReservaFormulario Formulario, IReadOnlyList<ServicioAgenda> Servicios, DisponibilidadAgenda Disponibilidad, DateOnly Hoy);
public sealed record AgendaPanel(DateOnly Dia, IReadOnlyList<ReservaCita> Citas, IReadOnlyList<MensajeCita> Mensajes, IReadOnlyList<SmsEntrante> Entrantes, bool SmsActivo, AvisoAgenda Avisos);
public sealed record HorariosPagina(IReadOnlyList<TramoAgenda> Horarios, IReadOnlyList<CierreAgenda> Cierres, DateOnly Hoy);
