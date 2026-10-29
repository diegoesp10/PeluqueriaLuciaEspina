using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public enum Periodo
{
    Dia,
    Mes,
    Todo,
    Anio,
}

/// <param name="PorCobrar">En la vista de un día, las citas confirmadas de la agenda que aún no tienen visita.</param>
public sealed record VisitasIndiceModelo(Periodo Periodo, DateOnly Fecha, DateOnly Hoy, Pagina<Visita> Pagina, ResumenVisitas Resumen,
    IReadOnlyList<ReservaCita>? PorCobrar = null)
{
    public IReadOnlyList<ReservaCita> CitasPorCobrar => PorCobrar ?? [];

    public static FiltroVisitas Filtro(Periodo periodo, DateOnly fecha) => periodo switch
    {
        Periodo.Dia => FiltroVisitas.Dia(fecha),
        Periodo.Mes => FiltroVisitas.Mes(fecha.Year, fecha.Month),
        Periodo.Anio => FiltroVisitas.Anio(fecha.Year),
        _ => FiltroVisitas.Todo,
    };

    public string Titulo => Periodo switch
    {
        Periodo.Dia => Formato.Dia(Fecha, Hoy),
        Periodo.Mes => Formato.Mes(Fecha),
        Periodo.Anio => $"Año {Fecha.Year}",
        _ => "Todas las visitas",
    };

    /// <summary>Línea bajo el título: la fecha completa cuando el título es «Hoy» o «Ayer».</summary>
    public string? Subtitulo => Periodo == Periodo.Dia && Titulo != Formato.FechaLarga(Fecha) ? Formato.FechaLarga(Fecha) : null;

    public DateOnly? Anterior => Periodo switch
    {
        Periodo.Dia when Fecha > DateOnly.MinValue => Fecha.AddDays(-1),
        Periodo.Mes when Fecha.Year > 1 || Fecha.Month > 1 => new DateOnly(Fecha.Year, Fecha.Month, 1).AddMonths(-1),
        Periodo.Anio when Fecha.Year > 1 => new DateOnly(Fecha.Year - 1, 1, 1),
        _ => null,
    };

    public DateOnly? Siguiente => Periodo switch
    {
        Periodo.Dia when Fecha < DateOnly.MaxValue => Fecha.AddDays(1),
        Periodo.Mes when Fecha.Year < 9999 || Fecha.Month < 12 => new DateOnly(Fecha.Year, Fecha.Month, 1).AddMonths(1),
        Periodo.Anio when Fecha.Year < 9999 => new DateOnly(Fecha.Year + 1, 1, 1),
        _ => null,
    };

    public bool MuestraHoy => Periodo switch
    {
        Periodo.Dia => Fecha == Hoy,
        Periodo.Mes => Fecha.Year == Hoy.Year && Fecha.Month == Hoy.Month,
        Periodo.Anio => Fecha.Year == Hoy.Year,
        _ => true,
    };

    /// <summary>En vistas de varios días, las visitas se agrupan por día con un encabezado.</summary>
    public bool AgruparPorDia => Periodo != Periodo.Dia;

    public string TextoVacio => Periodo switch
    {
        Periodo.Dia when Fecha == Hoy => "Todavía no hay visitas hoy",
        Periodo.Dia => "No hubo visitas este día",
        Periodo.Mes => "No hay visitas este mes",
        Periodo.Anio => "No hay visitas este año",
        _ => "Aún no hay visitas registradas",
    };
}

public sealed class NuevaVisitaFormulario
{
    [Required(ErrorMessage = "Elige un cliente.")]
    public int? ClienteId { get; set; }

    [Required(ErrorMessage = "Indica la fecha y la hora.")]
    public DateTime? Fecha { get; set; }

    public FormaPago FormaPago { get; set; } = FormaPago.Efectivo;

    public List<int> Servicios { get; set; } = [];
    public Dictionary<int,decimal> Precios { get; set; } = [];

    /// <summary>Productos que se lleva el cliente (nueva-visita.js añade una fila por producto).</summary>
    public List<ProductoVisitaFormulario> Productos { get; set; } = [];

    /// <summary>«cobrar» lleva directamente a la pantalla de cobro después de guardar.</summary>
    public string? Accion { get; set; }
}

/// <summary>Una fila de producto en «Nueva visita»: vendido (sin precio, el de venta de la ficha) o usado en el servicio.</summary>
public sealed class ProductoVisitaFormulario
{
    public int ProductoId { get; set; }

    [Range(1, Domain.Visitas.LineaProductoVisita.CantidadMaxima, ErrorMessage = "Indica entre {1} y {2} unidades de cada producto.")]
    public int Cantidad { get; set; } = 1;

    public decimal? Precio { get; set; }

    public UsoProducto Uso { get; set; }
}

public sealed record NuevaVisitaModelo(NuevaVisitaFormulario Formulario, Cliente? Cliente, IReadOnlyList<Servicio> Servicios, IReadOnlyList<Producto> Productos);

/// <summary>
/// La pantalla de una visita pendiente es un único formulario: los precios de cada línea, lo que se va a añadir y la forma de
/// pago. Cualquier botón (añadir, quitar, guardar o cobrar) guarda antes los precios escritos, así no se pierden. Cada acción
/// comprueba solo sus campos: lo escrito para añadir un producto no impide cobrar.
/// </summary>
public sealed class VisitaFormulario
{
    /// <summary>Precio de cada servicio de la visita, por número de línea.</summary>
    public Dictionary<int, decimal?> PreciosServicios { get; set; } = [];

    /// <summary>Precio por unidad de cada producto vendido, por número de línea.</summary>
    public Dictionary<int, decimal?> PreciosProductos { get; set; } = [];

    public FormaPago FormaPago { get; set; } = FormaPago.Efectivo;

    public decimal? Efectivo { get; set; }

    public decimal? Tarjeta { get; set; }

    /// <summary>Servicio del catálogo que se añade; sin precio, el del catálogo.</summary>
    public int? ServicioId { get; set; }

    public decimal? PrecioServicio { get; set; }

    /// <summary>Producto que se añade: vendido (sin precio, el de la ficha) o usado en el servicio.</summary>
    public int? ProductoId { get; set; }

    public int Cantidad { get; set; } = 1;

    public decimal? Precio { get; set; }

    /// <summary>Usado en el servicio en lugar de vendido: va incluido, a 0,00 €.</summary>
    public bool EnServicio { get; set; }

    /// <summary>Si se ha usado en el servicio, si se ha terminado (sale del stock) o sigue en uso. Hay que contestarlo.</summary>
    public bool? Terminado { get; set; }

    /// <summary>Cómo sale el producto que se añade; null si es de uso en el servicio y falta decir si se ha terminado.</summary>
    public UsoProducto? Uso => !EnServicio ? UsoProducto.Venta
        : Terminado switch { true => UsoProducto.ServicioTerminado, false => UsoProducto.ServicioEnUso, null => null };
}

/// <param name="ParaVenta">Productos activos, para añadirlos mientras la visita está pendiente.</param>
/// <param name="Servicios">Servicios del catálogo, para añadirlos mientras la visita está pendiente.</param>
/// <param name="Cita">La cita de la agenda que se cobra con esta visita, si la hay.</param>
/// <param name="Usados">Movimientos de uso de una visita ya cobrada (ver <see cref="UsadosAntiguos"/>).</param>
/// <param name="Abierto">Qué se estaba añadiendo cuando hubo un error («servicio» o «producto»), para mostrarlo abierto.</param>
public sealed record VisitaDetalleModelo(Visita Visita, VisitaFormulario Formulario, IReadOnlyList<Producto> ParaVenta, IReadOnlyList<Servicio> Servicios,
    ReservaCita? Cita = null, IReadOnlyList<MovimientoStock>? Usados = null, string? Abierto = null)
{
    /// <summary>Unidades en stock de un producto, o null si ya no está a la venta.</summary>
    public int? Stock(int productoId) => ParaVenta.FirstOrDefault(p => p.Id == productoId)?.Stock;

    /// <summary>Lo que quedará en stock de un producto después de cobrar esta visita (puede ser negativo).</summary>
    public int Disponibles(int productoId) =>
        (Stock(productoId) ?? 0) - Visita.Productos.Where(l => l.ProductoId == productoId && l.DescuentaStock).Sum(l => l.Cantidad);

    /// <summary>Productos de la visita cuyo stock quedará en negativo al cobrar: nombre y cuántas faltarán.</summary>
    public IEnumerable<(string Nombre, int Faltan)> QuedaranEnNegativo() => Visita.Productos
        .Where(l => l.DescuentaStock)
        .GroupBy(l => l.ProductoId)
        .Select(g => (Nombre: g.First().Nombre, Faltan: -Disponibles(g.Key)))
        .Where(x => x.Faltan > 0);

    /// <summary>Unidades que salen del stock al cobrar: lo vendido y lo usado que se ha terminado.</summary>
    public int UnidadesQueSalen => Visita.Productos.Where(l => l.DescuentaStock).Sum(l => l.Cantidad);

    /// <summary>
    /// En las visitas cobradas antes de poner lo usado en la visita, los productos usados solo están en los movimientos de
    /// stock. En las demás ya salen en la lista, incluidos en el servicio.
    /// </summary>
    public IReadOnlyList<MovimientoStock> UsadosAntiguos => Visita.Productos.Any(p => p.Incluido) ? [] : Usados ?? [];

    /// <summary>El precio escrito en un campo: lo que llegó en el formulario (aunque no valga, para corregirlo) o el guardado.</summary>
    public static string Valor(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary estado, string campo, decimal guardado) =>
        estado.TryGetValue(campo, out var entrada) && entrada.AttemptedValue is { } escrito ? escrito : Formato.Importe(guardado);

    /// <summary>El campo llegó con un valor que no vale (se marca en rojo).</summary>
    public static bool NoVale(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary estado, string campo) =>
        estado.TryGetValue(campo, out var entrada) && entrada.Errors.Count > 0;
}

public sealed record TicketModelo(Visita Visita, DatosNegocio Negocio, string? CodigoOpinion = null, Infraestructura.Impresion.ImpresoraTickets? Impresora = null)
{
    /// <summary>Hay impresora térmica configurada: el botón Imprimir manda el ticket por USB desde el navegador.</summary>
    public bool ConImpresoraTermica => Impresora?.Configurada == true;

    public decimal TipoImpuesto => Visita.PorcentajeIva ?? Negocio.TipoImpuesto;

    /// <summary>Base imponible y cuota a partir del total con impuestos incluidos.</summary>
    public decimal Base => decimal.Round(Visita.Total / (1 + TipoImpuesto / 100m), 2, MidpointRounding.AwayFromZero);

    public decimal Cuota => Visita.Total - Base;
}
