using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class AccesoFormulario
{
    [Required(ErrorMessage = "Escribe tu usuario."), StringLength(50)]
    public string Usuario { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe tu contraseña."), StringLength(256)]
    [DataType(DataType.Password)]
    public string Contrasena { get; set; } = string.Empty;

    public string? Volver { get; set; }
}

public sealed class ContrasenaFormulario
{
    [Required(ErrorMessage = "Escribe tu contraseña actual."), StringLength(256, ErrorMessage = "La contraseña actual no puede superar 256 caracteres."), DataType(DataType.Password)]
    public string Actual { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe una nueva contraseña."), StringLength(256, MinimumLength = 15, ErrorMessage = "La nueva contraseña debe tener entre 15 y 256 caracteres."), DataType(DataType.Password)]
    public string Nueva { get; set; } = string.Empty;

    [Required(ErrorMessage = "Repite la nueva contraseña."), StringLength(256, ErrorMessage = "La confirmación no puede superar 256 caracteres."), Compare(nameof(Nueva), ErrorMessage = "Las contraseñas no coinciden."), DataType(DataType.Password)]
    public string Confirmacion { get; set; } = string.Empty;
}

public sealed record SeguridadModelo(Guid SesionActual, IReadOnlyList<SesionAdministrador> Sesiones,
    IReadOnlyList<IntentoAcceso> Intentos, IReadOnlyList<ActividadIp> Actividad, OpcionesSeguridad Opciones);

public sealed record PanelModelo(DateOnly Hoy, ResumenVisitas Resumen, IReadOnlyList<ActividadIp> Actividad);
