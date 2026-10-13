namespace PeluqueriaLucia.Web.Infraestructura.Impresion;

/// <summary>
/// Impresora térmica de tickets del salón (sección «ImpresoraTickets» de appsettings). La app construye el ticket en
/// ESC/POS y el navegador del panel lo envía por USB a la impresora con WebUSB, así funciona igual con la web
/// publicada en Azure. El driver de la impresora está sustituido con Zadig (WinUSB), que es lo que WebUSB necesita.
/// </summary>
public sealed class ImpresoraTickets
{
    public const string Seccion = "ImpresoraTickets";

    /// <summary>Identificador USB del fabricante, en hexadecimal («0x0FE6»). Vacío: sin impresora térmica.</summary>
    public string Vid { get; set; } = string.Empty;

    /// <summary>Identificador USB del producto, en hexadecimal («0x811E»).</summary>
    public string Pid { get; set; } = string.Empty;

    /// <summary>Caracteres por línea del papel (32 en papel de 58 mm, 48 en 80 mm).</summary>
    public int AnchoCaracteres { get; set; } = 32;

    /// <summary>Ancho imprimible en puntos, para el logo (384 en papel de 58 mm, 576 en 80 mm).</summary>
    public int AnchoLogoPuntos { get; set; } = 384;

    /// <summary>Logo en blanco y negro que encabeza el ticket, relativo a wwwroot. Vacío: sin logo.</summary>
    public string Logo { get; set; } = "Img/Icono_bn.png";

    public bool Configurada => IdentificadorUsb(Vid) is not null && IdentificadorUsb(Pid) is not null;

    public int VidNumero => IdentificadorUsb(Vid) ?? 0;

    public int PidNumero => IdentificadorUsb(Pid) ?? 0;

    /// <summary>«0x0FE6», «0FE6» o «4070» → 0x0FE6. null si está vacío o no es un identificador válido.</summary>
    public static int? IdentificadorUsb(string? valor)
    {
        var texto = (valor ?? string.Empty).Trim();
        if (texto.Length == 0)
            return null;
        if (texto.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            texto = texto[2..];
        return int.TryParse(texto, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var numero)
            && numero is > 0 and <= 0xFFFF ? numero : null;
    }
}
