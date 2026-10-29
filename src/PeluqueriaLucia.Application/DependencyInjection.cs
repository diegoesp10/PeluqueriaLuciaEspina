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
        services.AddScoped<Agenda.IAgendaService, Agenda.AgendaService>();
        services.AddScoped<Agenda.IComunicacionesCitas, Agenda.ComunicacionesCitas>();
        services.AddScoped<ClientesService>();
        services.AddScoped<Agenda.ClientesAgendaService>();
        services.AddScoped<Agenda.VerificacionContactoService>();
        services.AddScoped<CatalogoService>();
        services.AddScoped<VisitasService>();
        services.AddScoped<Productos.ProductosService>();
        services.AddScoped<AccesoService>();
        services.AddScoped<MfaService>();
        services.AddScoped<Publico.ContenidoService>();
        services.AddScoped<Publico.ResenasService>();
        services.AddScoped<Sorteos.SorteosService>();
        return services;
    }
}
