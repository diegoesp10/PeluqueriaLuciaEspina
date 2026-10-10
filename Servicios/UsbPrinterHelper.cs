using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace PeluqueriaLuciaEspina.Services
{
    // Envía bytes en crudo (RAW) directamente a una impresora térmica por USB, sin pasar por el
    // spooler de Windows (winspool.drv). Necesario cuando el driver de la impresora se ha
    // sustituido con Zadig (WinUSB/libusbK/libusb-win32) para poder imprimir también desde una
    // web (WebUSB): a partir de ese momento la impresora deja de existir como impresora
    // instalada en Windows, así que RawPrinterHelper (basado en winspool) ya no puede usarse.
    // Usa LibUsbDotNet sobre libusb-1.0, que es compatible con el mismo driver WinUSB que
    // instala Zadig.
    public static class UsbPrinterHelper
    {
        // Envía `data` al dispositivo USB identificado por `vendorId`/`productId`, escribiendo
        // en su primer endpoint OUT (de tipo Bulk). Lanza InvalidOperationException con un
        // mensaje explicativo si el dispositivo no se encuentra o si otra aplicación (p.ej. una
        // pestaña del navegador con WebUSB) tiene el dispositivo abierto en ese momento.
        public static void SendBytesToPrinter(int vendorId, int productId, byte[] data)
        {
            using var context = new UsbContext();

            IUsbDevice? device = context.List()
                .FirstOrDefault(d => d.VendorId == vendorId && d.ProductId == productId);

            if (device == null)
            {
                throw new InvalidOperationException(
                    $"No se encontró ninguna impresora USB con VID 0x{vendorId:X4} PID 0x{productId:X4}. " +
                    "Comprueba que está encendida y conectada, y que el VID/PID configurado en " +
                    "\"TicketPrinter\" de appsettings.json es correcto.");
            }

            try
            {
                device.Open();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"No se pudo abrir la impresora USB (VID 0x{vendorId:X4} PID 0x{productId:X4}): {ex.Message}", ex);
            }

            try
            {
                var iface = device.Configs
                    .SelectMany(c => c.Interfaces)
                    .FirstOrDefault();

                if (iface == null)
                {
                    throw new InvalidOperationException("La impresora USB no expone ninguna interfaz utilizable.");
                }

                var outEndpoint = iface.Endpoints
                    .FirstOrDefault(e => ((byte)e.EndpointAddress & 0x80) == 0);

                if (outEndpoint == null)
                {
                    throw new InvalidOperationException("La impresora USB no tiene ningún endpoint de salida (OUT) para enviar datos.");
                }

                try
                {
                    device.ClaimInterface(iface.Number);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "No se pudo reservar el acceso a la impresora USB. Normalmente significa que otra " +
                        "aplicación la tiene abierta en este momento (por ejemplo, una pestaña del navegador " +
                        $"usando WebUSB); ciérrala e inténtalo de nuevo. Detalle: {ex.Message}", ex);
                }

                try
                {
                    var writer = device.OpenEndpointWriter((WriteEndpointID)(byte)outEndpoint.EndpointAddress);
                    var resultado = writer.Write(data, 5000, out int bytesEscritos);

                    if (resultado != LibUsbDotNet.Error.Success || bytesEscritos != data.Length)
                    {
                        throw new InvalidOperationException(
                            $"No se pudieron escribir todos los bytes en la impresora USB " +
                            $"({bytesEscritos}/{data.Length} bytes, resultado: {resultado}).");
                    }
                }
                finally
                {
                    device.ReleaseInterface(iface.Number);
                }
            }
            finally
            {
                device.Close();
            }
        }
    }
}
