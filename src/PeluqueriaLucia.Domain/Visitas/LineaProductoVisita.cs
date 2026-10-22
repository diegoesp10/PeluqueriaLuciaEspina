using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>Producto que se quiere vender en una visita: cuántas unidades y, si cambia, a qué precio cada una.</summary>
public sealed record ProductoPedido(Producto Producto, int Cantidad, decimal? PrecioUnitario = null);

/// <summary>Producto gastado en el servicio de una visita (tinte, oxidante…). No se cobra: solo sale del stock al cobrar.</summary>
public sealed record ProductoUsado(Producto Producto, int Cantidad);

/// <summary>
/// Un producto vendido en una visita. Como las líneas de servicios, guarda referencia, nombre y precio del momento:
/// cambiar o archivar el producto no altera tickets, totales ni exportaciones anteriores.
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

    internal static LineaProductoVisita Desde(Producto producto, int cantidad, decimal? precio)
    {
        var unitario = decimal.Round(precio ?? producto.PrecioVenta, 2, MidpointRounding.AwayFromZero);
        if (unitario is <= 0 or > Producto.PrecioMaximo)
            throw new ReglaDeNegocioException($"Indica un precio entre 0,01 y 9.999,99 € para «{producto.Nombre}».", "Productos");
        ComprobarCantidad(cantidad, producto.Nombre);
        return new()
        {
            ProductoId = producto.Id,
            Referencia = producto.Referencia,
            Nombre = producto.Nombre,
            PrecioUnitario = unitario,
            Cantidad = cantidad,
            Importe = unitario * cantidad,
        };
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
