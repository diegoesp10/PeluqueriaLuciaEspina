using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Productos;

/// <summary>
/// Datos de la ficha de un producto que escribe el salón. Ni el stock (solo cambia con movimientos) ni la categoría
/// (es una entidad, <see cref="CategoriaProducto"/>) están aquí.
/// </summary>
public sealed record FichaProducto(
    string? Referencia,
    string? CodigoBarras,
    string? Nombre,
    string? Marca,
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

    /// <summary>Categoría obligatoria. Si se elimina, el producto se archiva con ella (ver <see cref="ArchivadoConCategoria"/>).</summary>
    public int CategoriaId { get; private set; }

    public CategoriaProducto? Categoria { get; private set; }

    public string? Descripcion { get; private set; }

    /// <summary>Precio de venta al público, con impuestos incluidos, como los servicios.</summary>
    public decimal PrecioVenta { get; private set; }

    /// <summary>
    /// Lo que cuesta al salón ahora; sirve para valorar el inventario y ver el margen. Cada coste que se anota queda en el
    /// historial (<see cref="CosteProducto"/>), y este es el del más reciente.
    /// </summary>
    public decimal? PrecioCoste { get; private set; }

    public int Stock { get; private set; }

    /// <summary>Por debajo o igual a esta cantidad se avisa de que hay que reponer (0 = solo avisar al agotarse).</summary>
    public int StockMinimo { get; private set; }

    /// <summary>Los productos archivados no se ofrecen en visitas nuevas, pero siguen en el historial y en los tickets.</summary>
    public bool Activo { get; private set; } = true;

    /// <summary>Se archivó al eliminar su categoría (con su stock): al recuperar la categoría vuelve a estar a la venta.</summary>
    public bool ArchivadoConCategoria { get; private set; }

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

    public static Producto Crear(FichaProducto ficha, CategoriaProducto categoria, DateTime ahora)
    {
        var producto = new Producto { CreadoEn = ahora, Activo = true };
        producto.Aplicar(ficha, ahora);
        producto.Clasificar(categoria);
        return producto;
    }

    /// <summary>Comprueba una ficha sin crear el producto. Devuelve la referencia y el código de barras normalizados (para buscar duplicados).</summary>
    public static (string Referencia, string? CodigoBarras) Comprobar(FichaProducto ficha)
    {
        var prueba = new Producto();
        prueba.Aplicar(ficha, default);
        return (prueba.Referencia, prueba.CodigoBarras);
    }

    /// <summary>Cambia la ficha y la categoría. Se puede mantener una categoría eliminada (producto archivado con ella), pero no pasar a una.</summary>
    public void Editar(FichaProducto ficha, CategoriaProducto categoria, DateTime ahora)
    {
        Aplicar(ficha, ahora);
        if (categoria.Id != CategoriaId || CategoriaId == 0)
            Clasificar(categoria);
    }

    public void Archivar(DateTime ahora)
    {
        Activo = false;
        ArchivadoConCategoria = false;
        ActualizadoEn = ahora;
    }

    /// <summary>Lo archiva la eliminación de su categoría; recuperarla lo devuelve a la venta.</summary>
    internal void ArchivarPorCategoria(DateTime ahora)
    {
        Activo = false;
        ArchivadoConCategoria = true;
        ActualizadoEn = ahora;
    }

    public void Reactivar(DateTime ahora)
    {
        if (Categoria is { Activa: false } eliminada)
            throw new ReglaDeNegocioException($"Su categoría «{eliminada.Nombre}» está eliminada. Recupérala o elige otra en la ficha del producto.", nameof(CategoriaId));
        Activo = true;
        ArchivadoConCategoria = false;
        ActualizadoEn = ahora;
    }

    private void Clasificar(CategoriaProducto categoria)
    {
        ArgumentNullException.ThrowIfNull(categoria);
        if (!categoria.Activa)
            throw new ReglaDeNegocioException($"La categoría «{categoria.Nombre}» está eliminada. Elige otra o recupérala.", nameof(CategoriaId));
        // Si sale de la categoría con la que se archivó, recuperar aquella ya no lo devuelve a la venta
        ArchivadoConCategoria = false;
        Categoria = categoria;
        CategoriaId = categoria.Id;
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

    /// <summary>
    /// Anota lo que ha costado comprar el producto a partir de <paramref name="desde"/> («en enero, 100 €»). Si es el registro
    /// más reciente del historial (no hay ninguno o <paramref name="ultimoDesde"/> no es posterior), pasa a ser su precio de
    /// coste actual; uno de un mes pasado solo completa el historial.
    /// </summary>
    public CosteProducto RegistrarCoste(decimal coste, DateOnly desde, string? nota, DateOnly? ultimoDesde, DateTime ahora, int? administradorId)
    {
        var importe = decimal.Round(coste, 2, MidpointRounding.AwayFromZero);
        if (importe is < 0 or > PrecioMaximo)
            throw new ReglaDeNegocioException("El coste tiene que estar entre 0 y 9.999,99 €.", "Coste");
        if (desde > DateOnly.FromDateTime(ahora))
            throw new ReglaDeNegocioException("La fecha del coste no puede ser futura.", "Desde");
        if (desde < CosteProducto.Primera)
            throw new ReglaDeNegocioException("Revisa la fecha del coste.", "Desde");
        var limpia = Texto.Opcional(nota);
        if (limpia?.Length > CosteProducto.LongitudNota)
            throw new ReglaDeNegocioException($"La nota admite hasta {CosteProducto.LongitudNota} caracteres.", "NotaCoste");

        if (ultimoDesde is null || desde >= ultimoDesde)
        {
            PrecioCoste = importe;
            ActualizadoEn = ahora;
        }

        return new CosteProducto(this, importe, desde, limpia, ahora, administradorId);
    }

    /// <summary>
    /// El registro del historial de costes cuando la ficha cambia el precio de coste (al dar de alta o al editarla): desde
    /// hoy, el nuevo. Null si no ha cambiado o se ha quitado.
    /// </summary>
    public CosteProducto? CosteDeLaFicha(decimal? anterior, string? nota, DateTime ahora, int? administradorId) =>
        PrecioCoste is { } coste && coste != anterior ? new CosteProducto(this, coste, DateOnly.FromDateTime(ahora), nota, ahora, administradorId) : null;

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

/// <summary>
/// Lo que costó comprar un producto a partir de una fecha: el historial de precios de compra («en enero, 100 €; en
/// febrero, 120 €»). Se anota al dar de alta el producto, al cambiar su coste en la ficha, al registrar una entrada con su
/// coste o a mano en la ficha.
/// </summary>
public sealed class CosteProducto
{
    public const int LongitudNota = 200;

    /// <summary>Fecha más antigua que se admite.</summary>
    public static readonly DateOnly Primera = new(2000, 1, 1);

    private CosteProducto()
    {
    }

    internal CosteProducto(Producto producto, decimal coste, DateOnly desde, string? nota, DateTime registradoEn, int? administradorId)
    {
        Producto = producto;
        Coste = coste;
        Desde = desde;
        Nota = nota;
        RegistradoEn = registradoEn;
        AdministradorId = administradorId;
    }

    public long Id { get; private set; }

    public int ProductoId { get; private set; }

    public Producto? Producto { get; private set; }

    /// <summary>Precio de compra de cada unidad.</summary>
    public decimal Coste { get; private set; }

    /// <summary>Desde cuándo costaba eso.</summary>
    public DateOnly Desde { get; private set; }

    /// <summary>Proveedor, pedido…</summary>
    public string? Nota { get; private set; }

    /// <summary>Cuándo se anotó (hora local del salón).</summary>
    public DateTime RegistradoEn { get; private set; }

    public int? AdministradorId { get; private set; }
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
