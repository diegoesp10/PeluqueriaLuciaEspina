using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Publico;
namespace PeluqueriaLucia.Application.Publico;

// Proyección del catálogo único. No mantiene otra copia del nombre ni del precio.
public sealed record TratamientoWeb(int Id, int CategoriaId, string Nombre, string? Descripcion, decimal? Precio,
    TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado, string? NombreEn = null, string? DescripcionEn = null)
{
    public static TratamientoWeb Desde(Servicio s) => new(s.Id, s.CategoriaPublicaId!.Value, s.Nombre, s.DescripcionPublica,
        s.TipoPrecioPublico == TipoPrecioPublico.Consultar ? null : s.Precio, s.TipoPrecioPublico, s.DuracionMinutos, s.OrdenPublico, s.Publicado && s.Activo, s.Destacado,
        s.NombreEn, s.DescripcionPublicaEn);

    /// <summary>Textos que ve el visitante: en inglés si hay traducción; si no, el español.</summary>
    public string NombreEnIdioma(bool ingles) => ingles && NombreEn is { Length: > 0 } ? NombreEn : Nombre;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;
}
