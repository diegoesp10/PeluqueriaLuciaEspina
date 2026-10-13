using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Application.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class TratamientoFormulario
{
    [Range(1, int.MaxValue)] public int CategoriaId { get; set; }
    [Range(1, int.MaxValue)] public int ServicioId { get; set; }
    [StringLength(1000)] public string? Descripcion { get; set; }
    public TipoPrecioPublico TipoPrecio { get; set; }
    [Range(5, 600)] public int DuracionMinutos { get; set; } = 30;
    [Range(0, 10000)] public int Orden { get; set; }
    public bool Publicado { get; set; } = true;
    public bool Destacado { get; set; }
    public DatosTratamiento Datos() => new(ServicioId, CategoriaId, Descripcion, TipoPrecio, DuracionMinutos, Orden, Publicado, Destacado);
}
public sealed record TratamientosIndice(IReadOnlyList<CategoriaTratamiento> Categorias, IReadOnlyList<TratamientoWeb> Tratamientos, string? Texto = null);
public sealed record TratamientoEdicion(int? Id, TratamientoFormulario Formulario, IReadOnlyList<CategoriaTratamiento> Categorias, IReadOnlyList<PeluqueriaLucia.Domain.Catalogo.Servicio> Servicios);
public sealed class CategoriaFormulario
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = "";
}
public sealed record CategoriaEdicion(int? Id, CategoriaFormulario Formulario);
public sealed record FlechasOrden(string Accion, int Id, string Nombre, bool Primero, bool Ultimo);
public sealed record ResenasIndice(PeluqueriaLucia.Application.Comun.Pagina<Resena> Pagina, EstadoResena? Estado, string? Texto);
public sealed class ModeracionFormulario
{
    public EstadoResena Estado { get; set; }
    [StringLength(1500)] public string? Respuesta { get; set; }
}
