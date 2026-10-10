using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Application.Visitas;

namespace PeluqueriaLucia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ClientesService>();
        services.AddScoped<CatalogoService>();
        services.AddScoped<VisitasService>();
        services.AddScoped<AccesoService>();
        return services;
    }
}
