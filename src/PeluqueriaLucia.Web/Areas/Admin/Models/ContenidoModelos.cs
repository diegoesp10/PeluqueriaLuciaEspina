using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Application.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class TratamientoFormulario
{
    [Range(1, int.MaxValue)] public int CategoriaId { get; set; }
    [Range(1, int.MaxValue)] public int ServicioId { get; set; }
    [StringLength(1000)] public string? Descripcion { get; set; }
    [StringLength(1000)] public string? DescripcionEn { get; set; }
    public TipoPrecioPublico TipoPrecio { get; set; }
    [Range(5, 600)] public int DuracionMinutos { get; set; } = 30;
    [Range(0, 10000)] public int Orden { get; set; }
    public bool Publicado { get; set; } = true;
    public bool Destacado { get; set; }
    public DatosTratamiento Datos() => new(ServicioId, CategoriaId, Descripcion, TipoPrecio, DuracionMinutos, Orden, Publicado, Destacado, DescripcionEn);
}
public sealed record TratamientosIndice(IReadOnlyList<CategoriaTratamiento> Categorias, IReadOnlyList<TratamientoWeb> Tratamientos, string? Texto = null);
public sealed record TratamientoEdicion(int? Id, TratamientoFormulario Formulario, IReadOnlyList<CategoriaTratamiento> Categorias, IReadOnlyList<PeluqueriaLucia.Domain.Catalogo.Servicio> Servicios);
public sealed class CategoriaFormulario
{
    [Required, StringLength(80, MinimumLength = 2)] public string Nombre { get; set; } = "";
    /// <summary>Obligatorio al crear; en las categorías anteriores al inglés puede faltar (la web muestra el español).</summary>
    [StringLength(80, MinimumLength = 2)] public string? NombreEn { get; set; }
}
public sealed record CategoriaEdicion(int? Id, CategoriaFormulario Formulario);
public sealed record FlechasOrden(string Accion, int Id, string Nombre, bool Primero, bool Ultimo);
/// <param name="Conteo">Cuántas reseñas hay en cada estado, para las pestañas.</param>
public sealed record ResenasIndice(PeluqueriaLucia.Application.Comun.Pagina<Resena> Pagina, EstadoResena? Estado, string? Texto,
    IReadOnlyDictionary<EstadoResena, int> Conteo)
{
    /// <summary>Las pestañas, en el orden del trabajo: lo que hay que revisar, lo que se ve, lo que se quitó y lo descartado.</summary>
    public static IReadOnlyList<(EstadoResena Estado, string Texto)> Pestanas { get; } =
        [(EstadoResena.Pendiente, "Pendientes"), (EstadoResena.Publicada, "Publicadas"), (EstadoResena.Oculta, "Ocultas"), (EstadoResena.Rechazada, "Rechazadas")];

    public int Total(EstadoResena estado) => Conteo.GetValueOrDefault(estado);

    public static string Nombre(EstadoResena estado) => estado switch
    {
        EstadoResena.Publicada => "Publicada",
        EstadoResena.Oculta => "Oculta",
        EstadoResena.Rechazada => "Rechazada",
        _ => "Pendiente",
    };

    public static string Tinte(EstadoResena estado) => estado switch
    {
        EstadoResena.Publicada => "tint-green",
        EstadoResena.Oculta => "tint-gray",
        EstadoResena.Rechazada => "tint-red",
        _ => "tint-orange",
    };

    public static string Vacio(EstadoResena? estado) => estado switch
    {
        EstadoResena.Pendiente => "No hay reseñas por revisar.",
        EstadoResena.Publicada => "Todavía no hay reseñas publicadas.",
        EstadoResena.Oculta => "No hay reseñas ocultas.",
        EstadoResena.Rechazada => "No hay reseñas rechazadas.",
        _ => "Aún no hay opiniones aquí.",
    };

    public static string VacioDetalle(EstadoResena? estado) => estado switch
    {
        EstadoResena.Pendiente => "Cuando una clienta deje su opinión con el código del ticket, aparecerá aquí para que la publiques.",
        EstadoResena.Oculta => "Las publicadas que quites de la web quedan aquí y se pueden volver a publicar.",
        _ => "Las visitas verificadas aparecerán en este espacio.",
    };

    /// <summary>El aviso tras moderar, según de qué estado a qué estado pasa.</summary>
    public static string Aviso(string alias, EstadoResena antes, EstadoResena ahora) => (antes, ahora) switch
    {
        (EstadoResena.Publicada, EstadoResena.Publicada) => $"Respuesta a {alias} guardada: la web ya muestra la nueva.",
        (EstadoResena.Oculta, EstadoResena.Publicada) => $"Reseña de {alias} publicada de nuevo: vuelve a verse en la web.",
        (_, EstadoResena.Publicada) => $"Reseña de {alias} publicada: ya se ve en la web. La tienes en «Publicadas».",
        (_, EstadoResena.Oculta) => $"Reseña de {alias} oculta: ya no se ve en la web. Puedes volver a publicarla desde «Ocultas».",
        (_, EstadoResena.Rechazada) => $"Reseña de {alias} rechazada: no se publicará.",
        _ => $"Reseña de {alias} devuelta a «Pendientes».",
    };
}

public sealed class ModeracionFormulario
{
    public EstadoResena Estado { get; set; }
    [StringLength(1500)] public string? Respuesta { get; set; }
    [StringLength(1500)] public string? RespuestaEn { get; set; }
    /// <summary>La pestaña desde la que se modera, para volver a ella (la reseña sale de la lista si cambia de estado).</summary>
    public EstadoResena? Desde { get; set; }
    public int Pagina { get; set; } = 1;
    [StringLength(200)] public string? Q { get; set; }
}
