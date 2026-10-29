using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>
/// Producto que se pone en una visita: cuántas unidades, si se vende o se ha usado en el servicio y, si se vende y cambia,
/// a qué precio cada una.
/// </summary>
public sealed record ProductoPedido(Producto Producto, int Cantidad, decimal? PrecioUnitario = null, UsoProducto Uso = UsoProducto.Venta);

/// <summary>
/// Un producto de una visita: vendido o usado en el servicio (tinte, oxidante…), que va incluido en él a 0,00 €. Como las
/// líneas de servicios, guarda referencia, nombre y precio del momento: cambiar o archivar el producto no altera tickets,
/// totales ni exportaciones anteriores.
/// </summary>
public sealed class LineaProductoVisita
{
    public const int CantidadMaxima = 99;

    private LineaProductoVisita()
    {
    }

    public int Id { get; private set; }

    public int VisitaId { get; private set; }

    public int ProductoId { get; private set; }

    public string Referencia { get; private set; } = string.Empty;

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Precio de cada unidad, impuestos incluidos.</summary>
    public decimal PrecioUnitario { get; private set; }

    public int Cantidad { get; private set; }

    /// <summary>Precio por cantidad. Se guarda para que los resúmenes se calculen en la base de datos.</summary>
    public decimal Importe { get; private set; }

    public UsoProducto Uso { get; private set; }

    /// <summary>Usado en el servicio: va incluido en él y no se cobra (0,00 €).</summary>
    public bool Incluido => Uso != UsoProducto.Venta;

    /// <summary>Sale del stock al cobrar: lo vendido y lo usado que se ha terminado.</summary>
    public bool DescuentaStock => Uso != UsoProducto.ServicioEnUso;

    internal static LineaProductoVisita Desde(Producto producto, int cantidad, decimal? precio, UsoProducto uso = UsoProducto.Venta)
    {
        if (!Enum.IsDefined(uso))
            throw new ReglaDeNegocioException($"Indica si «{producto.Nombre}» se vende o se ha usado en el servicio.", "Productos");
        // Lo usado en el servicio va incluido en él: sale en la visita y en el ticket, pero a 0,00 €
        var unitario = uso == UsoProducto.Venta ? PrecioValido(precio ?? producto.PrecioVenta, producto.Nombre) : 0;
        ComprobarCantidad(cantidad, producto.Nombre);
        return new()
        {
            ProductoId = producto.Id,
            Referencia = producto.Referencia,
            Nombre = producto.Nombre,
            Uso = uso,
            PrecioUnitario = unitario,
            Cantidad = cantidad,
            Importe = unitario * cantidad,
        };
    }

    /// <summary>Cambia el precio de cada unidad vendida (hasta cobrar). Lo usado en el servicio no tiene precio.</summary>
    internal void CambiarPrecio(decimal precio)
    {
        if (Incluido)
            throw new ReglaDeNegocioException($"«{Nombre}» va incluido en el servicio: no se cobra.", "Productos");
        PrecioUnitario = PrecioValido(precio, Nombre);
        Importe = PrecioUnitario * Cantidad;
    }

    /// <summary>El precio de una unidad redondeado a céntimos, entre 0,01 y el máximo de los productos.</summary>
    internal static decimal PrecioValido(decimal precio, string nombre)
    {
        var unitario = decimal.Round(precio, 2, MidpointRounding.AwayFromZero);
        if (unitario is <= 0 or > Producto.PrecioMaximo)
            throw new ReglaDeNegocioException($"Indica un precio entre 0,01 y 9.999,99 € para «{nombre}».", "Productos");
        return unitario;
    }

    internal void Sumar(int cantidad)
    {
        ComprobarCantidad(Cantidad + cantidad, Nombre);
        Cantidad += cantidad;
        Importe = PrecioUnitario * Cantidad;
    }

    private static void ComprobarCantidad(int cantidad, string nombre)
    {
        if (cantidad is < 1 or > CantidadMaxima)
            throw new ReglaDeNegocioException($"Indica entre 1 y {CantidadMaxima} unidades de «{nombre}».", "Productos");
    }
}
