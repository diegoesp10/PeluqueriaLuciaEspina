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
    [Required(ErrorMessage="Escribe tu nombre o apodo."), StringLength(100)] public string? Nombre { get; set; }
    [Required(ErrorMessage="Escribe tus apellidos."), StringLength(100)] public string Apellido { get; set; } = "";
    [Required(ErrorMessage="Elige una opción."), EnumDataType(typeof(PeluqueriaLucia.Domain.Comun.Sexo))] public PeluqueriaLucia.Domain.Comun.Sexo? Sexo { get; set; }
    public int? ServicioId { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage="Necesitamos tu permiso para contactar sobre esta cita.")]
    public bool Consentimiento { get; set; }
    public string? Web { get; set; }
}
public sealed record ReservaPagina(ReservaFormulario Formulario, IReadOnlyList<ServicioAgenda> Servicios, DisponibilidadAgenda Disponibilidad, DateOnly Hoy);
public sealed record AgendaPanel(DateOnly Dia, IReadOnlyList<ReservaCita> Citas, IReadOnlyList<ComunicacionCita> Mensajes, bool CorreoConfigurado, bool EnlaceConfigurado, AvisoAgenda Avisos);
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
