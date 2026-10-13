namespace PeluqueriaLucia.Application.Comun;

/// <summary>Un trozo de un listado largo, con lo necesario para pintar la paginación.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Elementos, int Numero, int Tamano, int TotalElementos)
{
    public int TotalPaginas => TotalElementos == 0 ? 1 : (int)Math.Ceiling(TotalElementos / (double)Tamano);

    public bool HayAnterior => Numero > 1;

    public bool HaySiguiente => Numero < TotalPaginas;

    /// <summary>Corrige números de página fuera de rango (0, negativos…) para no romper la consulta.</summary>
    public static int Normalizar(int numero) => Math.Max(1, numero);
}
