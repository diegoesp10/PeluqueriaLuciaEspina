using PeluqueriaLucia.Web;
using PeluqueriaLucia.Web.Configuracion;
using PeluqueriaLucia.Infrastructure.Persistencia;

if (args.FirstOrDefault() == "configurar-conexion")
{
    try
    {
        Console.WriteLine("Servicio conectado SQL. La credencial se guarda en Windows, fuera del repositorio.");
        var valor = HerramientaAdministrador.LeerOculto("Cadena SQL (Enter para conexión local con autenticación Windows): ");
        var cadena = ServicioConexionSql.Validar(string.IsNullOrWhiteSpace(valor) ? ServicioConexionSql.ConexionLocal() : valor);
        await using var conexion = new Microsoft.Data.SqlClient.SqlConnection(cadena);
        await conexion.OpenAsync();
        ServicioConexionSql.GuardarCredencial(ServicioConexionSql.Destino, cadena);
        Console.WriteLine("Conexión verificada y guardada. No se han mostrado credenciales.");
        return 0;
    }
    catch (Exception) { Console.Error.WriteLine("No se ha podido verificar o guardar la conexión. Revisa servidor, base de datos y permisos."); return 1; }
}

if (args.FirstOrDefault() == "configurar-sms")
{
    Console.WriteLine("Twilio SMS. No se enviarán mensajes al configurar.");
    Console.Write("Account SID: "); var cuenta = Console.ReadLine() ?? "";
    var token = HerramientaAdministrador.LeerOculto("Auth Token (oculto): ");
    Console.Write("Número remitente Twilio (+ prefijo internacional): "); var numero = Console.ReadLine() ?? "";
    if (!System.Text.RegularExpressions.Regex.IsMatch(cuenta,"^AC[a-fA-F0-9]{32}$") ||
        !System.Text.RegularExpressions.Regex.IsMatch(token,"^[a-fA-F0-9]{32}$") ||
        !System.Text.RegularExpressions.Regex.IsMatch(numero,"^\\+[1-9][0-9]{6,14}$"))
    { Console.Error.WriteLine("Revisa la cuenta, el token y el número."); return 1; }
    try
    {
        ServicioConexionSql.GuardarCredencial("PeluqueriaLucia/TwilioSms", System.Text.Json.JsonSerializer.Serialize(new CredencialSms(cuenta,token,numero,DateTime.UtcNow)));
        Console.WriteLine("Credencial guardada. Para activar establece PELUQUERIA_SMS_ACTIVO=true y PELUQUERIA_SMS_URL_PUBLICA=https://tu-dominio, y configura los webhooks del proveedor. Ver docs/AGENDA.md.");
        return 0;
    }
    catch(Exception) { Console.Error.WriteLine("No se ha podido guardar la credencial SMS en Windows."); return 1; }
}

var builder = WebApplication.CreateBuilder(args);

// Textos y datos de la web pública (horario, servicios, contacto): se editan sin recompilar
builder.Configuration.AddJsonFile("contenido-publico.json", optional: false, reloadOnChange: true);

var startup = new Startup(builder.Configuration, builder.Environment);
startup.ConfigureServices(builder.Services);

var app = builder.Build();

// «dotnet PeluqueriaLucia.Web.dll administrador»: crea el usuario del panel desde la consola y termina
if (HerramientaAdministrador.EsComando(args))
    return await HerramientaAdministrador.EjecutarAsync(app.Services, recuperarMfa: args[0] == "recuperar-mfa", soloCrear: args[0] == "administrador-nuevo");

startup.Configure(app);
await app.RunAsync();
return 0;

/// <summary>Punto de entrada; público para las pruebas de integración.</summary>
public partial class Program;
