using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Infrastructure.Seguridad;

namespace PeluqueriaLucia.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuracion, string? conexionPrueba = null)
    {
        // Resolver al abrir el contexto permite mostrar un error genérico si el servicio conectado falla.
        services.AddDbContext<PeluqueriaDbContext>(o => o.UseSqlServer(conexionPrueba ?? ServicioConexionSql.Obtener(), sql => sql.EnableRetryOnFailure(3)));
        services.AddScoped<IUnidadDeTrabajo>(sp => sp.GetRequiredService<PeluqueriaDbContext>());
        services.AddScoped<IClienteRepositorio, ClienteRepositorio>();
        services.AddScoped<IServicioRepositorio, ServicioRepositorio>();
        services.AddScoped<IVisitaRepositorio, VisitaRepositorio>();
        services.AddScoped<IAdministradorRepositorio, AdministradorRepositorio>();
        services.AddScoped<ISesionRepositorio, SesionRepositorio>();
        services.AddScoped<IIntentoAccesoRepositorio, IntentoAccesoRepositorio>();
        services.AddSingleton<IHasherContrasenas, HasherContrasenas>();
        services.AddScoped<IMfaRepositorio, MfaRepositorio>();
        services.AddSingleton<IProtectorSecretos, ProtectorSecretos>();
        services.AddScoped<IContenidoRepositorio, ContenidoRepositorio>();
        services.AddScoped<PeluqueriaLucia.Application.Agenda.IAgendaService, AgendaService>();
        services.AddScoped<PeluqueriaLucia.Application.Agenda.ISmsCitas, SmsCitas>();
        services.AddHttpClient("sms-citas", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<IFotosResenas, FotosResenas>();
        services.AddOptions<OpcionesFotosResenas>().Bind(configuracion.GetSection("FotosResenas"));

        // Claves de cifrado de cookies y tokens antifalsificación en la base de datos: sobreviven a reinicios de IIS
        // y no dependen de permisos de carpetas en el equipo
        var proteccion = services.AddDataProtection()
            .SetApplicationName("PeluqueriaLucia")
            .PersistKeysToDbContext<PeluqueriaDbContext>();
        if (OperatingSystem.IsWindows())
            proteccion.ProtectKeysWithDpapi(protectToLocalMachine: true);

        services.AddOptions<OpcionesImagenes>().Bind(configuracion.GetSection(OpcionesImagenes.Seccion));
        services.AddSingleton<IAlmacenImagenes, AlmacenImagenesLocal>();
        services.AddSingleton<IExportadorVisitas, ExportadorExcelVisitas>();
        return services;
    }
}
