using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>Orden del canal de peticiones (middleware) y rutas.</summary>
public static class CanalDePeticionesExtensions
{
    private const string Inmutable = "public, max-age=31536000, immutable";

    public static WebApplication UseCanalDePeticiones(this WebApplication app)
    {
        if (app.Services.GetRequiredService<IOptions<OpcionesSeguridad>>().Value.ProxiesConfiables.Length > 0)
            app.UseForwardedHeaders();
        app.UseRequestLocalization();

        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/error");
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

        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
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
        return app;
    }

    /// <summary>
    /// Los CSS y JS se enlazan con su huella (<c>?v=…</c>, de <c>asp-append-version</c>): si cambian, cambia la URL. Así el
    /// navegador puede guardarlos un año sin volver a preguntar en cada página, que es lo que hacía lenta la navegación.
    /// </summary>
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
