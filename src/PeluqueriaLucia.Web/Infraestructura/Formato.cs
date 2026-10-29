using System.Globalization;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>Cómo se escriben en pantalla importes, fechas y valores de enumeraciones.</summary>
public static class Formato
{
    public static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>«1.234,50 €», con espacio no separable para que el símbolo no quede solo en otra línea.</summary>
    public static string Euros(decimal importe) => importe.ToString("#,##0.00 €", Espanol);

    /// <summary>Importe para un campo de formulario: «12,50», sin símbolo.</summary>
    public static string Importe(decimal? importe) => importe?.ToString("0.00", Espanol) ?? string.Empty;

    /// <summary>Importe con punto decimal para atributos data-* que lee JavaScript.</summary>
    public static string ImporteJs(decimal importe) => importe.ToString("0.00", CultureInfo.InvariantCulture);

    public static string Hora(DateTime fecha) => fecha.ToString("HH:mm", Espanol);

    public static string FechaCorta(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Espanol);

    /// <summary>«viernes, 10 de octubre de 2026» con la primera letra en mayúscula.</summary>
    public static string FechaLarga(DateOnly fecha) => Capitalizar(fecha.ToString("dddd, d 'de' MMMM 'de' yyyy", Espanol));

    public static string Mes(DateOnly fecha) => Capitalizar(fecha.ToString("MMMM 'de' yyyy", Espanol));

    /// <summary>Encabezado de grupo en los listados: «Hoy», «Ayer» o la fecha completa.</summary>
    public static string Dia(DateOnly dia, DateOnly hoy) =>
        dia == hoy ? "Hoy" : dia == hoy.AddDays(-1) ? "Ayer" : FechaLarga(dia);

    public static string Texto(Sexo sexo) => sexo switch
    {
        Sexo.Hombre => "Hombre",
        Sexo.Mujer => "Mujer",
        _ => "Otro",
    };

    public static string Texto(FormaPago forma) => forma switch
    {
        FormaPago.Efectivo => "Efectivo",
        FormaPago.Tarjeta => "Tarjeta",
        _ => "Mixto",
    };

    /// <summary>Cómo sale un producto en una visita, bajo su nombre.</summary>
    public static string Texto(UsoProducto uso) => uso switch
    {
        UsoProducto.ServicioTerminado => "Incluido en el servicio · terminado, sale del stock",
        UsoProducto.ServicioEnUso => "Incluido en el servicio · sigue en uso, no sale del stock",
        _ => "Venta",
    };

    public static string Icono(FormaPago forma) => forma switch
    {
        FormaPago.Efectivo => "efectivo",
        FormaPago.Tarjeta => "tarjeta",
        _ => "mixto",
    };

    /// <summary>Clase de tinte (ver CSS) asociada a cada sexo, para avatares y etiquetas.</summary>
    public static string Tinte(Sexo sexo) => sexo switch
    {
        Sexo.Hombre => "tint-blue",
        Sexo.Mujer => "tint-pink",
        _ => "tint-purple",
    };

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], Espanol) + texto[1..];
}
