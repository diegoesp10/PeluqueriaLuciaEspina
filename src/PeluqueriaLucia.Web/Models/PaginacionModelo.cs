namespace PeluqueriaLucia.Web.Models;

/// <param name="Enlace">Construye la URL de una página conservando los filtros de la vista.</param>
public sealed record PaginacionModelo(int Numero, int TotalPaginas, int TotalElementos, Func<int, string> Enlace);
