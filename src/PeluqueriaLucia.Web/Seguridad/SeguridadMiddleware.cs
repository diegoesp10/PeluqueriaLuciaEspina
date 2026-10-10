using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace PeluqueriaLucia.Web.Seguridad;

/// <summary>Cabeceras de seguridad, filtro de IP del panel y recuento de peticiones por IP.</summary>
public static class SeguridadMiddleware
{
    private const string ClaveNonce = "csp-nonce";

    /// <summary>Valor único por petición que autoriza los pocos scripts que van dentro de la página.</summary>
    public static string Nonce(HttpContext contexto) => (string)contexto.Items[ClaveNonce]!;

    /// <summary>
    /// Política de contenido estricta: solo se ejecutan scripts propios (ficheros de la web o con el nonce de la
    /// petición), no se puede incrustar la web en otra página y los formularios solo envían a la propia web.
    /// </summary>
    public static IApplicationBuilder UseCabecerasDeSeguridad(this IApplicationBuilder app) =>
        app.Use((contexto, siguiente) =>
        {
            var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            contexto.Items[ClaveNonce] = nonce;

            var cabeceras = contexto.Response.Headers;
            cabeceras.ContentSecurityPolicy =
                "default-src 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; " +
                "style-src 'self'; img-src 'self' data: blob:; font-src 'self'; connect-src 'self'; " +
                "object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            cabeceras.XContentTypeOptions = "nosniff";
            cabeceras.XFrameOptions = "DENY";
            cabeceras["Referrer-Policy"] = "strict-origin-when-cross-origin";
            cabeceras["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
            cabeceras["Cross-Origin-Opener-Policy"] = "same-origin";

            // Los datos del panel no se guardan en cachés compartidas
            if (EsPanel(contexto))
                contexto.Response.OnStarting(() =>
                {
                    contexto.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                });

            return siguiente(contexto);
        });

    /// <summary>Si hay redes permitidas configuradas, el panel solo responde a ellas; al resto, 404.</summary>
    public static IApplicationBuilder UseFiltroIpPanel(this IApplicationBuilder app)
    {
        var redes = app.ApplicationServices.GetRequiredService<IOptions<OpcionesSeguridad>>().Value.RedesPermitidasPanel
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Contains('/') ? IPNetwork.Parse(r.Trim()) : new IPNetwork(IPAddress.Parse(r.Trim()), IPAddress.Parse(r.Trim()).GetAddressBytes().Length * 8))
            .ToList();
        if (redes.Count == 0)
            return app;

        return app.Use((contexto, siguiente) =>
        {
            if ((EsPanel(contexto) || EsImagen(contexto)) && (IpCliente.Direccion(contexto) is not { } ip || !redes.Any(r => r.Contains(ip))))
            {
                contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            return siguiente(contexto);
        });
    }

    public static IApplicationBuilder UseMonitorPeticiones(this IApplicationBuilder app) =>
        app.Use((contexto, siguiente) =>
        {
            // Los ficheros estáticos (CSS, JS, imágenes) no cuentan: solo páginas y llamadas
            if (!Path.HasExtension(contexto.Request.Path.Value))
                contexto.RequestServices.GetRequiredService<MonitorPeticiones>().Anotar(IpCliente.De(contexto), EsPanel(contexto));
            return siguiente(contexto);
        });

    public static bool EsPanel(HttpContext contexto) => contexto.Request.Path.StartsWithSegments("/admin");
    public static bool EsImagen(HttpContext contexto) => contexto.Request.Path.StartsWithSegments("/imagenes");
}
