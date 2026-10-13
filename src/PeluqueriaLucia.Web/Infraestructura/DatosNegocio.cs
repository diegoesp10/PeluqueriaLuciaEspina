namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>Datos fiscales y de contacto que salen en la cabecera del ticket (sección «Negocio» de appsettings).</summary>
public sealed class DatosNegocio
{
    public const string Seccion = "Negocio";

    public string Nombre { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string Localidad { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string Nif { get; set; } = string.Empty;

    public string Empleado { get; set; } = string.Empty;

    /// <summary>Tipo de IVA aplicado, en porcentaje. 0 si el negocio está exento.</summary>
    public decimal TipoImpuesto { get; set; }

    public string PieTicket { get; set; } = "Gracias por su visita";
}
