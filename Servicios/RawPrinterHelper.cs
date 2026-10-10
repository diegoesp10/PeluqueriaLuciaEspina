using System.Runtime.InteropServices;

namespace PeluqueriaLuciaEspina.Services
{
    // Envía bytes en crudo (RAW) directamente a una cola de impresión de Windows,
    // sin pasar por GDI ni por ningún diálogo de impresión del sistema o del navegador.
    // Técnica estándar de Microsoft (KB322091) usando winspool.drv.
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In] ref DOCINFOA pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        // Envía `data` directamente a la cola `printerName` en modo RAW (tipo de dato "RAW",
        // sin driver GDI de por medio). Requiere que la impresora acepte RAW (impresoras
        // térmicas ESC/POS lo hacen de forma nativa). Lanza InvalidOperationException si
        // cualquier paso de la API de Windows falla.
        public static void SendBytesToPrinter(string printerName, byte[] data)
        {
            if (!OpenPrinter(printerName, out var hPrinter, IntPtr.Zero))
            {
                throw new InvalidOperationException(
                    $"No se pudo abrir la impresora \"{printerName}\" (código Win32 {Marshal.GetLastWin32Error()}). " +
                    "Comprueba que el nombre coincide exactamente con el de \"Impresoras y escáneres\" de Windows.");
            }

            try
            {
                var docInfo = new DOCINFOA
                {
                    pDocName = "Ticket Peluqueria Lucia Espina",
                    pOutputFile = null,
                    pDataType = "RAW",
                };

                if (!StartDocPrinter(hPrinter, 1, ref docInfo))
                    throw new InvalidOperationException($"No se pudo iniciar el trabajo de impresión (código Win32 {Marshal.GetLastWin32Error()}).");

                try
                {
                    if (!StartPagePrinter(hPrinter))
                        throw new InvalidOperationException($"No se pudo iniciar la página de impresión (código Win32 {Marshal.GetLastWin32Error()}).");

                    try
                    {
                        var pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
                        try
                        {
                            Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
                            if (!WritePrinter(hPrinter, pUnmanagedBytes, data.Length, out var written) || written != data.Length)
                                throw new InvalidOperationException($"No se pudieron escribir todos los bytes en la impresora (código Win32 {Marshal.GetLastWin32Error()}).");
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pUnmanagedBytes);
                        }
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }
    }
}
