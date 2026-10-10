using System.Text;
using Microsoft.AspNetCore.Hosting;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;

namespace PeluqueriaLuciaEspina.Services
{
    public interface ITicketPrinterService
    {
        // Construye el ticket en comandos ESC/POS y lo envía en RAW a la impresora
        // térmica configurada. Lanza InvalidOperationException si falla el envío.
        void ImprimirTicket(ServicioCliente servicio);
    }

    public class TicketPrinterService : ITicketPrinterService
    {
        private const byte ESC = 0x1B;
        private const byte GS = 0x1D;

        // "WinUsb": envía los bytes directamente al dispositivo USB (VID/PID) mediante libusb,
        //           sin pasar por Windows. Necesario si el driver de la impresora se sustituyó
        //           con Zadig (p.ej. para poder imprimir también desde una web con WebUSB).
        // "Spooler": método clásico vía winspool.drv/nombre de impresora de Windows. Solo
        //            funciona si la impresora sigue instalada como impresora de Windows
        //            (driver original, sin tocar con Zadig).
        private readonly string _backend;
        private readonly string? _printerName;
        private readonly int _vendorId;
        private readonly int _productId;

        private readonly int _anchoPapel;
        private readonly int _anchoLogoDots;
        private readonly string? _logoPath;
        private readonly Encoding _encoding;

        public TicketPrinterService(IConfiguration configuration, IWebHostEnvironment env)
        {
            _backend = configuration["TicketPrinter:Backend"] ?? "WinUsb";

            _printerName = configuration["TicketPrinter:Name"];
            _vendorId = ParseHex(configuration["TicketPrinter:Vid"]);
            _productId = ParseHex(configuration["TicketPrinter:Pid"]);

            if (_backend.Equals("WinUsb", StringComparison.OrdinalIgnoreCase))
            {
                if (_vendorId == 0 || _productId == 0)
                    throw new InvalidOperationException(
                        "Falta configurar \"TicketPrinter:Vid\" y \"TicketPrinter:Pid\" en appsettings.json " +
                        "(formato \"0x0FE6\") con el VID/PID USB de la impresora. Puedes verlos en el " +
                        "Administrador de dispositivos, propiedades del dispositivo > Detalles > Id. de hardware.");
            }
            else if (string.IsNullOrWhiteSpace(_printerName))
            {
                throw new InvalidOperationException("Falta configurar \"TicketPrinter:Name\" en appsettings.json con el nombre exacto de la impresora en Windows.");
            }

            _anchoPapel = configuration.GetValue<int?>("TicketPrinter:PaperWidthChars") ?? 32;
            _anchoLogoDots = configuration.GetValue<int?>("TicketPrinter:PrintWidthDots") ?? 384;

            var logoRelativo = configuration["TicketPrinter:LogoPath"] ?? "Img/Icono_bn.png";
            var candidato = Path.Combine(env.WebRootPath, logoRelativo.Replace('/', Path.DirectorySeparatorChar));
            _logoPath = File.Exists(candidato) ? candidato : null;

            // CP850 (multilingüe): soporta tildes y ñ en impresoras térmicas ESC/POS.
            _encoding = Encoding.GetEncoding(850);
        }

        private static int ParseHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) value = value[2..];
            return int.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        public void ImprimirTicket(ServicioCliente servicio)
        {
            var bytes = ConstruirTicket(servicio);

            if (_backend.Equals("WinUsb", StringComparison.OrdinalIgnoreCase))
                UsbPrinterHelper.SendBytesToPrinter(_vendorId, _productId, bytes);
            else
                RawPrinterHelper.SendBytesToPrinter(_printerName!, bytes);
        }

        private byte[] ConstruirTicket(ServicioCliente servicio)
        {
            var servicios = servicio.ServiciosRealizados.ToList();
            var total = servicios.Sum(s => s.PrecioAplicado);
            var metodoPago = servicio.OpcionPago switch
            {
                OpcionPago.Efectivo => "Contado (Efectivo)",
                OpcionPago.Tarjeta => "Contado (Tarjeta)",
                OpcionPago.Mixto => "Contado (Mixto)",
                _ => "Contado",
            };
            // El precio de cada servicio se considera IVA incluido; se desglosa base/cuota
            // según el porcentaje de IVA elegido al cobrar (0% o 21%).
            var baseImponible = servicio.PorcentajeIva > 0 ? total / (1 + servicio.PorcentajeIva / 100m) : total;
            var cuotaIva = total - baseImponible;

            using var buffer = new MemoryStream();

            void Raw(params byte[] b) => buffer.Write(b, 0, b.Length);
            void Text(string s)
            {
                var b = _encoding.GetBytes(s);
                buffer.Write(b, 0, b.Length);
            }
            void Line(string s = "")
            {
                Text(s);
                Raw(0x0A);
            }
            void Center(bool on) => Raw(ESC, 0x61, (byte)(on ? 1 : 0));
            void Bold(bool on) => Raw(ESC, 0x45, (byte)(on ? 1 : 0));

            string Separador() => new string('-', _anchoPapel);

            string DosColumnas(string izquierda, string derecha)
            {
                var espacio = _anchoPapel - izquierda.Length - derecha.Length;
                return espacio > 0 ? izquierda + new string(' ', espacio) + derecha : izquierda + " " + derecha;
            }

            string TresColumnas(string a, int wa, string b, int wb, string c, int wc)
            {
                string Fit(string s, int w) => s.Length > w ? s[..w] : s.PadRight(w);
                return Fit(a, wa) + Fit(b, wb) + c.PadLeft(wc);
            }

            string TresColumnasDer(string a, int wa, string b, int wb, string c, int wc)
            {
                string FitIzq(string s, int w) => (s.Length > w ? s[..w] : s).PadRight(w);
                string FitDer(string s, int w) => (s.Length > w ? s[..w] : s).PadLeft(w);
                return FitIzq(a, wa) + FitDer(b, wb) + FitDer(c, wc);
            }

            var wUds = 4;
            var wTotal = 8;
            var wDesc = Math.Max(1, _anchoPapel - wUds - wTotal);

            // Inicializar impresora y seleccionar página de códigos PC850 (tildes, ñ)
            Raw(ESC, 0x40);
            Raw(ESC, 0x74, 0x02);

            if (_logoPath != null)
            {
                try
                {
                    var raster = EscPosImage.BuildRasterCommand(_logoPath, _anchoLogoDots);
                    Center(true);
                    Raw(raster);
                    Line();
                    Center(false);
                }
                catch
                {
                    // Si el logo no se puede procesar (formato no soportado, archivo corrupto...)
                    // se omite y el ticket se sigue imprimiendo solo con texto.
                }
            }

            Center(true);
            Bold(true);
            Line("Lucia Peluqueria Ecologica");
            Bold(false);
            Line("Oviedo");
            Line("Tlf 659306659");
            Line("C/Pio XII, 10");
            Center(false);

            Line(Separador());
            Line(DosColumnas($"Fact. Simpli:{servicio.Id}", servicio.Fecha.ToString("dd/MM/yy")));
            Line("Empleado: lucia espina");
            if (servicio.Cliente != null)
            {
                Line($"Cliente: {servicio.Cliente.Nombre} {servicio.Cliente.Apellido}");
            }
            Line("NIF: 71678769M");
            Line(Separador());

            Bold(true);
            Line(TresColumnas("Uds", wUds, "Descripcion", wDesc, "Total", wTotal));
            Bold(false);
            foreach (var s in servicios)
            {
                Line(TresColumnas("1", wUds, s.ServicioGenerico?.Nombre ?? "Servicio", wDesc, s.PrecioAplicado.ToString("F2").Replace(".", ","), wTotal));
            }
            Line(Separador());
            Bold(true);
            Line(DosColumnas("TOTAL", total.ToString("F2").Replace(".", ",") + " EUR"));
            Bold(false);
            Line(Separador());

            var wTipo = 10;
            var wBase = 11;
            var wCuota = Math.Max(1, _anchoPapel - wTipo - wBase);

            Bold(true);
            Line(TresColumnasDer("Tipo Imp", wTipo, "Base", wBase, "Cuota", wCuota));
            Bold(false);
            Line(TresColumnasDer($"{servicio.PorcentajeIva:F0}%", wTipo, baseImponible.ToString("F2").Replace(".", ","), wBase, cuotaIva.ToString("F2").Replace(".", ","), wCuota));
            Line(Separador());

            Center(true);
            Bold(true);
            Line(servicio.Estado == Estado.Cobrado ? metodoPago : "PENDIENTE DE COBRO");
            Bold(false);
            Line("Impuestos Incluidos");
            Line("Gracias por su visita **");
            Center(false);

            // Avanzar papel y cortar (Function B: feed + corte total)
            Raw(0x0A, 0x0A, 0x0A, 0x0A);
            Raw(GS, 0x56, 0x42, 0x00);

            return buffer.ToArray();
        }
    }
}
