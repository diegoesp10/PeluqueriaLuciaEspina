using System.Text;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Web.Infraestructura.Impresion;

/// <summary>
/// Construye el ticket de una visita en comandos ESC/POS para la impresora térmica: el mismo contenido que el ticket en
/// pantalla (cabecera del negocio, líneas, total, desglose de IVA, forma de pago y código de opinión), en texto de ancho
/// fijo con página de códigos CP850 para las tildes y la ñ, y corte de papel al final.
/// </summary>
public static class TicketEscPos
{
    private const byte Esc = 0x1B;
    private const byte Gs = 0x1D;

    private static readonly Encoding Cp850 = Codificacion();

    public static byte[] Construir(TicketModelo modelo, ImpresoraTickets impresora, Stream? logo = null)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(impresora);

        var v = modelo.Visita;
        var n = modelo.Negocio;
        var ancho = Math.Clamp(impresora.AnchoCaracteres, 24, 64);
        var salida = new MemoryStream();

        void Bytes(params byte[] b) => salida.Write(b);
        void Linea(string texto = "") { salida.Write(Cp850.GetBytes(Normalizar(texto))); salida.WriteByte(0x0A); }
        void Centrar(bool si) => Bytes(Esc, 0x61, (byte)(si ? 1 : 0));
        void Negrita(bool si) => Bytes(Esc, 0x45, (byte)(si ? 1 : 0));
        string Separador() => new('-', ancho);
        string DosColumnas(string izquierda, string derecha)
        {
            var hueco = ancho - izquierda.Length - derecha.Length;
            return hueco > 0 ? izquierda + new string(' ', hueco) + derecha : izquierda + " " + derecha;
        }
        string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
        string TresColumnas(string a, int wa, string b, int wb, string c, int wc) =>
            Recortar(a, wa).PadRight(wa) + Recortar(b, wb).PadRight(wb) + Recortar(c, wc).PadLeft(wc);
        string TresColumnasDerecha(string a, int wa, string b, int wb, string c, int wc) =>
            Recortar(a, wa).PadRight(wa) + Recortar(b, wb).PadLeft(wb) + Recortar(c, wc).PadLeft(wc);

        // Inicializar la impresora y elegir la página de códigos PC850 (tildes, ñ)
        Bytes(Esc, 0x40);
        Bytes(Esc, 0x74, 0x02);

        if (logo is not null)
        {
            try
            {
                var raster = ImagenEscPos.ComandoRaster(logo, impresora.AnchoLogoPuntos);
                Centrar(true);
                Bytes(raster);
                Linea();
                Centrar(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                // Sin logo el ticket se imprime igual
            }
        }

        Centrar(true);
        Negrita(true);
        Linea(n.Nombre);
        Negrita(false);
        Linea(n.Localidad);
        Linea("Tlf " + n.Telefono);
        Linea(n.Direccion);
        Centrar(false);

        Linea(Separador());
        Linea(DosColumnas("Fact. simplificada: " + v.Id, v.Fecha.ToString("dd/MM/yy HH:mm", Formato.Espanol)));
        Linea("Empleado: " + n.Empleado);
        if (v.Cliente is not null)
            Linea("Cliente: " + v.Cliente.NombreCompleto);
        Linea("NIF: " + n.Nif);
        Linea(Separador());

        const int anchoUds = 4, anchoTotal = 9;
        var anchoDescripcion = ancho - anchoUds - anchoTotal;
        Negrita(true);
        Linea(TresColumnas("Uds", anchoUds, "Descripción", anchoDescripcion, "Total", anchoTotal));
        Negrita(false);
        foreach (var linea in v.Lineas)
            Linea(TresColumnas("1", anchoUds, linea.Nombre, anchoDescripcion, Importe(linea.Precio), anchoTotal));
        foreach (var producto in v.Productos)
            Linea(TresColumnas(producto.Cantidad.ToString(Formato.Espanol), anchoUds, producto.Nombre, anchoDescripcion, Importe(producto.Importe), anchoTotal));
        Linea(Separador());
        Negrita(true);
        Linea(DosColumnas("TOTAL", Importe(v.Total) + " EUR"));
        Negrita(false);
        Linea(Separador());

        const int anchoTipo = 10, anchoBase = 11;
        Negrita(true);
        Linea(TresColumnasDerecha("Tipo imp.", anchoTipo, "Base", anchoBase, "Cuota", ancho - anchoTipo - anchoBase));
        Negrita(false);
        Linea(TresColumnasDerecha(modelo.TipoImpuesto.ToString("0.##", Formato.Espanol) + "%", anchoTipo, Importe(modelo.Base), anchoBase, Importe(modelo.Cuota), ancho - anchoTipo - anchoBase));
        Linea(Separador());

        Centrar(true);
        Negrita(true);
        Linea(v.EstaCobrada ? "Contado (" + Formato.Texto(v.FormaPago) + ")" : "PENDIENTE DE COBRO");
        Negrita(false);
        Centrar(false);
        if (v.EstaCobrada && v.FormaPago == FormaPago.Mixto)
        {
            Linea(DosColumnas("Efectivo", Importe(v.TotalEfectivo ?? 0)));
            Linea(DosColumnas("Tarjeta", Importe(v.TotalTarjeta ?? 0)));
        }
        Centrar(true);
        Linea("Impuestos incluidos");
        Linea(n.PieTicket);

        if (modelo.CodigoOpinion is { Length: 32 } codigo)
        {
            Linea(Separador());
            Negrita(true);
            Linea("Tu experiencia cuenta");
            Negrita(false);
            Linea("Comparte tu opinión en la web");
            Linea("del salón, en Opiniones.");
            Linea("Código:");
            for (var i = 0; i < 8; i += 4)
                Linea(string.Join("-", Enumerable.Range(i, 4).Select(j => codigo.Substring(j * 4, 4))));
            Linea("Una opinión por ticket.");
            Linea("Válido 6 meses.");
        }
        Centrar(false);

        // Avanzar el papel y cortar (GS V B: corte total tras alimentar)
        Bytes(0x0A, 0x0A, 0x0A, 0x0A);
        Bytes(Gs, 0x56, 0x42, 0x00);

        return salida.ToArray();
    }

    private static string Importe(decimal importe) => importe.ToString("0.00", Formato.Espanol);

    /// <summary>Caracteres que CP850 no tiene y que la térmica imprimiría como «?».</summary>
    private static string Normalizar(string texto) => texto.Replace("€", "EUR").Replace("→", "-").Replace("«", "\"").Replace("»", "\"");

    private static Encoding Codificacion()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(850);
    }
}
