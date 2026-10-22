using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class ClienteFormulario
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(Cliente.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el apellido.")]
    [StringLength(Cliente.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Apellido { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Ese email no parece válido.")]
    public string? Email { get; set; }

    [Phone(ErrorMessage = "Ese teléfono no parece válido.")]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "Elige el sexo.")]
    public Sexo? Sexo { get; set; }

    [StringLength(80)] public string? Apodo { get; set; }

    /// <summary>Canal preferido para los avisos de sus citas; vacío, sin preferencia (se elige en cada cita).</summary>
    public Domain.Agenda.CanalConfirmacion? CanalPreferido { get; set; }

    public DatosCliente ADatos() => new(Nombre, Apellido, Email, Telefono, Sexo ?? Domain.Comun.Sexo.Otro, Apodo, CanalPreferido);

    public static ClienteFormulario Desde(Cliente c) =>
        new() { Nombre = c.Nombre, Apellido = c.Apellido, Email = c.Email, Telefono = c.Telefono, Sexo = c.Sexo, Apodo = c.Apodo, CanalPreferido = c.CanalPreferido };
}

/// <param name="VolverAVisita">El alta se abrió desde «Nueva visita»: al guardar se vuelve allí con el cliente elegido.</param>
public sealed record ClienteEdicionModelo(int? Id, ClienteFormulario Formulario, bool VolverAVisita = false);

public sealed record ClientesIndiceModelo(Pagina<Cliente> Pagina, string? Texto, Sexo? Sexo);

public sealed record ClienteDetalleModelo(Cliente Cliente, ResumenVisitas Resumen, Pagina<Visita> Visitas, DateOnly Hoy, bool TieneCitas = false);

/// <summary>Sugerencia del buscador de clientes (JSON). Canal: preferencia de aviso (0 correo, 1 WhatsApp, null sin preferencia).</summary>
public sealed record ClienteSugerencia(int Id, string Nombre, string? Detalle, string Iniciales, int Sexo, int? Canal = null, bool ConCorreo = false, bool ConTelefono = false);

/// <summary>Textos de la preferencia de aviso, iguales en todas las pantallas del panel.</summary>
public static class FormatoAvisos
{
    public static string Texto(Domain.Agenda.CanalConfirmacion? canal) => canal switch
    {
        Domain.Agenda.CanalConfirmacion.Correo => "Por correo",
        Domain.Agenda.CanalConfirmacion.WhatsApp => "Por WhatsApp",
        _ => "Sin preferencia",
    };

    public static string Icono(Domain.Agenda.CanalConfirmacion? canal) => canal == Domain.Agenda.CanalConfirmacion.WhatsApp ? "telefono" : "email";
}
