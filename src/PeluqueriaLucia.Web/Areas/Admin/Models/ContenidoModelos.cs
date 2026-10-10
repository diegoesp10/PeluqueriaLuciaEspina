using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Application.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class TratamientoFormulario
{
    [Range(1, int.MaxValue)] public int CategoriaId { get; set; }
    [Required, StringLength(100, MinimumLength = 2)] public string Nombre { get; set; } = "";
    [StringLength(1000)] public string? Descripcion { get; set; }
    [Range(0, 99999)] public decimal? Precio { get; set; }
    public TipoPrecioPublico TipoPrecio { get; set; }
    [Range(5, 600)] public int DuracionMinutos { get; set; } = 30;
    [Range(0, 10000)] public int Orden { get; set; }
    public bool Publicado { get; set; } = true;
    public bool Destacado { get; set; }
    public DatosTratamiento Datos() => new(CategoriaId, Nombre, Descripcion, Precio, TipoPrecio, DuracionMinutos, Orden, Publicado, Destacado);
}
public sealed record TratamientosIndice(IReadOnlyList<CategoriaTratamiento> Categorias, IReadOnlyList<TratamientoPublico> Tratamientos);
public sealed record TratamientoEdicion(int? Id, TratamientoFormulario Formulario, IReadOnlyList<CategoriaTratamiento> Categorias);
public sealed class CategoriaFormulario
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = "";
    [Range(0, 10000)] public int Orden { get; set; }
}
public sealed record CategoriaEdicion(int? Id, CategoriaFormulario Formulario);
public sealed class ModeracionFormulario
{
    public EstadoResena Estado { get; set; }
    [StringLength(1500)] public string? Respuesta { get; set; }
}
