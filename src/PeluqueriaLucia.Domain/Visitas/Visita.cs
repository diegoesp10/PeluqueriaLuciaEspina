using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>Una visita de un cliente: los servicios que se le hicieron, los productos que se llevó y cómo se cobró.</summary>
public sealed class Visita
{
    private readonly List<LineaVisita> _lineas = [];
    private readonly List<LineaProductoVisita> _productos = [];

    private Visita()
    {
    }

    public int Id { get; private set; }

    public DateTime Fecha { get; private set; }

    public int? ClienteId { get; private set; }

    public Cliente? Cliente { get; private set; }

    /// <summary>Forma de pago prevista al registrar la visita; al cobrar queda la definitiva.</summary>
    public FormaPago FormaPago { get; private set; }

    public EstadoVisita Estado { get; private set; }

    public decimal? TotalEfectivo { get; private set; }

    public decimal? TotalTarjeta { get; private set; }

    /// <summary>Tipo de IVA conservado de los tickets antiguos; null utiliza la configuración del negocio.</summary>
    public decimal? PorcentajeIva { get; private set; }

    /// <summary>Servicios hechos.</summary>
    public IReadOnlyList<LineaVisita> Lineas => _lineas;

    /// <summary>Productos vendidos.</summary>
    public IReadOnlyList<LineaProductoVisita> Productos => _productos;

    public decimal TotalServicios => _lineas.Sum(l => l.Precio);

    public decimal TotalProductos => _productos.Sum(p => p.Importe);

    public decimal Total => TotalServicios + TotalProductos;

    public int UnidadesProductos => _productos.Sum(p => p.Cantidad);

    public bool EstaCobrada => Estado == EstadoVisita.Cobrada;

    public static Visita Registrar(
        Cliente cliente, DateTime fecha, FormaPago formaPago, IReadOnlyCollection<Servicio> servicios,
        IReadOnlyDictionary<int, decimal>? precios = null, IReadOnlyCollection<ProductoPedido>? productos = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        productos ??= [];
        if (servicios.Count == 0 && productos.Count == 0)
            throw new ReglaDeNegocioException("Elige al menos un servicio o un producto.", "Servicios");
        if (servicios.FirstOrDefault(s => !s.Activo) is { } archivado)
            throw new ReglaDeNegocioException($"«{archivado.Nombre}» está archivado y ya no se ofrece.", "Servicios");
        ComprobarFormaPago(formaPago);

        var visita = new Visita
        {
            Cliente = cliente,
            ClienteId = cliente.Id,
            Fecha = fecha,
            FormaPago = formaPago,
            Estado = EstadoVisita.Pendiente,
        };
        visita._lineas.AddRange(servicios.Select(s => LineaVisita.Desde(s, precios is not null && precios.TryGetValue(s.Id, out var precio) ? precio : null)));
        foreach (var pedido in productos)
            visita.AgregarProducto(pedido.Producto, pedido.Cantidad, pedido.PrecioUnitario);
        return visita;
    }

    /// <summary>
    /// Añade unidades de un producto a una visita pendiente. Si ya estaba al mismo precio, se suman a esa línea.
    /// El stock se descuenta al cobrar; si no hay bastante, queda en negativo.
    /// </summary>
    public LineaProductoVisita AgregarProducto(Producto producto, int cantidad, decimal? precioUnitario = null)
    {
        ArgumentNullException.ThrowIfNull(producto);
        ComprobarPendiente("añadir productos");
        if (!producto.Activo)
            throw new ReglaDeNegocioException($"«{producto.Nombre}» está archivado y ya no se vende.", "Productos");

        var nueva = LineaProductoVisita.Desde(producto, cantidad, precioUnitario);

        var existente = _productos.FirstOrDefault(p => p.ProductoId == producto.Id && p.PrecioUnitario == nueva.PrecioUnitario);
        if (existente is null)
        {
            _productos.Add(nueva);
            return nueva;
        }

        existente.Sumar(cantidad);
        return existente;
    }

    /// <summary>Quita una línea de producto de una visita pendiente. La visita no puede quedarse vacía.</summary>
    public void QuitarProducto(int lineaId)
    {
        ComprobarPendiente("quitar productos");
        var linea = _productos.FirstOrDefault(p => p.Id == lineaId)
            ?? throw new ReglaDeNegocioException("Ese producto ya no está en la visita.", "Productos");
        if (_lineas.Count == 0 && _productos.Count == 1)
            throw new ReglaDeNegocioException("La visita se quedaría vacía. Si ya no hace falta, elimínala.", "Productos");
        _productos.Remove(linea);
    }

    /// <summary>
    /// Cobra la visita. En pago mixto, efectivo y tarjeta tienen que sumar exactamente el total;
    /// en los demás casos el importe completo va a la forma elegida.
    /// Los productos vendidos se descuentan del stock de <paramref name="existencias"/> (los productos de la visita), y los
    /// <paramref name="usados"/> en el servicio también, sin cobrarse. Devuelve un movimiento por producto vendido y otro por
    /// producto usado. Si falta stock, queda en negativo.
    /// </summary>
    public IReadOnlyList<MovimientoStock> Cobrar(
        FormaPago formaPago, decimal? efectivo, decimal? tarjeta,
        IReadOnlyCollection<Producto>? existencias = null, DateTime? ahora = null, int? administradorId = null,
        IReadOnlyCollection<ProductoUsado>? usados = null)
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException("Esta visita ya estaba cobrada.");
        if (_lineas.Count == 0 && _productos.Count == 0)
            throw new ReglaDeNegocioException("La visita no tiene servicios ni productos que cobrar.");
        ComprobarFormaPago(formaPago);

        var total = Total;
        decimal enEfectivo, conTarjeta;
        switch (formaPago)
        {
            case FormaPago.Mixto:
                enEfectivo = Redondear(efectivo ?? 0);
                conTarjeta = Redondear(tarjeta ?? 0);
                if (enEfectivo < 0 || conTarjeta < 0)
                    throw new ReglaDeNegocioException("Los importes no pueden ser negativos.");
                if (enEfectivo == 0 || conTarjeta == 0)
                    throw new ReglaDeNegocioException("En un pago mixto hay que poner una parte en efectivo y otra con tarjeta.");
                if (enEfectivo + conTarjeta != total)
                    throw new ReglaDeNegocioException(
                        $"Efectivo y tarjeta suman {enEfectivo + conTarjeta:0.00} € y el total es {total:0.00} €.");
                break;
            case FormaPago.Tarjeta:
                enEfectivo = 0;
                conTarjeta = total;
                break;
            default:
                enEfectivo = total;
                conTarjeta = 0;
                break;
        }

        // Todo se comprueba antes de descontar nada: si algo no vale, la visita y el stock quedan como estaban
        var ventas = _productos
            .GroupBy(p => p.ProductoId)
            .Select(g => (Producto: existencias?.FirstOrDefault(e => e.Id == g.Key)
                ?? throw new InvalidOperationException($"Falta el producto {g.Key} para descontar su stock al cobrar la visita."),
                Unidades: g.Sum(p => p.Cantidad)))
            .ToList();
        var usos = (usados ?? [])
            .GroupBy(u => u.Producto.Id)
            .Select(g => (Producto: g.First().Producto, Unidades: g.Sum(u => u.Cantidad)))
            .ToList();
        var malo = usos.FirstOrDefault(u => u.Unidades is < 1 or > LineaProductoVisita.CantidadMaxima);
        if (malo.Producto is not null)
            throw new ReglaDeNegocioException($"Indica entre 1 y {LineaProductoVisita.CantidadMaxima} unidades utilizadas de «{malo.Producto.Nombre}».", "Usados");
        var momento = ahora ?? Fecha;
        var movimientos = ventas.Select(v => v.Producto.Vender(v.Unidades, Id, momento, administradorId))
            .Concat(usos.Select(u => u.Producto.UsarEnVisita(u.Unidades, Id, momento, administradorId)))
            .ToList();

        TotalEfectivo = enEfectivo;
        TotalTarjeta = conTarjeta;
        FormaPago = formaPago;
        Estado = EstadoVisita.Cobrada;
        return movimientos;
    }

    /// <summary>Una visita cobrada forma parte de la caja y no se puede borrar.</summary>
    public void ComprobarQueSePuedeEliminar()
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException("No se puede eliminar una visita que ya está cobrada.");
    }

    private void ComprobarPendiente(string accion)
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException($"La visita ya está cobrada: no se pueden {accion}.", "Productos");
    }

    private static void ComprobarFormaPago(FormaPago formaPago)
    {
        if (!Enum.IsDefined(formaPago))
            throw new ReglaDeNegocioException("La forma de pago no es válida.", nameof(FormaPago));
    }

    private static decimal Redondear(decimal importe) => decimal.Round(importe, 2, MidpointRounding.AwayFromZero);
}
