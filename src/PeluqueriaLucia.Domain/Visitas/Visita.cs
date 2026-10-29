using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>
/// Una visita de un cliente: los servicios que se le hicieron, los productos que se llevó o se usaron en el servicio y cómo
/// se cobró. Mientras está pendiente se puede editar (servicios, productos y precios); cobrada, ya no cambia.
/// </summary>
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

    /// <summary>La cita de la agenda que se cobra con esta visita, si la hay.</summary>
    public Guid? ReservaId { get; private set; }

    /// <summary>Forma de pago prevista al registrar la visita; al cobrar queda la definitiva.</summary>
    public FormaPago FormaPago { get; private set; }

    public EstadoVisita Estado { get; private set; }

    public decimal? TotalEfectivo { get; private set; }

    public decimal? TotalTarjeta { get; private set; }

    /// <summary>Tipo de IVA conservado de los tickets antiguos; null utiliza la configuración del negocio.</summary>
    public decimal? PorcentajeIva { get; private set; }

    /// <summary>Servicios hechos.</summary>
    public IReadOnlyList<LineaVisita> Lineas => _lineas;

    /// <summary>Productos vendidos y usados en el servicio.</summary>
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
            visita.AgregarProducto(pedido.Producto, pedido.Cantidad, pedido.PrecioUnitario, pedido.Uso);
        return visita;
    }

    /// <summary>
    /// La visita de una cita de la agenda, para cobrarla: con el servicio de la cita si sigue en el catálogo con precio. Si
    /// no (una consulta, o un servicio archivado o con el precio por definir), empieza sin servicios y se añaden antes de
    /// cobrar. La forma de pago se elige al cobrar.
    /// </summary>
    public static Visita DesdeCita(Cliente cliente, Guid reservaId, DateTime fecha, Servicio? servicio)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        var visita = new Visita
        {
            Cliente = cliente,
            ClienteId = cliente.Id,
            ReservaId = reservaId,
            Fecha = fecha,
            FormaPago = FormaPago.Efectivo,
            Estado = EstadoVisita.Pendiente,
        };
        if (servicio is { Activo: true, Precio: > 0 })
            visita._lineas.Add(LineaVisita.Desde(servicio));
        return visita;
    }

    /// <summary>Enlaza con su cita una visita que se registró aparte, para que la agenda la reconozca.</summary>
    public void VincularCita(Guid reservaId)
    {
        if (ReservaId is { } actual && actual != reservaId)
            throw new ReglaDeNegocioException("Esta visita ya es de otra cita.");
        ReservaId = reservaId;
    }

    /// <summary>Añade un servicio del catálogo a una visita pendiente, a su precio o al indicado.</summary>
    public LineaVisita AgregarServicio(Servicio servicio, decimal? precio = null)
    {
        ArgumentNullException.ThrowIfNull(servicio);
        ComprobarPendiente("añadir servicios", "Servicios");
        if (!servicio.Activo)
            throw new ReglaDeNegocioException($"«{servicio.Nombre}» está archivado y ya no se ofrece.", "Servicios");

        var linea = LineaVisita.Desde(servicio, precio);
        _lineas.Add(linea);
        return linea;
    }

    /// <summary>Quita un servicio de una visita pendiente. La visita no puede quedarse vacía.</summary>
    public void QuitarServicio(int lineaId)
    {
        ComprobarPendiente("quitar servicios", "Servicios");
        var linea = _lineas.FirstOrDefault(l => l.Id == lineaId)
            ?? throw new ReglaDeNegocioException("Ese servicio ya no está en la visita.", "Servicios");
        if (_lineas.Count == 1 && _productos.Count == 0)
            throw new ReglaDeNegocioException("La visita se quedaría vacía. Si ya no hace falta, elimínala.", "Servicios");
        _lineas.Remove(linea);
    }

    /// <summary>
    /// Cambia los precios de una visita pendiente (por ejemplo, justo antes de cobrarla): el de cada línea de servicio y el
    /// de cada unidad de los productos vendidos, por número de línea. Las líneas que ya no están se pasan por alto, y lo
    /// usado en el servicio sigue a 0,00 €. Si algún precio no vale, no cambia ninguno.
    /// </summary>
    public void CambiarPrecios(IReadOnlyDictionary<int, decimal>? servicios, IReadOnlyDictionary<int, decimal>? productos)
    {
        var deServicios = (servicios ?? new Dictionary<int, decimal>())
            .Select(p => (Linea: _lineas.FirstOrDefault(l => l.Id == p.Key), Precio: p.Value))
            .Where(p => p.Linea is not null)
            .ToList();
        var deProductos = (productos ?? new Dictionary<int, decimal>())
            .Select(p => (Linea: _productos.FirstOrDefault(l => l.Id == p.Key), Precio: p.Value))
            .Where(p => p.Linea is { Incluido: false })
            .ToList();
        if (deServicios.Count == 0 && deProductos.Count == 0)
            return;
        ComprobarPendiente("cambiar los precios", "Precios");

        // Primero se comprueban todos; solo si valen, se cambian
        foreach (var (linea, precio) in deServicios)
            LineaVisita.PrecioValido(precio, linea!.Nombre);
        foreach (var (linea, precio) in deProductos)
            LineaProductoVisita.PrecioValido(precio, linea!.Nombre);
        deServicios.ForEach(p => p.Linea!.CambiarPrecio(p.Precio));
        deProductos.ForEach(p => p.Linea!.CambiarPrecio(p.Precio));
    }

    /// <summary>
    /// Añade unidades de un producto a una visita pendiente: vendido o usado en el servicio (incluido, a 0,00 €). Si ya
    /// estaba igual (mismo uso y precio), se suman a esa línea. Lo vendido y lo usado que se ha terminado sale del stock al
    /// cobrar; si no hay bastante, queda en negativo.
    /// </summary>
    public LineaProductoVisita AgregarProducto(Producto producto, int cantidad, decimal? precioUnitario = null, UsoProducto uso = UsoProducto.Venta)
    {
        ArgumentNullException.ThrowIfNull(producto);
        ComprobarPendiente("añadir productos");
        if (!producto.Activo)
            throw new ReglaDeNegocioException($"«{producto.Nombre}» está archivado y ya no se vende.", "Productos");

        var nueva = LineaProductoVisita.Desde(producto, cantidad, precioUnitario, uso);

        var existente = _productos.FirstOrDefault(p => p.ProductoId == producto.Id && p.Uso == nueva.Uso && p.PrecioUnitario == nueva.PrecioUnitario);
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
    /// Lo vendido y lo usado en el servicio que se ha terminado se descuenta del stock de <paramref name="existencias"/> (los
    /// productos de la visita): devuelve un movimiento de venta por producto vendido y otro de uso por producto terminado. Lo
    /// usado que sigue en uso no mueve el stock. Si falta stock, queda en negativo.
    /// </summary>
    public IReadOnlyList<MovimientoStock> Cobrar(
        FormaPago formaPago, decimal? efectivo, decimal? tarjeta,
        IReadOnlyCollection<Producto>? existencias = null, DateTime? ahora = null, int? administradorId = null)
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
        var salidas = _productos
            .Where(p => p.DescuentaStock)
            .GroupBy(p => (p.ProductoId, Venta: !p.Incluido))
            .Select(g => (Producto: existencias?.FirstOrDefault(e => e.Id == g.Key.ProductoId)
                ?? throw new InvalidOperationException($"Falta el producto {g.Key.ProductoId} para descontar su stock al cobrar la visita."),
                g.Key.Venta, Unidades: g.Sum(p => p.Cantidad)))
            .ToList();
        var momento = ahora ?? Fecha;
        var movimientos = salidas
            .Select(s => s.Venta
                ? s.Producto.Vender(s.Unidades, Id, momento, administradorId)
                : s.Producto.UsarEnVisita(s.Unidades, Id, momento, administradorId))
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

    private void ComprobarPendiente(string accion, string campo = "Productos")
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException($"La visita ya está cobrada: no se pueden {accion}.", campo);
    }

    private static void ComprobarFormaPago(FormaPago formaPago)
    {
        if (!Enum.IsDefined(formaPago))
            throw new ReglaDeNegocioException("La forma de pago no es válida.", nameof(FormaPago));
    }

    private static decimal Redondear(decimal importe) => decimal.Round(importe, 2, MidpointRounding.AwayFromZero);
}
