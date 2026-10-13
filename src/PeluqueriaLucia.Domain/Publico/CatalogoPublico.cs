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
    /// <summary>Posición en la web (1 = primera). La fijan las flechas de subir y bajar.</summary>
    public void Colocar(int posicion) => Orden = Math.Max(0, posicion);

    public void Editar(string nombre, int orden)
    {
        nombre = nombre.Trim();
        if (nombre.Length is < 2 or > 80 || orden is < 0 or > 10000) throw new ReglaDeNegocioException("Revisa el nombre y el orden de la categoría.");
        Nombre = nombre; Orden = orden;
    }
}
