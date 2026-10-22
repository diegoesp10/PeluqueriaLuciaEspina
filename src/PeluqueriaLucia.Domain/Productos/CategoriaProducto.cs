using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Productos;

/// <summary>
/// Categoría de la tienda (Champús, Mascarillas, Aceites…). Todo producto pertenece a una. Eliminarla es un borrado lógico:
/// la categoría se conserva marcada como eliminada y sus productos quedan archivados con su stock (no se venden ni cuentan en
/// el inventario). Al recuperarla vuelven a la venta los productos que se archivaron con ella.
/// </summary>
public sealed class CategoriaProducto
{
    public const int LongitudNombre = 60;

    private CategoriaProducto()
    {
    }

    public int Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>false: eliminada (borrado lógico). No se ofrece al crear o editar productos.</summary>
    public bool Activa { get; private set; } = true;

    public DateTime CreadaEn { get; private set; }

    public DateTime? EliminadaEn { get; private set; }

    public static CategoriaProducto Crear(string? nombre, DateTime ahora)
    {
        var categoria = new CategoriaProducto { CreadaEn = ahora, Activa = true };
        categoria.Renombrar(nombre);
        return categoria;
    }

    public void Renombrar(string? nombre) =>
        Nombre = Texto.Obligatorio(nombre, LongitudNombre, "El nombre de la categoría", nameof(Nombre));

    /// <summary>Borrado lógico: la categoría deja de ofrecerse y sus productos a la venta se archivan. Devuelve cuántos.</summary>
    public int Eliminar(IReadOnlyCollection<Producto> productos, DateTime ahora)
    {
        if (!Activa)
            throw new ReglaDeNegocioException($"La categoría «{Nombre}» ya estaba eliminada.");
        var suyos = productos.Where(p => p.CategoriaId == Id && p.Activo).ToList();
        suyos.ForEach(p => p.ArchivarPorCategoria(ahora));
        Activa = false;
        EliminadaEn = ahora;
        return suyos.Count;
    }

    /// <summary>Vuelve a estar disponible, y con ella los productos que se archivaron al eliminarla. Devuelve cuántos.</summary>
    public int Recuperar(IReadOnlyCollection<Producto> productos, DateTime ahora)
    {
        if (Activa)
            throw new ReglaDeNegocioException($"La categoría «{Nombre}» no está eliminada.");
        Activa = true;
        EliminadaEn = null;
        var archivados = productos.Where(p => p.CategoriaId == Id && p.ArchivadoConCategoria).ToList();
        archivados.ForEach(p => p.Reactivar(ahora));
        return archivados.Count;
    }
}
