using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PeluqueriaLucia.Infrastructure.Almacenamiento;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>Orden del canal de peticiones (middleware) y rutas.</summary>
public static class CanalDePeticionesExtensions
{
    private const string Inmutable = "public, max-age=31536000, immutable";

    public static WebApplication UseCanalDePeticiones(this WebApplication app)
    {
        app.UseRequestLocalization();

        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/error");
        app.UseStatusCodePagesWithReExecute("/error/{0}");

        app.UseCacheDeRecursosVersionados();
        app.UseFotosDelCatalogo();
        app.UseRouting();
        return app;
    }

    public static WebApplication MapRutas(this WebApplication app)
    {
        // CSS, JS e imágenes propias: con huella en la URL, comprimidos y guardados por el navegador hasta que cambian
        app.MapStaticAssets();
        app.MapControllerRoute(name: "default", pattern: "{controller=Visitas}/{action=Index}/{id?}")
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

    /// <summary>
    /// Las fotos del catálogo se suben con la aplicación en marcha, así que no forman parte de los recursos estáticos
    /// compilados. Tienen nombre único (un Guid) y nunca cambian: el navegador las guarda sin volver a preguntar.
    /// </summary>
    private static void UseFotosDelCatalogo(this WebApplication app)
    {
        var opciones = app.Services.GetRequiredService<IOptions<OpcionesImagenes>>().Value;
        Directory.CreateDirectory(opciones.Carpeta);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(opciones.Carpeta),
            RequestPath = opciones.RutaPublica,
            OnPrepareResponse = c => c.Context.Response.Headers[HeaderNames.CacheControl] = Inmutable,
        });
    }
}
