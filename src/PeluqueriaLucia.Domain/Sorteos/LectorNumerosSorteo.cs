using System.Globalization;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Sorteos;

/// <summary>
/// Lee los números que se escriben a mano para un sorteo. Una línea por persona (o separadas por «;»): primero los números,
/// sueltos o en rangos, separados por comas o espacios, y después, si se quiere, el nombre de quien los tiene.
/// <list type="bullet">
/// <item>«1-50»: del 1 al 50, sin nombre.</item>
/// <item>«51 María López»: el 51, de María López.</item>
/// <item>«60, 61, 75 Juan»: esos tres, de Juan.</item>
/// </list>
/// </summary>
public static partial class LectorNumerosSorteo
{
    /// <summary>Lo más largo que se acepta escribir de una vez.</summary>
    public const int LongitudMaxima = 20_000;

    public static IReadOnlyList<NumeroSorteo> Leer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            throw new ReglaDeNegocioException("Escribe los números: por ejemplo, «1-50» o «12 María».", "Numeros");
        if (texto.Length > LongitudMaxima)
            throw new ReglaDeNegocioException($"Escribe como mucho {LongitudMaxima} caracteres de una vez.", "Numeros");

        var numeros = new List<NumeroSorteo>();
        var lineas = texto.Split(['\n', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var linea in lineas)
        {
            var posicion = 0;
            var deLaLinea = new List<int>();
            while (true)
            {
                while (posicion < linea.Length && (char.IsWhiteSpace(linea[posicion]) || linea[posicion] == ','))
                    posicion++;
                var trozo = NumeroORango().Match(linea, posicion);
                if (!trozo.Success || trozo.Index != posicion)
                    break;
                var desde = Cifra(trozo.Groups["desde"].Value);
                var hasta = trozo.Groups["hasta"].Success ? Cifra(trozo.Groups["hasta"].Value) : desde;
                if (hasta < desde)
                    (desde, hasta) = (hasta, desde);
                if (hasta - desde + 1 > Sorteo.MaximoParticipantes - numeros.Count - deLaLinea.Count)
                    throw new ReglaDeNegocioException($"Un sorteo admite hasta {Sorteo.MaximoParticipantes} participantes: escribe menos números.", "Numeros");
                for (var n = desde; n <= hasta; n++)
                    deLaLinea.Add(n);
                posicion = trozo.Index + trozo.Length;
            }

            if (deLaLinea.Count == 0)
                throw new ReglaDeNegocioException($"«{Recortar(linea)}» no empieza por un número. Escribe primero los números y después, si quieres, el nombre.", "Numeros");
            var nombre = Texto.Opcional(linea[posicion..].Trim(' ', ',', '-', '–', ':', '·', '.'));
            numeros.AddRange(deLaLinea.Select(n => new NumeroSorteo(n, nombre)));
        }

        return numeros;
    }

    private static int Cifra(string cifras) =>
        int.TryParse(cifras, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) && numero is >= 1 and <= Sorteo.NumeroMaximo
            ? numero
            : throw new ReglaDeNegocioException($"Los números van del 1 al {Sorteo.NumeroMaximo}: «{Recortar(cifras)}» no vale.", "Numeros");

    private static string Recortar(string texto) => texto.Length > 40 ? texto[..40] + "…" : texto;

    /// <summary>«12» o «1-50» (también con espacios alrededor del guion o con «–»). Acaba en espacio, coma o fin de línea.</summary>
    [GeneratedRegex(@"(?<desde>\d+)(?:\s*[-–]\s*(?<hasta>\d+))?(?=[\s,]|$)", RegexOptions.CultureInvariant)]
    private static partial Regex NumeroORango();
}
