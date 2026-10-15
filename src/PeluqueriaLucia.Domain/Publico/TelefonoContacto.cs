using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Publico;

/// <summary>
/// Teléfono de contacto de la peluquería que enseñan la web y los avisos a clientes. Puede haber varios, en orden; el
/// administrador los cambia en «Contacto de la web». Se guardan tal como se escriben (con espacios), validados.
/// </summary>
public sealed class TelefonoContacto
{
    public const int LongitudMaxima = 30;

    public int Id { get; private set; }
    public string Numero { get; private set; } = string.Empty;
    public int Orden { get; private set; }

    private TelefonoContacto() { }

    public static TelefonoContacto Crear(string numero, int orden) => new() { Numero = Normalizar(numero), Orden = orden };

    /// <summary>
    /// Admite cifras, espacios, guiones, puntos, paréntesis y un «+» inicial; entre 9 y 15 cifras (fijos, móviles y
    /// números con prefijo internacional). Deja un solo espacio entre grupos.
    /// </summary>
    public static string Normalizar(string? numero)
    {
        var texto = string.Join(' ', (numero ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var cifras = texto.Count(char.IsAsciiDigit);
        var valido = texto.Length is > 0 and <= LongitudMaxima && cifras is >= 9 and <= 15 &&
            texto.Select((c, i) => char.IsAsciiDigit(c) || c is ' ' or '-' or '.' or '(' or ')' || (c == '+' && i == 0)).All(x => x);
        return valido ? texto : throw new ReglaDeNegocioException($"Revisa el teléfono «{texto}»: entre 9 y 15 cifras, con espacios si quieres.", "Telefonos");
    }

    /// <summary>Enlace para llamar desde el móvil: sin prefijo se entiende España («tel:+34659306659»).</summary>
    public static string Enlace(string numero)
    {
        var cifras = new string(numero.Where(char.IsAsciiDigit).ToArray());
        return "tel:+" + (numero.TrimStart().StartsWith('+') ? cifras : "34" + cifras);
    }
}
