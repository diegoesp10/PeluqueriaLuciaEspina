using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>Servicios de la capa web: MVC, cultura, opciones y preparación de la base de datos al arrancar.</summary>
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

        // Fechas e importes siempre en español de España, sea cual sea el idioma del equipo o del navegador
        services.Configure<RequestLocalizationOptions>(o =>
        {
            o.DefaultRequestCulture = new RequestCulture(Espanol);
            o.SupportedCultures = [Espanol];
            o.SupportedUICultures = [Espanol];
            o.RequestCultureProviders.Clear();
        });

        services.Configure<DatosNegocio>(configuration.GetSection(DatosNegocio.Seccion));
        services.Configure<OpcionesBaseDeDatos>(configuration.GetSection(OpcionesBaseDeDatos.Seccion));
        services.PostConfigure<OpcionesImagenes>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.WebRootPath, "imagenes")
            : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));

        services.AddHostedService<ArranqueBaseDeDatos>();
        return services;
    }
}
