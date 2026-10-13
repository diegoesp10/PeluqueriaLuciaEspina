using System.Text;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Infraestructura.Impresion;
using SkiaSharp;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>El ticket en ESC/POS para la impresora térmica del salón, que el navegador manda por WebUSB.</summary>
public sealed class TicketTermicoTests
{
    private static readonly DateTime Fecha = new(2026, 10, 13, 17, 30, 0);
    private static readonly byte[] Cortar = [0x1D, 0x56, 0x42, 0x00];
    private static readonly byte[] Raster = [0x1D, 0x76, 0x30, 0x00];

    private static readonly DatosNegocio Negocio = new()
    {
        Nombre = "Lucía Peluquería Ecológica", Direccion = "C/ Pío XII, 10", Localidad = "Oviedo", Telefono = "659 306 659",
        Nif = "71678769M", Empleado = "Lucía Espina", TipoImpuesto = 21, PieTicket = "Gracias por su visita",
    };

    private static readonly ImpresoraTickets Impresora = new() { Vid = "0x0FE6", Pid = "0x811E" };

    private static Encoding Cp850()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(850);
    }

    private static Visita VisitaCobrada(FormaPago forma = FormaPago.Tarjeta, decimal? efectivo = null, decimal? tarjeta = null)
    {
        var cliente = Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, Fecha);
        var visita = Visita.Registrar(cliente, Fecha, forma,
            [Servicio.Crear("Corte señora", 18.50m, Sexo.Mujer, Fecha), Servicio.Crear("Tinte ecológico", 42m, Sexo.Mujer, Fecha)]);
        visita.Cobrar(forma, efectivo, tarjeta);
        return visita;
    }

    [Fact]
    public void El_ticket_lleva_cabecera_lineas_total_iva_y_corte_en_cp850()
    {
        var bytes = TicketEscPos.Construir(new TicketModelo(VisitaCobrada(), Negocio), Impresora);
        var texto = Cp850().GetString(bytes);

        Assert.Equal([0x1B, 0x40, 0x1B, 0x74, 0x02], bytes[..5]);
        Assert.Contains("Lucía Peluquería Ecológica", texto);
        Assert.Contains("Cliente: Ana García", texto);
        Assert.Contains("NIF: 71678769M", texto);
        Assert.Contains("Corte señora", texto);
        Assert.Contains("Tinte ecológico", texto);
        Assert.Contains("TOTAL", texto);
        Assert.Contains("60,50 EUR", texto);
        Assert.Contains("21%", texto);
        Assert.Contains("50,00", texto);
        Assert.Contains("10,50", texto);
        Assert.Contains("Contado (Tarjeta)", texto);
        Assert.DoesNotContain("€", texto);
        Assert.DoesNotContain("?", texto);
        Assert.Equal(Cortar, bytes[^4..]);
        Assert.DoesNotContain(Trozos(bytes), t => t.SequenceEqual(Raster));
    }

    [Fact]
    public void Las_lineas_caben_en_el_ancho_del_papel()
    {
        var bytes = TicketEscPos.Construir(new TicketModelo(VisitaCobrada(), Negocio), new ImpresoraTickets { AnchoCaracteres = 32 });
        var lineas = Cp850().GetString(bytes).Split('\n')
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, "\x1B..", string.Empty))
            .Select(l => new string(l.Where(c => c >= ' ').ToArray()));
        Assert.All(lineas.Where(l => l.Contains("Corte") || l.Contains("TOTAL") || l.StartsWith("Uds", StringComparison.Ordinal)),
            l => Assert.Equal(32, l.Length));
    }

    [Fact]
    public void El_pago_mixto_y_el_codigo_de_opinion_se_imprimen()
    {
        var visita = VisitaCobrada(FormaPago.Mixto, 20m, 40.50m);
        var codigo = new string('A', 32);
        var texto = Cp850().GetString(TicketEscPos.Construir(new TicketModelo(visita, Negocio, codigo), Impresora));

        Assert.Contains("Contado (Mixto)", texto);
        Assert.Contains("Efectivo", texto);
        Assert.Contains("20,00", texto);
        Assert.Contains("Tarjeta", texto);
        Assert.Contains("Tu experiencia cuenta", texto);
        Assert.Contains("AAAA-AAAA-AAAA-AAAA", texto);
    }

    [Fact]
    public void El_logo_va_como_imagen_raster_del_ancho_del_papel()
    {
        using var logo = new SKBitmap(100, 50);
        using (var lienzo = new SKCanvas(logo))
        {
            lienzo.Clear(SKColors.White);
            lienzo.DrawRect(new SKRect(0, 0, 50, 50), new SKPaint { Color = SKColors.Black });
        }
        using var png = SKImage.FromBitmap(logo).Encode(SKEncodedImageFormat.Png, 100);
        using var imagen = new MemoryStream(png.ToArray());

        var raster = ImagenEscPos.ComandoRaster(imagen, 384);

        Assert.Equal(Raster, raster[..4]);
        var anchoBytes = raster[4] | raster[5] << 8;
        var alto = raster[6] | raster[7] << 8;
        Assert.Equal(13, anchoBytes);
        Assert.Equal(50, alto);
        Assert.Equal(8 + anchoBytes * alto, raster.Length);
        Assert.Equal(0xFF, raster[8]);
        Assert.Equal(0x00, raster[8 + anchoBytes - 1]);

        imagen.Position = 0;
        var bytes = TicketEscPos.Construir(new TicketModelo(VisitaCobrada(), Negocio), Impresora, imagen);
        Assert.Contains(Trozos(bytes), t => t.SequenceEqual(Raster));
    }

    [Theory]
    [InlineData("0x0FE6", 0x0FE6)]
    [InlineData("811e", 0x811E)]
    [InlineData("", null)]
    [InlineData("0x", null)]
    [InlineData("zz", null)]
    [InlineData("0x10000", null)]
    public void Los_identificadores_usb_se_leen_en_hexadecimal(string valor, int? esperado) =>
        Assert.Equal(esperado, ImpresoraTickets.IdentificadorUsb(valor));

    [Fact]
    public void Sin_vid_y_pid_no_hay_impresora_termica()
    {
        Assert.False(new ImpresoraTickets().Configurada);
        Assert.True(Impresora.Configurada);
        Assert.Equal(0x0FE6, Impresora.VidNumero);
    }

    private static IEnumerable<byte[]> Trozos(byte[] bytes) => Enumerable.Range(0, Math.Max(0, bytes.Length - 3)).Select(i => bytes[i..(i + 4)]);
}
