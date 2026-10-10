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

    public DatosCliente ADatos() => new(Nombre, Apellido, Email, Telefono, Sexo ?? Domain.Comun.Sexo.Otro);

    public static ClienteFormulario Desde(Cliente c) =>
        new() { Nombre = c.Nombre, Apellido = c.Apellido, Email = c.Email, Telefono = c.Telefono, Sexo = c.Sexo };
}

/// <param name="VolverAVisita">El alta se abrió desde «Nueva visita»: al guardar se vuelve allí con el cliente elegido.</param>
public sealed record ClienteEdicionModelo(int? Id, ClienteFormulario Formulario, bool VolverAVisita = false);

public sealed record ClientesIndiceModelo(Pagina<Cliente> Pagina, string? Texto, Sexo? Sexo);

public sealed record ClienteDetalleModelo(Cliente Cliente, ResumenVisitas Resumen, Pagina<Visita> Visitas, DateOnly Hoy);

/// <summary>Sugerencia del buscador de clientes (JSON).</summary>
public sealed record ClienteSugerencia(int Id, string Nombre, string? Detalle, string Iniciales, int Sexo);
