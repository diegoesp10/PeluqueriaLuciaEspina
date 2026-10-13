namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Búsqueda por palabras con LIKE: cada palabra escrita tiene que aparecer en alguno de los campos («tinte eco» encuentra
/// «Tinte ecológico»). SQL Server compara sin distinguir mayúsculas ni acentos con la intercalación de la base de datos.
/// </summary>
internal static class BusquedaTexto
{
    /// <summary>Máximo de palabras que se tienen en cuenta: más no afinan y alargan la consulta.</summary>
    private const int MaximoPalabras = 6;

    public static IEnumerable<string> Palabras(string? texto) =>
        (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length <= 100).Take(MaximoPalabras);

    /// <summary>«%palabra%» con los comodines de LIKE escapados (carácter de escape: \).</summary>
    public static string Patron(string palabra) =>
        $"%{palabra.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[")}%";
}
