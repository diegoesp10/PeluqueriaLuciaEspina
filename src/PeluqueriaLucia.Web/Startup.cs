using PeluqueriaLucia.Application;
using PeluqueriaLucia.Infrastructure;
using PeluqueriaLucia.Web.Configuracion;

namespace PeluqueriaLucia.Web;

/// <summary>Registro de dependencias por capas y canal de peticiones de la aplicación.</summary>
public sealed class Startup(IConfiguration configuration, IWebHostEnvironment environment)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAplicacion();
        services.AddInfraestructura(configuration);
        services.AddPresentacion(configuration, environment);
    }

    public void Configure(WebApplication app)
    {
        app.UseCanalDePeticiones();
        app.MapRutas();
    }
}
