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

    public static IServiceCollection AddPresentacion(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        // Traducciones de la web pública: las mismas para vistas, validaciones y controladores
        var textos = new Textos();
        services.AddSingleton(textos);
        services.AddControllersWithViews(o =>
        {
            // Acepta «12,50» y «12.50» por igual, sin depender del idioma de Windows
            o.ModelBinderProviders.Insert(0, new DecimalFlexibleModelBinderProvider());
            o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
            // Sin esto, un valor mal escrito («25:99» en una hora) mostraba el mensaje del framework en inglés
            var m = o.ModelBindingMessageProvider;
            m.SetAttemptedValueIsInvalidAccessor((valor, _) => textos.F("El valor «{0}» no es válido.", valor));
            m.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => textos.F("El valor «{0}» no es válido.", valor));
            m.SetUnknownValueIsInvalidAccessor(_ => textos["Revisa este dato."]);
            m.SetNonPropertyUnknownValueIsInvalidAccessor(() => textos["Revisa este dato."]);
            m.SetValueIsInvalidAccessor(valor => textos.F("El valor «{0}» no es válido.", valor));
            m.SetValueMustBeANumberAccessor(_ => textos["Escribe un número."]);
            m.SetNonPropertyValueMustBeANumberAccessor(() => textos["Escribe un número."]);
            m.SetValueMustNotBeNullAccessor(_ => textos["Este dato es obligatorio."]);
            m.SetMissingBindRequiredValueAccessor(_ => textos["Este dato es obligatorio."]);
            m.SetMissingKeyOrValueAccessor(() => textos["Este dato es obligatorio."]);
            m.SetMissingRequestBodyRequiredValueAccessor(() => textos["Faltan los datos del formulario."]);
        }).AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, _) => textos);

        services.AddSeguridad(configuration);

        // Español de España salvo en la web pública bajo /en (ver Idioma). No depende del idioma del equipo ni del navegador.
        services.Configure<RequestLocalizationOptions>(o =>
        {
            o.DefaultRequestCulture = new RequestCulture(Idioma.CulturaEspanol);
            o.SupportedCultures = [Idioma.CulturaEspanol, Idioma.CulturaIngles];
            o.SupportedUICultures = [Idioma.CulturaEspanol, Idioma.CulturaIngles];
            o.RequestCultureProviders.Clear();
            o.RequestCultureProviders.Add(new CustomRequestCultureProvider(c => Task.FromResult<ProviderCultureResult?>(
                Idioma.EsIngles(c) ? new ProviderCultureResult("en-GB") : null)));
        });

        services.Configure<DatosNegocio>(configuration.GetSection(DatosNegocio.Seccion));
        services.Configure<Infraestructura.Impresion.ImpresoraTickets>(configuration.GetSection(Infraestructura.Impresion.ImpresoraTickets.Seccion));
        services.Configure<ContenidoPublico>(configuration.GetSection(ContenidoPublico.Seccion));
        // Nombre, teléfono, dirección y logo del salón para los avisos a clientes (plantilla de correo y textos)
        services.AddMemoryCache();
        services.AddScoped<TelefonosPublicos>();
        services.AddScoped<PeluqueriaLucia.Application.Agenda.IDatosSalon, DatosSalonWeb>();
        services.Configure<OpcionesBaseDeDatos>(configuration.GetSection(OpcionesBaseDeDatos.Seccion));
        services.PostConfigure<OpcionesImagenes>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.WebRootPath, "imagenes")
            : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));

        services.AddHostedService<ArranqueBaseDeDatos>();
        services.AddHostedService<EnvioCorreosCitas>();
        services.PostConfigure<OpcionesFotosResenas>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.ContentRootPath, "AlmacenResenas") : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));
        // Fotos de productos: cada entorno declara su carpeta en «FotosProductos:Carpeta» (desarrollo, appsettings.Development.json;
        // Azure, el ajuste FotosProductos__Carpeta=/home/data/productos). Sin ella, AlmacenProductos en la raíz de la web.
        services.PostConfigure<OpcionesFotosProductos>(o => o.Carpeta = string.IsNullOrWhiteSpace(o.Carpeta)
            ? Path.Combine(environment.ContentRootPath, "AlmacenProductos") : Path.GetFullPath(o.Carpeta, environment.ContentRootPath));
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
