using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Sorteos;

/// <summary>Lo que escribe el salón de un producto de sorteo, en español y en inglés (se ve en la web al anunciar el sorteo).</summary>
public sealed record FichaProductoSorteo(string? Nombre, string? NombreEn, string? Descripcion, string? DescripcionEn);

/// <summary>
/// Algo que el salón sortea y que no es de la peluquería (una cena para dos, unas entradas, una cesta de un comercio
/// vecino…): se da de alta con su nombre y su descripción y se añade a los sorteos junto a servicios del catálogo y
/// productos de la tienda. Eliminarlo es un borrado lógico: deja de ofrecerse, pero los sorteos en los que estuvo lo conservan.
/// </summary>
public sealed class ProductoSorteo
{
    public const int LongitudNombre = PremioSorteo.LongitudNombre;
    public const int LongitudDescripcion = PremioSorteo.LongitudDescripcion;

    private ProductoSorteo()
    {
    }

    public int Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string? NombreEn { get; private set; }

    public string? Descripcion { get; private set; }

    public string? DescripcionEn { get; private set; }

    /// <summary>false: eliminado (borrado lógico). No se ofrece al añadir premios.</summary>
    public bool Activo { get; private set; } = true;

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    public DateTime? EliminadoEn { get; private set; }

    public string NombreEnIdioma(bool ingles) => ingles && NombreEn is { Length: > 0 } ? NombreEn : Nombre;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;

    public static ProductoSorteo Crear(FichaProductoSorteo ficha, DateTime ahora)
    {
        var producto = new ProductoSorteo { Activo = true, CreadoEn = ahora };
        producto.Editar(ficha, ahora);
        return producto;
    }

    public void Editar(FichaProductoSorteo ficha, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(ficha);
        var nombre = Texto.Obligatorio(ficha.Nombre, LongitudNombre, "El nombre", nameof(Nombre));
        // Se publica en la web con el sorteo: como todo lo nuevo, en los dos idiomas
        var nombreEn = Texto.Obligatorio(ficha.NombreEn, LongitudNombre, "El nombre en inglés", nameof(NombreEn));
        var descripcion = Texto.Opcional(ficha.Descripcion);
        var descripcionEn = Texto.Opcional(ficha.DescripcionEn);
        if (descripcion?.Length > LongitudDescripcion)
            throw new ReglaDeNegocioException($"La descripción admite hasta {LongitudDescripcion} caracteres.", nameof(Descripcion));
        if (descripcionEn?.Length > LongitudDescripcion)
            throw new ReglaDeNegocioException($"La descripción en inglés admite hasta {LongitudDescripcion} caracteres.", nameof(DescripcionEn));
        if (descripcion is not null && descripcionEn is null)
            throw new ReglaDeNegocioException("Escribe también la descripción en inglés.", nameof(DescripcionEn));

        Nombre = nombre;
        NombreEn = nombreEn;
        Descripcion = descripcion;
        DescripcionEn = descripcionEn;
        ActualizadoEn = ahora;
    }

    /// <summary>Deja de ofrecerse para nuevos sorteos; los sorteos en los que ya está lo conservan.</summary>
    public void Eliminar(DateTime ahora)
    {
        if (!Activo)
            throw new ReglaDeNegocioException($"«{Nombre}» ya estaba eliminado.");
        Activo = false;
        EliminadoEn = ahora;
        ActualizadoEn = ahora;
    }

    public void Recuperar(DateTime ahora)
    {
        if (Activo)
            throw new ReglaDeNegocioException($"«{Nombre}» no está eliminado.");
        Activo = true;
        EliminadoEn = null;
        ActualizadoEn = ahora;
    }
}
