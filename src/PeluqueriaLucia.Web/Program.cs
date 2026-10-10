using PeluqueriaLucia.Web;

var builder = WebApplication.CreateBuilder(args);
var startup = new Startup(builder.Configuration, builder.Environment);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app);
await app.RunAsync();

/// <summary>Punto de entrada; público para las pruebas de integración.</summary>
public partial class Program;
