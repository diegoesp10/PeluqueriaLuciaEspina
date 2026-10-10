using System.Globalization;

namespace PeluqueriaLucia.Application.Visitas;

/// <summary>Periodo (días incluidos) y, opcionalmente, cliente por el que filtrar las visitas.</summary>
public sealed record FiltroVisitas(DateOnly? Desde, DateOnly? Hasta, int? ClienteId = null)
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    public static FiltroVisitas Todo { get; } = new(null, null);

    public static FiltroVisitas Dia(DateOnly dia) => new(dia, dia);

    public static FiltroVisitas Mes(int anio, int mes)
    {
        var primero = new DateOnly(anio, mes, 1);
        return new(primero, new DateOnly(anio, mes, DateTime.DaysInMonth(anio, mes)));
    }

    public static FiltroVisitas Anio(int anio) => new(new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));

    public static FiltroVisitas DeCliente(int clienteId) => new(null, null, clienteId);

    /// <summary>Inicio del periodo como instante (incluido), o <c>null</c> si no hay límite.</summary>
    public DateTime? Inicio => Desde?.ToDateTime(TimeOnly.MinValue);

    /// <summary>Final del periodo como instante (excluido): el día siguiente a <see cref="Hasta"/> a las 00:00.</summary>
    public DateTime? FinExcluido => Hasta is { } hasta && hasta < DateOnly.MaxValue ? hasta.AddDays(1).ToDateTime(TimeOnly.MinValue) : null;

    /// <summary>Texto para títulos y nombres de fichero: «10/10/2026», «octubre de 2026», «del … al …» o «todas las fechas».</summary>
    public string Descripcion => (Desde, Hasta) switch
    {
        (null, null) => "todas las fechas",
        ({ } d, { } h) when d == h => d.ToString("dd/MM/yyyy", Espanol),
        ({ } d, { } h) when d.Month == 1 && d.Day == 1 && h == new DateOnly(d.Year, 12, 31) => $"año {d.Year}",
        ({ } d, { } h) when d.Day == 1 && h == new DateOnly(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)) => d.ToString("MMMM 'de' yyyy", Espanol),
        ({ } d, { } h) => $"del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}",
        ({ } d, null) => $"desde el {d:dd/MM/yyyy}",
        (null, { } h) => $"hasta el {h:dd/MM/yyyy}",
    };
}

/// <summary>Cifras de caja de un periodo.</summary>
public sealed record ResumenVisitas(
    int Visitas,
    int Servicios,
    decimal Facturado,
    decimal Cobrado,
    decimal Efectivo,
    decimal Tarjeta)
{
    public static ResumenVisitas Vacio { get; } = new(0, 0, 0, 0, 0, 0);

    public decimal Pendiente => Facturado - Cobrado;
}

public sealed record NuevaVisita(int ClienteId, DateTime Fecha, Domain.Visitas.FormaPago FormaPago, IReadOnlyCollection<int> ServicioIds, IReadOnlyDictionary<int,decimal>? Precios = null);
