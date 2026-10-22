using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Application.Abstracciones;

public interface IProductoRepositorio
{
    /// <summary>Producto con sus fotos, para modificarlo.</summary>
    Task<Producto?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Varios productos con sus fotos, para venderlos en una visita.</summary>
    Task<IReadOnlyList<Producto>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Inventario filtrado en la base de datos, ordenado por nombre.</summary>
    Task<Pagina<Producto>> BuscarAsync(FiltroProductos filtro, int pagina, int tamano, CancellationToken ct = default);

    /// <summary>Productos activos, para elegirlos en una visita. Los agotados también, para avisar.</summary>
    Task<IReadOnlyList<Producto>> ListarActivosAsync(CancellationToken ct = default);

    /// <summary>Activos que hay que reponer, primero los agotados y los que más lejos están del mínimo.</summary>
    Task<IReadOnlyList<Producto>> PorReponerAsync(int maximo, CancellationToken ct = default);

    Task<ResumenInventario> ResumirAsync(CancellationToken ct = default);

    /// <summary>Marcas ya escritas, para sugerirlas al rellenar la ficha.</summary>
    Task<IReadOnlyList<string>> MarcasAsync(CancellationToken ct = default);

    /// <summary>Categorías por nombre; las eliminadas, solo si se piden.</summary>
    Task<IReadOnlyList<CategoriaProducto>> CategoriasAsync(bool incluirEliminadas, CancellationToken ct = default);

    /// <summary>Todas las categorías (también las eliminadas) con sus productos y unidades.</summary>
    Task<IReadOnlyList<ResumenCategoria>> ResumenCategoriasAsync(CancellationToken ct = default);

    Task<CategoriaProducto?> CategoriaAsync(int id, CancellationToken ct = default);

    /// <summary>La categoría activa con ese nombre (sin distinguir mayúsculas), salvo <paramref name="excluirId"/>.</summary>
    Task<CategoriaProducto?> CategoriaActivaPorNombreAsync(string nombre, int? excluirId, CancellationToken ct = default);

    /// <summary>Productos de una categoría (activos y archivados), para archivarlos o recuperarlos con ella.</summary>
    Task<IReadOnlyList<Producto>> ProductosDeCategoriaAsync(int categoriaId, CancellationToken ct = default);

    void AgregarCategoria(CategoriaProducto categoria);

    Task<bool> ExisteReferenciaAsync(string referencia, int? excluirId, CancellationToken ct = default);

    Task<bool> ExisteCodigoBarrasAsync(string codigo, int? excluirId, CancellationToken ct = default);

    /// <summary>Aparece en alguna visita, cobrada o pendiente.</summary>
    Task<bool> TieneVentasAsync(int id, CancellationToken ct = default);

    /// <summary>Usos en el servicio de una visita, con su producto.</summary>
    Task<IReadOnlyList<MovimientoStock>> UsadosEnVisitaAsync(int visitaId, CancellationToken ct = default);

    /// <summary>Historial de stock de un producto, lo más reciente primero.</summary>
    Task<Pagina<MovimientoStock>> MovimientosAsync(int productoId, int pagina, int tamano, CancellationToken ct = default);

    void Agregar(Producto producto);

    void AgregarMovimientos(IEnumerable<MovimientoStock> movimientos);

    /// <summary>Borra el producto con sus fotos y su historial de stock.</summary>
    void Eliminar(Producto producto);
}

/// <summary>
/// Fotos de productos en una carpeta privada (no se sirven como ficheros estáticos). Cada foto se guarda en dos tamaños:
/// la grande para la ficha y una miniatura para listados y visitas.
/// </summary>
public interface IFotosProductos
{
    public const long TamanoMaximo = 10 * 1024 * 1024;

    /// <summary>Comprueba que sea una foto de verdad, la endereza, la reduce, le quita los metadatos (GPS…) y la guarda.</summary>
    Task<Resultado<Guid>> GuardarAsync(Stream contenido, CancellationToken ct = default);

    /// <summary>Ruta del fichero, o <c>null</c> si no existe.</summary>
    string? RutaFisica(Guid id, bool miniatura);

    /// <summary>Borra los dos tamaños. No falla si ya no están.</summary>
    void Eliminar(Guid id);
}
