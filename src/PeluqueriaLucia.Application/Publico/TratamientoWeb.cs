using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Publico;
namespace PeluqueriaLucia.Application.Publico;

// Proyección del catálogo único. No mantiene otra copia del nombre ni del precio.
public sealed record TratamientoWeb(int Id, int CategoriaId, string Nombre, string? Descripcion, decimal? Precio,
    TipoPrecioPublico TipoPrecio, int DuracionMinutos, int Orden, bool Publicado, bool Destacado)
{
    public static TratamientoWeb Desde(Servicio s) => new(s.Id, s.CategoriaPublicaId!.Value, s.Nombre, s.DescripcionPublica,
        s.TipoPrecioPublico == TipoPrecioPublico.Consultar ? null : s.Precio, s.TipoPrecioPublico, s.DuracionMinutos, s.OrdenPublico, s.Publicado && s.Activo, s.Destacado);
}
