using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Infrastructure;

public static class DependencyInjection
{
    public const string NombreCadenaConexion = "DefaultConnection";

    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString(NombreCadenaConexion);
        if (string.IsNullOrWhiteSpace(cadena))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión «{NombreCadenaConexion}». En el equipo de la peluquería va en " +
                "appsettings.Production.json (ver docs/DESPLIEGUE.md).");
        }

        services.AddDbContext<PeluqueriaDbContext>(o => o.UseSqlServer(cadena, sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped<IUnidadDeTrabajo>(sp => sp.GetRequiredService<PeluqueriaDbContext>());
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IServicioRepositorio, ServicioRepositorio>();
        services.AddScoped<IVisitaRepositorio, VisitaRepositorio>();

        services.AddOptions<OpcionesImagenes>().Bind(configuracion.GetSection(OpcionesImagenes.Seccion));
        services.AddSingleton<IAlmacenImagenes, AlmacenImagenesLocal>();
        services.AddSingleton<IExportadorVisitas, ExportadorExcelVisitas>();
        return services;
    }
}
