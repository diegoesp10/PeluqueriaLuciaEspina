using System.Text;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Publico;

public enum TipoPrecioPublico { Fijo, Desde, Consultar }
public sealed class CategoriaTratamiento
{
    private CategoriaTratamiento() { }
    public int Id { get; private set; }
    public string Nombre { get; private set; } = "";
    public string Slug { get; private set; } = "";
    public int Orden { get; private set; }
    public static CategoriaTratamiento Crear(string nombre, int orden)
    {
        var c = new CategoriaTratamiento(); c.Editar(nombre, orden);
        c.Slug = new string(c.Nombre.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(char.IsAsciiLetterOrDigit).ToArray());
        if (c.Slug.Length == 0) throw new ReglaDeNegocioException("La categoría necesita un nombre reconocible.");
        return c;
    }
    public void Editar(string nombre, int orden)
    {
        nombre = nombre.Trim();
        if (nombre.Length is < 2 or > 80 || orden is < 0 or > 10000) throw new ReglaDeNegocioException("Revisa el nombre y el orden de la categoría.");
        Nombre = nombre; Orden = orden;
    }
}
public sealed class TratamientoPublico
{
    private TratamientoPublico() { }
    public int Id { get; private set; }
    public int CategoriaId { get; private set; }
    public string Nombre { get; private set; } = "";
    public string? Descripcion { get; private set; }
    public decimal? Precio { get; private set; }
    public TipoPrecioPublico TipoPrecio { get; private set; }
    public int DuracionMinutos { get; private set; }
    public int Orden { get; private set; }
    public bool Publicado { get; private set; }
    public bool Destacado { get; private set; }
    public static TratamientoPublico Crear(int categoria, string nombre, string? descripcion, decimal? precio, TipoPrecioPublico tipo, int minutos, int orden, bool publicado, bool destacado)
    { var t = new TratamientoPublico(); t.Editar(categoria, nombre, descripcion, precio, tipo, minutos, orden, publicado, destacado); return t; }
    public void Editar(int categoria, string nombre, string? descripcion, decimal? precio, TipoPrecioPublico tipo, int minutos, int orden, bool publicado, bool destacado)
    {
        if (categoria <= 0 || nombre.Trim().Length is < 2 or > 100 || descripcion?.Length > 1000 ||
            !Enum.IsDefined(tipo) || (tipo != TipoPrecioPublico.Consultar && (precio is null || precio < 0 || precio > 99999)) || minutos is < 5 or > 600 || orden is < 0 or > 10000)
            throw new ReglaDeNegocioException("Revisa los datos del tratamiento, su precio y duración.");
        CategoriaId = categoria; Nombre = nombre.Trim(); Descripcion = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        TipoPrecio = tipo; Precio = tipo == TipoPrecioPublico.Consultar ? null : decimal.Round(precio!.Value, 2);
        DuracionMinutos = minutos; Orden = orden; Publicado = publicado; Destacado = destacado;
    }
}
