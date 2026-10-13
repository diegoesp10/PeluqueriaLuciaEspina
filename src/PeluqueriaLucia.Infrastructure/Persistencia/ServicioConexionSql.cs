using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// Servicio conectado SQL, nunca appsettings ni User Secrets. En Azure, la cadena de conexión «Peluqueria» de la Web App
/// (sin contraseña: entra con la identidad administrada); en el IIS local, la credencial genérica de Windows.
/// </summary>
public static class ServicioConexionSql
{
    public const string Destino = "PeluqueriaLucia/SqlServer";

    /// <summary>
    /// Variables de entorno con la cadena: la que crea App Service para una cadena de tipo SQLAzure llamada «Peluqueria»
    /// y la forma estándar de .NET, útil para lanzar migraciones o herramientas contra Azure desde un PC.
    /// </summary>
    private static readonly string[] VariablesEntorno = ["SQLAZURECONNSTR_Peluqueria", "ConnectionStrings__Peluqueria"];

    public static string Obtener()
    {
        foreach (var variable in VariablesEntorno)
            if (Environment.GetEnvironmentVariable(variable) is { Length: > 0 } cadena)
                return Validar(cadena);

        var valor = LeerCredencial(Destino);
        if (valor is not null) return Validar(valor);
        // Desarrollo local sin contraseña. En otros entornos se exige una credencial explícita.
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            return ConexionLocal();
        throw new InvalidOperationException("Configura el servicio conectado SQL con el comando configurar-conexion bajo la identidad que ejecuta la web.");
    }

    public static string ConexionLocal() => new SqlConnectionStringBuilder
    {
        DataSource = "localhost", InitialCatalog = "PeluqueriaLuciaDb", IntegratedSecurity = true,
        TrustServerCertificate = true, Encrypt = false, ConnectTimeout = 15
    }.ConnectionString;

    public static string Validar(string valor)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(valor);
            if (string.IsNullOrWhiteSpace(b.DataSource) || string.IsNullOrWhiteSpace(b.InitialCatalog)) throw new ArgumentException();
            b.PersistSecurityInfo = false;
            return b.ConnectionString;
        }
        catch (ArgumentException) { throw new InvalidOperationException("La conexión SQL no es válida. Reconfigura el servicio conectado; revisa servidor y base de datos."); }
    }

    public static string? LeerCredencial(string destino)
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!CredRead(destino, 1, 0, out var ptr))
        {
            if (Marshal.GetLastWin32Error() == 1168) return null;
            throw new InvalidOperationException("No se puede acceder al almacén de credenciales de Windows.");
        }
        try
        {
            var c = Marshal.PtrToStructure<Credencial>(ptr);
            return Marshal.PtrToStringUni(c.Blob, checked((int)c.Tamano / 2));
        }
        finally { CredFree(ptr); }
    }

    public static void GuardarCredencial(string destino, string valor)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Este servicio utiliza el almacén de credenciales de Windows.");
        if (valor.Length > 1200) throw new ArgumentException("La credencial es demasiado larga.");
        var blob = Marshal.StringToCoTaskMemUni(valor);
        try
        {
            var c = new Credencial { Tipo = 1, Destino = destino, Tamano = (uint)(valor.Length * 2), Blob = blob, Persistencia = 2, Usuario = "PeluqueriaLucia" };
            if (!CredWrite(ref c, 0)) throw new InvalidOperationException("No se ha podido guardar la credencial de Windows.");
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(blob); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credencial
    {
        public uint Flags, Tipo;
        public string? Destino, Comentario;
        public System.Runtime.InteropServices.ComTypes.FILETIME UltimaEscritura;
        public uint Tamano;
        public IntPtr Blob;
        public uint Persistencia, NumeroAtributos;
        public IntPtr Atributos;
        public string? Alias, Usuario;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string destino, uint tipo, uint flags, out IntPtr credencial);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credencial credencial, uint flags);
    [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr ptr);
}
