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

var builder = WebApplication.CreateBuilder(args);

// Textos y datos de la web pública (horario, servicios, contacto): se editan sin recompilar
builder.Configuration.AddJsonFile("contenido-publico.json", optional: false, reloadOnChange: true);

var startup = new Startup(builder.Configuration, builder.Environment);
startup.ConfigureServices(builder.Services);

var app = builder.Build();

// «dotnet PeluqueriaLucia.Web.dll administrador»: crea el usuario del panel desde la consola y termina
if (HerramientaAdministrador.EsComando(args))
    return await HerramientaAdministrador.EjecutarAsync(app.Services, recuperarMfa: args[0] == "recuperar-mfa");

startup.Configure(app);
await app.RunAsync();
return 0;

/// <summary>Punto de entrada; público para las pruebas de integración.</summary>
public partial class Program;
