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
        services.AddScoped<IProductoRepositorio, ProductoRepositorio>();
        services.AddScoped<IAdministradorRepositorio, AdministradorRepositorio>();
        services.AddScoped<ISesionRepositorio, SesionRepositorio>();
        services.AddScoped<IIntentoAccesoRepositorio, IntentoAccesoRepositorio>();
        services.AddSingleton<IHasherContrasenas, HasherContrasenas>();
        services.AddScoped<IMfaRepositorio, MfaRepositorio>();
        services.AddSingleton<IProtectorSecretos, ProtectorSecretos>();
        services.AddScoped<IContenidoRepositorio, ContenidoRepositorio>();
        services.AddScoped<PeluqueriaLucia.Application.Agenda.IRepositorioAgenda, RepositorioAgenda>();
        services.AddSingleton<Comunicaciones.Correo.IConfiguracionSmtp, ConfiguracionCorreo>();
        services.AddSingleton<Comunicaciones.Correo.IEnviadorCorreo, Comunicaciones.Correo.EnviadorCorreoSmtp>();
        services.AddSingleton<Comunicaciones.WhatsApp.IWhatsAppManual, Comunicaciones.WhatsApp.WhatsAppManual>();
        services.AddSingleton<PeluqueriaLucia.Application.Agenda.IUrlPublica, UrlPublica>();
        services.AddSingleton<PeluqueriaLucia.Application.Agenda.IAjustesComunicaciones, AjustesComunicaciones>();
        services.AddSingleton<IFotosResenas, FotosResenas>();
        services.AddOptions<OpcionesFotosResenas>().Bind(configuracion.GetSection("FotosResenas"));
        services.AddSingleton<IFotosProductos, FotosProductos>();
        services.AddOptions<OpcionesFotosProductos>().Bind(configuracion.GetSection(OpcionesFotosProductos.Seccion));

        // Claves de cifrado de cookies y tokens antifalsificación en la base de datos: sobreviven a reinicios de IIS
        // y no dependen de permisos de carpetas en el equipo
        var proteccion = services.AddDataProtection()
            .SetApplicationName("PeluqueriaLucia")
            .PersistKeysToDbContext<PeluqueriaDbContext>();
        // Las claves guardadas se cifran a su vez. En Azure, con una clave de Key Vault («ProteccionDatos:ClaveKeyVault»,
        // la URI sin versión), usando la identidad de la Web App o, desde un PC, la sesión de «az login». En el IIS
        // local, con DPAPI del equipo. Fuera de Windows no se permite guardarlas sin cifrar, salvo en las pruebas
        // automáticas («ProteccionDatos:PermitirSinCifrar», solo en GitHub Actions con bases de usar y tirar).
        if (configuracion["ProteccionDatos:ClaveKeyVault"] is { Length: > 0 } claveKeyVault)
            proteccion.ProtectKeysWithAzureKeyVault(new Uri(claveKeyVault), new Azure.Identity.DefaultAzureCredential());
        else if (OperatingSystem.IsWindows())
            proteccion.ProtectKeysWithDpapi(protectToLocalMachine: true);
        else if (!configuracion.GetValue<bool>("ProteccionDatos:PermitirSinCifrar"))
            throw new InvalidOperationException("Configura ProteccionDatos:ClaveKeyVault: fuera de Windows las claves de cifrado no pueden guardarse sin proteger.");

        services.AddOptions<OpcionesImagenes>().Bind(configuracion.GetSection(OpcionesImagenes.Seccion));
        services.AddSingleton<IAlmacenImagenes, AlmacenImagenesLocal>();
        services.AddSingleton<IExportadorVisitas, ExportadorExcelVisitas>();
        return services;
    }
}
