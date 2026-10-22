using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Productos;

/// <summary>Datos de la ficha de un producto que escribe el salón; el stock no está aquí porque solo cambia con movimientos.</summary>
public sealed record FichaProducto(
    string? Referencia,
    string? CodigoBarras,
    string? Nombre,
    string? Marca,
    string? Categoria,
    string? Descripcion,
    decimal PrecioVenta,
    decimal? PrecioCoste,
    int StockMinimo);

/// <summary>
/// Un producto de la tienda del salón (champús, mascarillas, aceites…). El stock nunca se escribe a mano: cada entrada,
/// venta, uso en el salón o recuento deja un <see cref="MovimientoStock"/>, así el historial explica siempre el stock actual.
/// Vender o usar más de lo que hay no se impide: el stock queda en negativo (−1, −2…) y el producto aparece por reponer.
/// </summary>
public sealed class Producto
{
    public const int LongitudReferencia = 40;
    public const int LongitudCodigoBarras = 14;
    public const int LongitudNombre = 120;
    public const int LongitudMarca = 60;
    public const int LongitudCategoria = 60;
    public const int LongitudDescripcion = 1000;
    public const decimal PrecioMaximo = 9999.99m;
    public const int StockMaximo = 99999;
    public const int MaximoFotos = 6;

    private readonly List<FotoProducto> _fotos = [];

    private Producto()
    {
    }

    public int Id { get; private set; }

    /// <summary>Código interno del salón o del proveedor, único y en mayúsculas («CH-ARG-250»).</summary>
    public string Referencia { get; private set; } = string.Empty;

    /// <summary>EAN/UPC del envase (8 a 14 cifras), para buscarlo con el lector.</summary>
    public string? CodigoBarras { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string? Marca { get; private set; }

    public string? Categoria { get; private set; }

    public string? Descripcion { get; private set; }

    /// <summary>Precio de venta al público, con impuestos incluidos, como los servicios.</summary>
    public decimal PrecioVenta { get; private set; }

    /// <summary>Lo que cuesta al salón; sirve para valorar el inventario y ver el margen.</summary>
    public decimal? PrecioCoste { get; private set; }

    public int Stock { get; private set; }

    /// <summary>Por debajo o igual a esta cantidad se avisa de que hay que reponer (0 = solo avisar al agotarse).</summary>
    public int StockMinimo { get; private set; }

    /// <summary>Los productos archivados no se ofrecen en visitas nuevas, pero siguen en el historial y en los tickets.</summary>
    public bool Activo { get; private set; } = true;

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    /// <summary>Control de concurrencia: dos cobros a la vez del mismo producto no pueden descontar sobre el mismo stock.</summary>
    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<FotoProducto> Fotos => _fotos;

    public IReadOnlyList<FotoProducto> FotosEnOrden => [.. _fotos.OrderBy(f => f.Orden).ThenBy(f => f.SubidaEn)];

    public FotoProducto? FotoPrincipal => FotosEnOrden.FirstOrDefault();

    public bool Agotado => Stock <= 0;

    /// <summary>Se ha vendido o usado más de lo que había: faltan unidades por reponer o por registrar su entrada.</summary>
    public bool EnNegativo => Stock < 0;

    public bool BajoStock => !Agotado && Stock <= StockMinimo;

    public decimal? Margen => PrecioCoste is { } coste ? PrecioVenta - coste : null;

    public static Producto Crear(FichaProducto ficha, DateTime ahora)
    {
        var producto = new Producto { CreadoEn = ahora, Activo = true };
        producto.Aplicar(ficha, ahora);
        return producto;
    }

    public void Editar(FichaProducto ficha, DateTime ahora) => Aplicar(ficha, ahora);

    public void Archivar(DateTime ahora)
    {
        Activo = false;
        ActualizadoEn = ahora;
    }

    public void Reactivar(DateTime ahora)
    {
        Activo = true;
        ActualizadoEn = ahora;
    }

    /// <summary>Unidades que hay al dar de alta el producto. Sin unidades no deja movimiento.</summary>
    public MovimientoStock? Alta(int unidades, DateTime ahora, int? administradorId)
    {
        if (unidades == 0)
            return null;
        ComprobarUnidades(unidades, "StockInicial");
        return Mover(TipoMovimientoStock.Alta, unidades, "Stock inicial", null, ahora, administradorId);
    }

    /// <summary>Llega mercancía (compra al proveedor, reposición).</summary>
    public MovimientoStock RegistrarEntrada(int unidades, string? nota, DateTime ahora, int? administradorId)
    {
        ComprobarUnidades(unidades, "Cantidad");
        return Mover(TipoMovimientoStock.Entrada, unidades, nota, null, ahora, administradorId);
    }

    /// <summary>Se gasta en el propio salón (una muestra, un producto abierto…). Si no quedaban, el stock queda en negativo.</summary>
    public MovimientoStock RegistrarUsoInterno(int unidades, string? nota, DateTime ahora, int? administradorId)
    {
        ComprobarUnidades(unidades, "Cantidad");
        return Mover(TipoMovimientoStock.UsoInterno, -unidades, nota, null, ahora, administradorId);
    }

    /// <summary>Se ha gastado en el servicio de una visita (tinte, oxidante…): no se cobra, pero sale del stock al cobrarla.</summary>
    public MovimientoStock UsarEnVisita(int unidades, int visitaId, DateTime ahora, int? administradorId)
    {
        ComprobarUnidades(unidades, "Usados");
        return Mover(TipoMovimientoStock.UsoInterno, -unidades, "Usado en el servicio", visitaId, ahora, administradorId);
    }

    /// <summary>Recuento físico: el stock pasa a ser lo contado y el movimiento guarda la diferencia.</summary>
    public MovimientoStock AjustarInventario(int recuento, string? nota, DateTime ahora, int? administradorId)
    {
        if (recuento is < 0 or > StockMaximo)
            throw new ReglaDeNegocioException($"El recuento tiene que estar entre 0 y {StockMaximo} unidades.", "Cantidad");
        if (recuento == Stock)
            throw new ReglaDeNegocioException($"El recuento coincide con el stock actual ({Stock}); no hay nada que ajustar.", "Cantidad");
        return Mover(TipoMovimientoStock.Ajuste, recuento - Stock, nota, null, ahora, administradorId);
    }

    /// <summary>Sale con el cobro de una visita. Si no quedaban, el stock queda en negativo.</summary>
    public MovimientoStock Vender(int unidades, int visitaId, DateTime ahora, int? administradorId)
    {
        ComprobarUnidades(unidades, "Productos");
        return Mover(TipoMovimientoStock.Venta, -unidades, null, visitaId, ahora, administradorId);
    }

    public FotoProducto AgregarFoto(Guid id, DateTime ahora)
    {
        if (_fotos.Count >= MaximoFotos)
            throw new ReglaDeNegocioException($"Un producto admite hasta {MaximoFotos} fotos. Quita alguna antes de subir otra.", "Fotos");
        var foto = new FotoProducto(id, _fotos.Count == 0 ? 0 : _fotos.Max(f => f.Orden) + 1, ahora);
        _fotos.Add(foto);
        ActualizadoEn = ahora;
        return foto;
    }

    /// <summary>Quita la foto de la ficha; el fichero lo borra quien la guardó.</summary>
    public FotoProducto QuitarFoto(Guid id, DateTime ahora)
    {
        var foto = _fotos.FirstOrDefault(f => f.Id == id) ?? throw new ReglaDeNegocioException("Esa foto ya no está en el producto.", "Fotos");
        _fotos.Remove(foto);
        Renumerar(FotosEnOrden);
        ActualizadoEn = ahora;
        return foto;
    }

    /// <summary>La foto pasa a ser la primera, la que se ve en listados, visitas y búsquedas.</summary>
    public void HacerPrincipal(Guid id, DateTime ahora)
    {
        var foto = _fotos.FirstOrDefault(f => f.Id == id) ?? throw new ReglaDeNegocioException("Esa foto ya no está en el producto.", "Fotos");
        Renumerar([foto, .. FotosEnOrden.Where(f => f.Id != id)]);
        ActualizadoEn = ahora;
    }

    private static void Renumerar(IEnumerable<FotoProducto> orden)
    {
        var posicion = 0;
        foreach (var foto in orden)
            foto.Colocar(posicion++);
    }

    private MovimientoStock Mover(TipoMovimientoStock tipo, int cantidad, string? nota, int? visitaId, DateTime ahora, int? administradorId)
    {
        var resultante = Stock + cantidad;
        if (resultante is < -StockMaximo or > StockMaximo)
            throw new ReglaDeNegocioException($"El stock tiene que quedar entre −{StockMaximo} y {StockMaximo} unidades.", "Cantidad");
        var limpia = Texto.Opcional(nota);
        if (limpia?.Length > MovimientoStock.LongitudNota)
            throw new ReglaDeNegocioException($"La nota admite hasta {MovimientoStock.LongitudNota} caracteres.", "Nota");
        Stock = resultante;
        ActualizadoEn = ahora;
        return new MovimientoStock(this, tipo, cantidad, resultante, limpia, visitaId, ahora, administradorId);
    }

    private static void ComprobarUnidades(int unidades, string campo)
    {
        if (unidades is < 1 or > StockMaximo)
            throw new ReglaDeNegocioException($"Indica entre 1 y {StockMaximo} unidades.", campo);
    }

    private void Aplicar(FichaProducto ficha, DateTime ahora)
    {
        var referencia = Texto.Obligatorio(ficha.Referencia, LongitudReferencia, "La referencia", "Referencia").ToUpperInvariant();
        if (referencia.Any(c => !(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or ' ')))
            throw new ReglaDeNegocioException("La referencia solo admite letras, números, espacios y - _ . /", "Referencia");

        var codigo = Texto.Opcional(ficha.CodigoBarras)?.Replace(" ", "");
        if (codigo is not null && (codigo.Length is < 8 or > LongitudCodigoBarras || !codigo.All(char.IsAsciiDigit)))
            throw new ReglaDeNegocioException("El código de barras tiene que tener entre 8 y 14 cifras (EAN, UPC).", "CodigoBarras");

        var nombre = Texto.Obligatorio(ficha.Nombre, LongitudNombre, "El nombre", "Nombre");
        var marca = Opcional(ficha.Marca, LongitudMarca, "La marca", "Marca");
        var categoria = Opcional(ficha.Categoria, LongitudCategoria, "La categoría", "Categoria");
        var descripcion = Opcional(ficha.Descripcion, LongitudDescripcion, "La descripción", "Descripcion");

        var precio = decimal.Round(ficha.PrecioVenta, 2, MidpointRounding.AwayFromZero);
        if (precio is <= 0 or > PrecioMaximo)
            throw new ReglaDeNegocioException("El precio de venta tiene que estar entre 0,01 y 9.999,99 €.", "PrecioVenta");
        var coste = ficha.PrecioCoste is { } c ? decimal.Round(c, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        if (coste is < 0 or > PrecioMaximo)
            throw new ReglaDeNegocioException("El precio de coste tiene que estar entre 0 y 9.999,99 €.", "PrecioCoste");
        if (ficha.StockMinimo is < 0 or > StockMaximo)
            throw new ReglaDeNegocioException($"El stock mínimo tiene que estar entre 0 y {StockMaximo}.", "StockMinimo");

        Referencia = referencia;
        CodigoBarras = codigo;
        Nombre = nombre;
        Marca = marca;
        Categoria = categoria;
        Descripcion = descripcion;
        PrecioVenta = precio;
        PrecioCoste = coste;
        StockMinimo = ficha.StockMinimo;
        ActualizadoEn = ahora;
    }

    private static string? Opcional(string? valor, int longitud, string nombre, string campo)
    {
        var limpio = Texto.Opcional(valor);
        if (limpio?.Length > longitud)
            throw new ReglaDeNegocioException($"{nombre} no puede superar los {longitud} caracteres.", campo);
        return limpio;
    }
}

/// <summary>Una foto del producto. El identificador es también el nombre de sus ficheros (grande y miniatura).</summary>
public sealed class FotoProducto
{
    private FotoProducto()
    {
    }

    internal FotoProducto(Guid id, int orden, DateTime subidaEn)
    {
        Id = id;
        Orden = orden;
        SubidaEn = subidaEn;
    }

    public Guid Id { get; private set; }

    public int ProductoId { get; private set; }

    /// <summary>0 es la principal.</summary>
    public int Orden { get; private set; }

    public DateTime SubidaEn { get; private set; }

    internal void Colocar(int orden) => Orden = orden;
}

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum TipoMovimientoStock
{
    /// <summary>Unidades con las que se dio de alta el producto.</summary>
    Alta = 1,

    /// <summary>Llega mercancía.</summary>
    Entrada = 2,

    /// <summary>Sale con el cobro de una visita.</summary>
    Venta = 3,

    /// <summary>Se gasta en el salón.</summary>
    UsoInterno = 4,

    /// <summary>Corrección tras un recuento.</summary>
    Ajuste = 5,
}

/// <summary>Un cambio del stock de un producto: cuánto, por qué, quién y con qué stock quedó.</summary>
public sealed class MovimientoStock
{
    public const int LongitudNota = 200;

    private MovimientoStock()
    {
    }

    internal MovimientoStock(Producto producto, TipoMovimientoStock tipo, int cantidad, int stockResultante, string? nota, int? visitaId, DateTime fecha, int? administradorId)
    {
        Producto = producto;
        Tipo = tipo;
        Cantidad = cantidad;
        StockResultante = stockResultante;
        Nota = nota;
        VisitaId = visitaId;
        Fecha = fecha;
        AdministradorId = administradorId;
    }

    public long Id { get; private set; }

    public int ProductoId { get; private set; }

    public Producto? Producto { get; private set; }

    public TipoMovimientoStock Tipo { get; private set; }

    /// <summary>Positiva si entran unidades, negativa si salen.</summary>
    public int Cantidad { get; private set; }

    public int StockResultante { get; private set; }

    public string? Nota { get; private set; }

    /// <summary>La visita cobrada, en las ventas.</summary>
    public int? VisitaId { get; private set; }

    /// <summary>Hora local del salón, como las visitas.</summary>
    public DateTime Fecha { get; private set; }

    public int? AdministradorId { get; private set; }
}
