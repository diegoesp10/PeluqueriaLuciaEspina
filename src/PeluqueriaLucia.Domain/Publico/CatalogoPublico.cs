using System.Text;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Publico;

public enum TipoPrecioPublico { Fijo, Desde, Consultar }
public sealed class CategoriaTratamiento
{
    private CategoriaTratamiento() { }
    public int Id { get; private set; }
    public const int LongitudNombre = 80;
    public string Nombre { get; private set; } = "";
    /// <summary>Nombre en inglés para la web. null en las categorías anteriores al inglés: se muestra el español.</summary>
    public string? NombreEn { get; private set; }
    public string Slug { get; private set; } = "";
    public int Orden { get; private set; }
    public static CategoriaTratamiento Crear(string nombre, int orden, string? nombreEn = null)
    {
        var c = new CategoriaTratamiento(); c.Editar(nombre, orden); c.Traducir(nombreEn);
        c.Slug = new string(c.Nombre.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(char.IsAsciiLetterOrDigit).ToArray());
        if (c.Slug.Length == 0) throw new ReglaDeNegocioException("La categoría necesita un nombre reconocible.");
        return c;
    }
    public void Traducir(string? nombreEn)
    {
        nombreEn = Texto.Opcional(nombreEn);
        if (nombreEn?.Length > LongitudNombre) throw new ReglaDeNegocioException($"El nombre en inglés no puede superar {LongitudNombre} caracteres.", nameof(NombreEn));
        NombreEn = nombreEn;
    }
    /// <summary>Nombre que ve el visitante: en inglés si hay traducción; si no, el español.</summary>
    public string NombreEnIdioma(bool ingles) => ingles && NombreEn is { Length: > 0 } ? NombreEn : Nombre;
    /// <summary>Posición en la web (1 = primera). La fijan las flechas de subir y bajar.</summary>
    public void Colocar(int posicion) => Orden = Math.Max(0, posicion);

    public void Editar(string nombre, int orden)
    {
        nombre = nombre.Trim();
        if (nombre.Length is < 2 or > 80 || orden is < 0 or > 10000) throw new ReglaDeNegocioException("Revisa el nombre y el orden de la categoría.");
        Nombre = nombre; Orden = orden;
    }
}
