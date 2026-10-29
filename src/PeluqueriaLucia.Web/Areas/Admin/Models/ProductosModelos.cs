using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class ProductoFormulario
{
    [Required(ErrorMessage = "Escribe la referencia del producto.")]
    [StringLength(Producto.LongitudReferencia, ErrorMessage = "Máximo {1} caracteres.")]
    public string Referencia { get; set; } = string.Empty;

    /// <summary>Se admiten espacios al escribirlo («8412 3456 78901»); el dominio los quita.</summary>
    [StringLength(20, ErrorMessage = "El código de barras tiene entre 8 y 14 cifras.")]
    public string? CodigoBarras { get; set; }

    [Required(ErrorMessage = "Escribe el nombre del producto.")]
    [StringLength(Producto.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [StringLength(Producto.LongitudMarca, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Marca { get; set; }

    /// <summary>Categoría existente. Obligatoria salvo que se escriba una nueva en <see cref="CategoriaNueva"/>.</summary>
    public int? CategoriaId { get; set; }

    /// <summary>Categoría escrita en el buscador que no estaba en la lista: se crea al guardar (o se usa la que ya se llame así).</summary>
    [StringLength(CategoriaProducto.LongitudNombre, ErrorMessage = "El nombre de la categoría no puede superar los {1} caracteres.")]
    public string? CategoriaNueva { get; set; }

    [StringLength(Producto.LongitudDescripcion, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Escribe el precio de venta.")]
    [Range(typeof(decimal), "0.01", "9999.99", ErrorMessage = "El precio de venta tiene que estar entre 0,01 y 9.999,99 €.",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PrecioVenta { get; set; }

    [Range(typeof(decimal), "0", "9999.99", ErrorMessage = "El precio de coste tiene que estar entre 0 y 9.999,99 €.",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? PrecioCoste { get; set; }

    [Range(0, Producto.StockMaximo, ErrorMessage = "El stock mínimo tiene que estar entre {1} y {2}.")]
    public int StockMinimo { get; set; } = 1;

    /// <summary>Solo al dar de alta. Después el stock cambia con entradas, ventas, usos y recuentos.</summary>
    [Range(0, Producto.StockMaximo, ErrorMessage = "El stock inicial tiene que estar entre {1} y {2} unidades.")]
    public int StockInicial { get; set; }

    public List<IFormFile> Fotos { get; set; } = [];

    /// <summary>Fotos guardadas que se quitan al editar.</summary>
    public List<Guid> QuitarFotos { get; set; } = [];

    /// <summary>Foto guardada que pasa a ser la principal.</summary>
    public Guid? FotoPrincipal { get; set; }

    public FichaProducto AFicha() =>
        new(Referencia, CodigoBarras, Nombre, Marca, Descripcion, PrecioVenta ?? 0, PrecioCoste, StockMinimo);

    public CategoriaElegida ACategoria() => new(CategoriaId, CategoriaNueva);

    public IReadOnlyList<ArchivoImagen> AArchivos() =>
        [.. Fotos.Where(f => f.Length > 0).Select(f => new ArchivoImagen(f.OpenReadStream(), f.FileName, f.Length))];

    public CambiosFotos ACambios() => new(AArchivos(), QuitarFotos, FotoPrincipal);

    public static ProductoFormulario Desde(Producto p) => new()
    {
        Referencia = p.Referencia,
        CodigoBarras = p.CodigoBarras,
        Nombre = p.Nombre,
        Marca = p.Marca,
        CategoriaId = p.CategoriaId,
        Descripcion = p.Descripcion,
        PrecioVenta = p.PrecioVenta,
        PrecioCoste = p.PrecioCoste,
        StockMinimo = p.StockMinimo,
        FotoPrincipal = p.FotoPrincipal?.Id,
    };
}

/// <param name="Producto">El producto guardado al editar (fotos actuales, stock); null al crear.</param>
/// <param name="Categorias">Las que se pueden elegir y, si el producto tiene una eliminada, también esa.</param>
public sealed record ProductoEdicionModelo(int? Id, ProductoFormulario Formulario, Producto? Producto, IReadOnlyList<CategoriaProducto> Categorias, IReadOnlyList<string> Marcas)
{
    public bool Nuevo => Id is null;
}

/// <param name="Categorias">Todas (también las eliminadas, para ver sus productos archivados) con sus cifras.</param>
public sealed record ProductosIndiceModelo(FiltroProductos Filtro, Pagina<Producto> Pagina, ResumenInventario Resumen, IReadOnlyList<ResumenCategoria> Categorias)
{
    public bool Filtrando => !string.IsNullOrWhiteSpace(Filtro.Texto) || Filtro.CategoriaId is not null || Filtro.Estado != EstadoInventario.Activos;

    public CategoriaProducto? CategoriaFiltrada => Categorias.FirstOrDefault(c => c.Categoria.Id == Filtro.CategoriaId)?.Categoria;

    public string TextoVacio => Filtro.Estado switch
    {
        _ when !string.IsNullOrWhiteSpace(Filtro.Texto) => "Ningún producto coincide con la búsqueda",
        _ when CategoriaFiltrada is { Activa: false } c && Filtro.Estado == EstadoInventario.Activos => $"«{c.Nombre}» está eliminada",
        _ when CategoriaFiltrada is { } c && Filtro.Estado == EstadoInventario.Activos => $"Nada a la venta en «{c.Nombre}»",
        EstadoInventario.PorReponer => "Nada por reponer",
        EstadoInventario.Archivados => "No hay productos archivados",
        _ when Resumen.Productos == 0 && Filtro.Estado == EstadoInventario.Activos => "Todavía no hay productos",
        _ => "Ningún producto con estos filtros",
    };
}

public sealed class MovimientoFormulario
{
    public MovimientoManual Tipo { get; set; } = MovimientoManual.Entrada;

    [Required(ErrorMessage = "Indica la cantidad.")]
    [Range(0, Producto.StockMaximo, ErrorMessage = "La cantidad tiene que estar entre {1} y {2}.")]
    public int? Cantidad { get; set; }

    [StringLength(MovimientoStock.LongitudNota, ErrorMessage = "La nota admite hasta {1} caracteres.")]
    public string? Nota { get; set; }

    /// <summary>En una entrada, lo que ha costado cada unidad (opcional): queda en el historial de costes.</summary>
    public decimal? Coste { get; set; }
}

/// <summary>Anotar en la ficha lo que ha costado el producto desde una fecha.</summary>
public sealed class CosteFormulario
{
    public decimal? Coste { get; set; }

    /// <summary>Desde cuándo; sin fecha, hoy.</summary>
    public DateOnly? Desde { get; set; }

    [StringLength(CosteProducto.LongitudNota, ErrorMessage = "La nota admite hasta {1} caracteres.")]
    public string? NotaCoste { get; set; }
}

/// <param name="Costes">Historial de costes de compra, lo más reciente primero.</param>
public sealed record ProductoFichaModelo(Producto Producto, Pagina<MovimientoStock> Movimientos, MovimientoFormulario Movimiento,
    IReadOnlyList<CosteProducto>? Costes = null, CosteFormulario? Coste = null)
{
    public IReadOnlyList<CosteProducto> HistorialCostes => Costes ?? [];

    public CosteFormulario FormularioCoste => Coste ?? new CosteFormulario();

    /// <summary>Lo que cambió el coste respecto al anotado antes (el siguiente de la lista); null en el primero.</summary>
    public (decimal Diferencia, decimal? Porcentaje)? Variacion(int indice)
    {
        if (indice + 1 >= HistorialCostes.Count)
            return null;
        var anterior = HistorialCostes[indice + 1].Coste;
        var diferencia = HistorialCostes[indice].Coste - anterior;
        return (diferencia, anterior == 0 ? null : diferencia / anterior);
    }
}

public sealed class CategoriaProductoFormulario
{
    [Required(ErrorMessage = "Escribe el nombre de la categoría.")]
    [StringLength(CategoriaProducto.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;
}

/// <param name="Editando">Categoría que se está renombrando (su fila pasa a ser un formulario).</param>
public sealed record CategoriasModelo(IReadOnlyList<ResumenCategoria> Categorias, CategoriaProductoFormulario Nueva, int? Editando = null)
{
    public IReadOnlyList<ResumenCategoria> Activas => [.. Categorias.Where(c => c.Categoria.Activa)];

    public IReadOnlyList<ResumenCategoria> Eliminadas => [.. Categorias.Where(c => !c.Categoria.Activa)];

    /// <summary>El aviso antes de eliminar: qué productos y cuántas unidades se archivan, y que se puede deshacer.</summary>
    public static string AvisoEliminar(ResumenCategoria c) => c.Productos == 0
        ? $"Se eliminará «{c.Categoria.Nombre}». No tiene productos a la venta. Podrás recuperarla desde esta pantalla."
        : $"Se eliminará «{c.Categoria.Nombre}» y {(c.Productos == 1 ? "su producto a la venta se archivará" : $"sus {c.Productos} productos a la venta se archivarán")} " +
          $"con su stock ({FormatoProductos.Unidades(c.Unidades)}): dejarán de ofrecerse en las visitas y de contar en el inventario. " +
          "Si recuperas la categoría, volverán a la venta con las mismas unidades.";
}

/// <summary>Textos, iconos y colores de cada tipo de movimiento y del estado del stock, iguales en todas las pantallas.</summary>
public static class FormatoProductos
{
    public static string Texto(TipoMovimientoStock tipo) => tipo switch
    {
        TipoMovimientoStock.Alta => "Alta",
        TipoMovimientoStock.Entrada => "Entrada",
        TipoMovimientoStock.Venta => "Venta",
        TipoMovimientoStock.UsoInterno => "Uso en el salón",
        TipoMovimientoStock.Ajuste => "Recuento",
        _ => tipo.ToString(),
    };

    public static string Icono(TipoMovimientoStock tipo) => tipo switch
    {
        TipoMovimientoStock.Alta => "producto",
        TipoMovimientoStock.Entrada => "mas",
        TipoMovimientoStock.Venta => "ticket",
        TipoMovimientoStock.UsoInterno => "tijeras",
        _ => "ajustes",
    };

    public static string Tinte(TipoMovimientoStock tipo) => tipo switch
    {
        TipoMovimientoStock.Entrada or TipoMovimientoStock.Alta => "tint-green",
        TipoMovimientoStock.Venta => "tint-blue",
        TipoMovimientoStock.UsoInterno => "tint-orange",
        _ => "tint-purple",
    };

    /// <summary>«12 uds», «Quedan 2», «Agotado», «−1 ud» (en negativo: se vendió o usó más de lo que había).</summary>
    public static string Stock(Producto p) => p.EnNegativo ? Unidades(p.Stock) : p.Agotado ? "Agotado" : p.BajoStock ? $"Quedan {p.Stock}" : Unidades(p.Stock);

    /// <summary>Estado del stock en frase: «Disponible», «Por debajo del mínimo», «Agotado», «Faltan 2 por reponer».</summary>
    public static string Estado(Producto p) => p.EnNegativo ? $"Faltan {-p.Stock} por reponer" : p.Agotado ? "Agotado" : p.BajoStock ? "Por debajo del mínimo" : "Disponible";

    /// <summary>«5 en stock», «agotado», «en negativo (−2)».</summary>
    public static string Disponible(int stock) => stock > 0 ? $"{stock} en stock" : stock == 0 ? "agotado" : $"en negativo ({Cantidad(stock)})";

    public static string TinteStock(Producto p) => p.Agotado ? "tint-red" : p.BajoStock ? "tint-orange" : "tint-green";

    public static string Unidades(int unidades) =>
        $"{(unidades < 0 ? "−" : "")}{Math.Abs(unidades).ToString("#,##0", Formato.Espanol)} {(Math.Abs(unidades) == 1 ? "ud" : "uds")}";

    /// <summary>«+10», «−3»: con signo menos tipográfico, como en los extractos.</summary>
    public static string Cantidad(int cantidad) => cantidad > 0 ? $"+{cantidad}" : $"−{-cantidad}";

    public static string Foto(Guid id, bool miniatura = true) => $"/admin/productos/foto/{id:N}{(miniatura ? "?mini=true" : "")}";

    /// <summary>Texto de la opción en los buscadores de producto: «Nombre · REF · 14,90 € · 5 en stock».</summary>
    public static string Opcion(Producto p) =>
        $"{p.Nombre} · {p.Referencia} · {Formato.Euros(p.PrecioVenta)} · {Disponible(p.Stock)}";

    /// <summary>Palabras extra para encontrar el producto en el buscador (código de barras, marca), sin mostrarlas.</summary>
    public static string ClavesBusqueda(Producto p) => string.Join(' ', new[] { p.CodigoBarras, p.Marca, p.Categoria?.Nombre }.Where(x => x is not null));

    /// <summary>«Eco Natura · Champús»: marca y categoría, lo que haya.</summary>
    public static string Detalle(Producto p) => string.Join(" · ", new[] { p.Marca, p.Categoria?.Nombre }.Where(x => x is not null));

    /// <summary>«5 productos · 23 uds», «Sin productos a la venta».</summary>
    public static string Cifras(ResumenCategoria c) => c.Productos == 0
        ? "Sin productos a la venta"
        : $"{c.Productos} {(c.Productos == 1 ? "producto" : "productos")} · {Unidades(c.Unidades)}";
}
