using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Gastos;
using PeluqueriaLucia.Domain.Gastos;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class GastoFormulario
{
    [Required(ErrorMessage = "Indica la fecha y la hora.")]
    public DateTime? Fecha { get; set; }

    [Required(ErrorMessage = "Elige el tipo de gasto.")]
    public int? TipoId { get; set; }

    [Required(ErrorMessage = "Escribe el concepto: qué has pagado.")]
    [StringLength(Gasto.LongitudConcepto, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Concepto { get; set; }

    [Required(ErrorMessage = "Escribe el importe.")]
    public decimal? Importe { get; set; }

    public MedioPagoGasto MedioPago { get; set; } = MedioPagoGasto.Efectivo;

    [StringLength(Gasto.LongitudNota, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Nota { get; set; }

    public DatosGasto Datos() => new(Fecha ?? default, TipoId ?? 0, Concepto, Importe ?? 0, MedioPago, Nota);

    public static GastoFormulario Desde(Gasto g) => new()
    {
        Fecha = g.Fecha, TipoId = g.TipoId, Concepto = g.Concepto, Importe = g.Importe, MedioPago = g.MedioPago, Nota = g.Nota,
    };
}

/// <param name="Volver">La pantalla del listado de visitas desde la que se vino, para volver a ella.</param>
/// <param name="Tipos">
/// Los tipos del desplegable, en su orden: los activos y, si el gasto lleva uno archivado, también ese (para no cambiárselo
/// sin querer al guardar).
/// </param>
public sealed record GastoEdicionModelo(int? Id, GastoFormulario Formulario, string? Volver, IReadOnlyList<TipoGasto> Tipos)
{
    public static IReadOnlyList<MedioPagoGasto> Medios { get; } =
        [MedioPagoGasto.Efectivo, MedioPagoGasto.Tarjeta, MedioPagoGasto.Transferencia, MedioPagoGasto.Domiciliacion, MedioPagoGasto.Bizum];

    /// <summary>Los activos y el que ya lleva el gasto, aunque esté archivado.</summary>
    public static IReadOnlyList<TipoGasto> Desplegable(IReadOnlyList<TipoGasto> todos, int? actual) =>
        [.. todos.Where(t => t.Activo || t.Id == actual)];
}

public sealed class TipoGastoFormulario
{
    [Required(ErrorMessage = "Escribe el nombre del tipo.")]
    [StringLength(TipoGasto.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;
}

/// <param name="Editando">El tipo que se está renombrando (su fila pasa a ser un formulario).</param>
/// <param name="Volver">La pantalla del gasto desde la que se vino, para volver a ella.</param>
public sealed record TiposGastoModelo(IReadOnlyList<TipoConUso> Tipos, TipoGastoFormulario Nuevo, int? Editando = null, string? Volver = null)
{
    public IReadOnlyList<TipoConUso> Activos => [.. Tipos.Where(t => t.Tipo.Activo)];

    public IReadOnlyList<TipoConUso> Archivados => [.. Tipos.Where(t => !t.Tipo.Activo)];

    public static string Uso(TipoConUso t) => t.Gastos switch
    {
        0 => "Sin gastos anotados",
        1 => "1 gasto anotado",
        var n => $"{n} gastos anotados",
    };
}

/// <summary>Iconos y textos de los gastos en el panel.</summary>
public static class FormatoGastos
{
    public static string Icono(MedioPagoGasto medio) => medio switch
    {
        MedioPagoGasto.Efectivo => Infraestructura.Formato.Icono(Domain.Visitas.FormaPago.Efectivo),
        MedioPagoGasto.Tarjeta => Infraestructura.Formato.Icono(Domain.Visitas.FormaPago.Tarjeta),
        _ => "euro",
    };
}
