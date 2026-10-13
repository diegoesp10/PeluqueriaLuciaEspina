using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
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

public sealed record VisitasIndiceModelo(Periodo Periodo, DateOnly Fecha, DateOnly Hoy, Pagina<Visita> Pagina, ResumenVisitas Resumen)
{
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

    /// <summary>«cobrar» lleva directamente a la pantalla de cobro después de guardar.</summary>
    public string? Accion { get; set; }
}

public sealed record NuevaVisitaModelo(NuevaVisitaFormulario Formulario, Cliente? Cliente, IReadOnlyList<Servicio> Servicios);

public sealed class CobroFormulario
{
    public FormaPago FormaPago { get; set; } = FormaPago.Efectivo;

    public decimal? Efectivo { get; set; }

    public decimal? Tarjeta { get; set; }
}

public sealed record VisitaDetalleModelo(Visita Visita, CobroFormulario Cobro);

public sealed record TicketModelo(Visita Visita, DatosNegocio Negocio, string? CodigoOpinion = null, Infraestructura.Impresion.ImpresoraTickets? Impresora = null)
{
    /// <summary>Hay impresora térmica configurada: el botón Imprimir manda el ticket por USB desde el navegador.</summary>
    public bool ConImpresoraTermica => Impresora?.Configurada == true;

    public decimal TipoImpuesto => Visita.PorcentajeIva ?? Negocio.TipoImpuesto;

    /// <summary>Base imponible y cuota a partir del total con impuestos incluidos.</summary>
    public decimal Base => decimal.Round(Visita.Total / (1 + TipoImpuesto / 100m), 2, MidpointRounding.AwayFromZero);

    public decimal Cuota => Visita.Total - Base;
}
