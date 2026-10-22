using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>Orden del canal de peticiones (middleware) y rutas.</summary>
public static class CanalDePeticionesExtensions
{
    private const string Inmutable = "public, max-age=31536000, immutable";

    public static WebApplication UseCanalDePeticiones(this WebApplication app)
    {
        var seguridad = app.Services.GetRequiredService<IOptions<OpcionesSeguridad>>().Value;
        if (seguridad.ProxiesConfiables.Length > 0)
            app.UseForwardedHeaders();
        else if (EnAzureAppService)
        {
            // App Service termina el HTTPS en su frontal y pasa la petición en HTTP con X-Forwarded-Proto. Sin esto, la
            // redirección a HTTPS entraría en bucle. Solo se acepta el protocolo, del frontal (el único que llega a la
            // aplicación): la IP del visitante la da la cabecera de Cloudflare.
            var protocolo = new ForwardedHeadersOptions { ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto };
            protocolo.KnownProxies.Clear();
            protocolo.KnownIPNetworks.Clear();
            app.UseForwardedHeaders(protocolo);
        }
        if (seguridad.CabeceraIpCliente is { Length: > 0 } cabecera)
            app.UseIpDeCabecera(cabecera);
        // /en/… → web en inglés; debe ir antes que todo lo que mira la ruta
        app.UsePrefijoDeIdioma();
        app.UseRequestLocalization();

        // Nunca publicar la traza de una excepción, tampoco bajo Visual Studio.
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = async contexto =>
            {
                var fallo = contexto.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                contexto.RequestServices.GetRequiredService<ILogger<Startup>>()
                    .LogError(fallo?.Error, "Fallo de petición. Referencia {Referencia}", contexto.TraceIdentifier);
                contexto.Response.StatusCode = 500;
                contexto.Response.Headers.CacheControl = "no-store";
                contexto.Response.Headers.XContentTypeOptions = "nosniff";
                contexto.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'self'; img-src 'self'; base-uri 'none'; frame-ancestors 'none'";
                contexto.Response.ContentType = "text/html; charset=utf-8";
                // Independiente de SQL, cookies, TempData y vistas: funciona incluso si falla Data Protection.
                await contexto.Response.WriteAsync(PaginaDeFallo(contexto));
            }
        });
        app.UseStatusCodePagesWithReExecute("/error/{0}");

        if (app.Services.GetRequiredService<IOptions<OpcionesSeguridad>>().Value.RequerirHttps)
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseCabecerasDeSeguridad();
        app.UseCacheDeRecursosVersionados();
        app.UseFiltroIpPanel();
        app.UseMonitorPeticiones();

        // Los recursos públicos funcionan también al ejecutar la publicación bajo Development.
        // No servir por este canal las fotos internas /imagenes: pasan por su controlador protegido.
        app.UseWhen(c => c.Request.Path.StartsWithSegments("/css") || c.Request.Path.StartsWithSegments("/js") ||
            c.Request.Path.StartsWithSegments("/Img") || c.Request.Path.StartsWithSegments("/lib") || c.Request.Path.StartsWithSegments("/iconos") || c.Request.Path == "/site.webmanifest" || c.Request.Path.StartsWithSegments("/publico") || c.Request.Path == "/favicon.ico",
            recursos => recursos.UseStaticFiles());

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    private static string PaginaDeFallo(HttpContext contexto)
    {
        var ingles = Idioma.EsIngles(contexto);
        var raiz = System.Net.WebUtility.HtmlEncode(contexto.Request.PathBase.Value + "/");
        var (idioma, titulo, texto, volver, referencia) = ingles
            ? ("en", "Something went wrong", "We couldn't complete this request. Please try again in a few minutes.", "Back to the salon", "Reference")
            : ("es", "Algo ha fallado", "No hemos podido completar esta operación. Vuelve a intentarlo en unos minutos.", "Volver al inicio", "Referencia");
        return $"<!doctype html><html lang=\"{idioma}\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>{titulo} · Lucía</title><link rel=\"stylesheet\" href=\"/css/error.css\"></head><body><main><img src=\"/Img/Icono.jpg\" width=\"100\" height=\"100\" alt=\"Lucía Peluquería Ecológica\"><h1>{titulo}</h1><p>{texto}</p><a href=\"{raiz}\">{volver}</a><p class=\"referencia\">{referencia}: {System.Net.WebUtility.HtmlEncode(contexto.TraceIdentifier)}</p></main></body></html>";
    }

    public static WebApplication MapRutas(this WebApplication app)
    {
        // CSS, JS e imágenes propias: con huella en la URL, comprimidos y guardados por el navegador hasta que cambian
        app.MapStaticAssets().AllowAnonymous();

        // Panel de administración en /admin; la web pública en la raíz
        app.MapAreaControllerRoute(name: "panel", areaName: "Admin", pattern: "admin/{controller=Panel}/{action=Index}/{id?}")
            .WithStaticAssets();
        app.MapControllerRoute(name: "publico", pattern: "{controller=Inicio}/{action=Index}/{id?}")
            .WithStaticAssets();
        app.MapSalud();

        // Direcciones de antes de mover la gestión a /admin (favoritos, accesos directos): llevan a su sitio nuevo,
        // que pide sesión. Lo demás que no existe acaba en la página 404.
        foreach (var antigua in new[] { "Clientes", "Visitas", "Catalogo", "imagenes" })
            app.MapGet($"/{antigua}/{{**resto}}", (HttpContext c) =>
                    Results.Redirect($"{c.Request.PathBase}/admin{c.Request.Path}{c.Request.QueryString}"))
                .AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Los CSS y JS se enlazan con su huella (<c>?v=…</c>, de <c>asp-append-version</c>): si cambian, cambia la URL. Así el
    /// navegador puede guardarlos un año sin volver a preguntar en cada página, que es lo que hacía lenta la navegación.
    /// </summary>
    /// <summary>Azure App Service define esta variable en todas sus aplicaciones.</summary>
    private static bool EnAzureAppService => Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") is { Length: > 0 };

    private static void UseCacheDeRecursosVersionados(this WebApplication app) =>
        app.Use((contexto, siguiente) =>
        {
            if (contexto.Request.Query.ContainsKey("v") && Path.HasExtension(contexto.Request.Path.Value))
            {
                contexto.Response.OnStarting(() =>
                {
                    if (contexto.Response.StatusCode == StatusCodes.Status200OK)
                        contexto.Response.Headers[HeaderNames.CacheControl] = Inmutable;
                    return Task.CompletedTask;
                });
            }

            return siguiente(contexto);
        });

}
