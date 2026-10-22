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

/// <summary>Búsqueda del inventario: palabras en referencia, código de barras, nombre, marca o categoría.</summary>
public sealed record FiltroProductos(string? Texto = null, EstadoInventario Estado = EstadoInventario.Activos, string? Categoria = null);

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

/// <summary>Ficha del producto con el stock con el que se da de alta.</summary>
public sealed record DatosProducto(FichaProducto Ficha, int StockInicial = 0);

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
