using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Application.Productos;

/// <summary>Qué productos enseñar en el inventario.</summary>
public enum EstadoInventario
{
    /// <summary>Los que se venden (no archivados).</summary>
    Activos,

    /// <summary>Activos que hay que reponer: agotados o con el stock mínimo o menos.</summary>
    PorReponer,

    Archivados,

    Todos,
}

/// <summary>Búsqueda del inventario: palabras en referencia, código de barras, nombre, marca o categoría; y, si se elige, una categoría.</summary>
public sealed record FiltroProductos(string? Texto = null, EstadoInventario Estado = EstadoInventario.Activos, int? CategoriaId = null);

/// <summary>
/// La categoría de un producto: una existente (<paramref name="Id"/>) o una nueva escrita en el formulario
/// (<paramref name="Nueva"/>); si ya hay una activa con ese nombre se usa esa, y si no, se crea al guardar.
/// </summary>
public sealed record CategoriaElegida(int? Id, string? Nueva = null);

/// <summary>Una categoría con sus cifras, para la pantalla de categorías y el aviso antes de eliminarla.</summary>
/// <param name="Productos">Productos a la venta en la categoría.</param>
/// <param name="Unidades">Unidades en stock de esos productos.</param>
/// <param name="Archivados">Productos archivados al eliminarla (vuelven a la venta si se recupera).</param>
/// <param name="UnidadesArchivadas">Unidades en stock de los archivados con ella.</param>
public sealed record ResumenCategoria(CategoriaProducto Categoria, int Productos, int Unidades, int Archivados, int UnidadesArchivadas);

/// <summary>Cifras del inventario de productos activos. El stock negativo cuenta como agotado, no resta unidades ni valor.</summary>
/// <param name="Unidades">Unidades en el almacén (solo las de productos con stock).</param>
/// <param name="Agotados">Sin unidades o en negativo.</param>
/// <param name="ValorVenta">Unidades en stock por su precio de venta.</param>
/// <param name="ValorCoste">Unidades en stock por su precio de coste (los productos sin coste no suman).</param>
public sealed record ResumenInventario(int Productos, int Unidades, int BajoStock, int Agotados, decimal ValorVenta, decimal ValorCoste)
{
    public static ResumenInventario Vacio { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>Productos que hay que reponer: agotados o por debajo del mínimo.</summary>
    public int PorReponer => BajoStock + Agotados;
}

/// <summary>Resultado de eliminar o recuperar una categoría: su nombre y cuántos productos se archivaron o volvieron a la venta.</summary>
public sealed record CambioCategoria(string Nombre, int Productos);

/// <summary>Ficha del producto, su categoría y el stock con el que se da de alta.</summary>
public sealed record DatosProducto(FichaProducto Ficha, CategoriaElegida Categoria, int StockInicial = 0);

/// <summary>Cambios en las fotos al editar: las nuevas se añaden al final; la principal puede ser una existente.</summary>
public sealed record CambiosFotos(IReadOnlyList<Abstracciones.ArchivoImagen> Nuevas, IReadOnlyCollection<Guid> Quitar, Guid? Principal = null)
{
    public static CambiosFotos Ninguno { get; } = new([], []);
}

/// <summary>Un movimiento de stock que registra el salón a mano.</summary>
public enum MovimientoManual
{
    /// <summary>Llega mercancía: suma unidades.</summary>
    Entrada,

    /// <summary>Se gasta en el salón: resta unidades.</summary>
    UsoInterno,

    /// <summary>Recuento: el stock pasa a ser la cantidad contada.</summary>
    Recuento,
}
