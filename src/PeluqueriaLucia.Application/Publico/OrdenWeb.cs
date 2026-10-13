using PeluqueriaLucia.Application.Abstracciones;

namespace PeluqueriaLucia.Application.Publico;

/// <summary>
/// Orden de la web: categorías y servicios llevan su posición (1, 2, 3…). Se cambia con las flechas de subir y bajar;
/// lo nuevo entra al final. Tras cada movimiento se renumera todo seguido, sin huecos ni repetidos.
/// </summary>
public static class OrdenWeb
{
    /// <summary>Posición para un servicio que entra en <paramref name="categoriaId"/>: detrás del último.</summary>
    public static async Task<int> SiguienteAsync(IServicioRepositorio servicios, int categoriaId, CancellationToken ct) =>
        (await servicios.ListarAsync(null, true, ct)).Where(s => s.CategoriaPublicaId == categoriaId)
            .Select(s => s.OrdenPublico).DefaultIfEmpty(0).Max() + 1;

    /// <summary>
    /// Mueve el elemento <paramref name="id"/> una posición (<paramref name="desplazamiento"/> -1 sube, +1 baja) y devuelve
    /// la lista en su nuevo orden. Si ya está en el extremo, no cambia nada.
    /// </summary>
    public static List<T> Mover<T>(IEnumerable<T> ordenados, Func<T, int> clave, int id, int desplazamiento)
    {
        var lista = ordenados.ToList();
        var actual = lista.FindIndex(x => clave(x) == id);
        var destino = actual + Math.Sign(desplazamiento);
        if (actual < 0 || destino < 0 || destino >= lista.Count) return lista;
        (lista[actual], lista[destino]) = (lista[destino], lista[actual]);
        return lista;
    }
}
