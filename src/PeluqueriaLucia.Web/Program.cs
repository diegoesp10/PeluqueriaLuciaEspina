using PeluqueriaLucia.Web;
using PeluqueriaLucia.Web.Configuracion;

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
