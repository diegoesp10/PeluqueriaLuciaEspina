using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>Servicios de la capa web: MVC, seguridad, cultura, opciones y preparación de la base de datos al arrancar.</summary>
public static class PresentacionServiceCollectionExtensions
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    public static IServiceCollection AddPresentacion(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        services.AddControllersWithViews(o =>
        {
            // Acepta «12,50» y «12.50» por igual, sin depender del idioma de Windows
            o.ModelBinderProviders.Insert(0, new DecimalFlexibleModelBinderProvider());
            o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });

        services.AddSeguridad(configuration);

        // Fechas e importes siempre en español de España, sea cual sea el idioma del equipo o del navegador
        services.Configure<RequestLocalizationOptions>(o =>
        {
            o.DefaultRequestCulture = new RequestCulture(Espanol);
            o.SupportedCultures = [Espanol];
            o.SupportedUICultures = [Espanol];
            o.RequestCultureProviders.Clear();
        });

        services.Configure<DatosNegocio>(configuration.GetSection(DatosNegocio.Seccion));
        services.Configure<ContenidoPublico>(configuration.GetSection(ContenidoPublico.Seccion));
        services.Configure<OpcionesBaseDeDatos>(configuration.GetSection(OpcionesBaseDeDatos.Seccion));
        services.PostConfigure<OpcionesImagenes>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.WebRootPath, "imagenes")
            : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));

        services.AddHostedService<ArranqueBaseDeDatos>();
        services.PostConfigure<OpcionesFotosResenas>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.ContentRootPath, "AlmacenResenas") : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));
        return services;
    }

    /// <summary>Acceso al panel con usuario y contraseña, límites de peticiones por IP y monitor de actividad.</summary>
    private static void AddSeguridad(this IServiceCollection services, IConfiguration configuration)
    {
        var seccion = configuration.GetSection(OpcionesSeguridad.Seccion);
        var seguridad = seccion.Get<OpcionesSeguridad>() ?? new OpcionesSeguridad();
        services.Configure<OpcionesSeguridad>(seccion);
        if (seguridad.Limites.PublicoPorMinuto < 1 || seguridad.Limites.PanelPorMinuto < 1 ||
            seguridad.Limites.AccesoPorCuartoDeHora < 1 || seguridad.Limites.TotalPorMinuto < 1)
            throw new InvalidOperationException("Los límites de peticiones deben ser positivos.");
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownProxies.Clear();
            o.KnownIPNetworks.Clear();
            foreach (var proxy in seguridad.ProxiesConfiables) o.KnownProxies.Add(IPAddress.Parse(proxy));
            o.ForwardLimit = 1;
        });

        services.AddAutenticacionAdministrador();
        services.AddScoped<DesafioCookie>();
        services.AddLimitesDePeticiones();
        services.AddSingleton<MonitorPeticiones>();
        services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "PeluqueriaLucia.Antifalsificacion";
            o.Cookie.SameSite = SameSiteMode.Strict;
        });
        services.AddOptions<Microsoft.AspNetCore.Antiforgery.AntiforgeryOptions>().Configure<Microsoft.Extensions.Options.IOptions<OpcionesSeguridad>>(
            (o, opciones) => o.Cookie.SecurePolicy = opciones.Value.RequerirHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest);
        services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
    }
}
