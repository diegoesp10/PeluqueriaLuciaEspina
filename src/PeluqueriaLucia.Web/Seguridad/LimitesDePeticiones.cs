using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace PeluqueriaLucia.Web.Seguridad;

/// <summary>
/// Límite de llamadas por IP con ventana deslizante. Cada zona tiene el suyo (web pública, panel y formulario de acceso)
/// y hay además un tope global. Al superarlo se responde 429 con la cabecera Retry-After y queda anotado en el monitor.
/// </summary>
public static class LimitesDePeticiones
{
    public const string Publico = "publico";
    public const string Panel = "panel";
    public const string Acceso = "acceso";
    public const string Resenas = "resenas";

    public static IServiceCollection AddLimitesDePeticiones(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<OpcionesSeguridad>>((o, seguridad) =>
        {
            var limites = seguridad.Value.Limites;
            if (limites.PublicoPorMinuto < 1 || limites.PanelPorMinuto < 1 || limites.AccesoPorCuartoDeHora < 1 || limites.TotalPorMinuto < 1)
                throw new InvalidOperationException("Los límites de peticiones deben ser positivos.");
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(c =>
                PorIp(c, "total", limites.TotalPorMinuto, TimeSpan.FromMinutes(1)));
            o.AddPolicy(Publico, c => PorIp(c, Publico, limites.PublicoPorMinuto, TimeSpan.FromMinutes(1)));
            o.AddPolicy(Panel, c => PorIp(c, Panel, limites.PanelPorMinuto, TimeSpan.FromMinutes(1)));
            o.AddPolicy(Resenas, c => PorIp(c, Resenas, 6, TimeSpan.FromHours(1)));
            o.AddPolicy(Acceso, c => HttpMethods.IsPost(c.Request.Method)
                ? PorIp(c, Acceso, limites.AccesoPorCuartoDeHora, TimeSpan.FromMinutes(15))
                : PorIp(c, Publico, limites.PublicoPorMinuto, TimeSpan.FromMinutes(1)));
            o.OnRejected = RechazarAsync;
        });
        return services;
    }

    private static RateLimitPartition<string> PorIp(HttpContext contexto, string zona, int permitidas, TimeSpan ventana) =>
        RateLimitPartition.GetSlidingWindowLimiter($"{zona}|{IpCliente.De(contexto)}", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitidas,
            Window = ventana,
            SegmentsPerWindow = 6,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    private static async ValueTask RechazarAsync(OnRejectedContext contexto, CancellationToken ct)
    {
        var http = contexto.HttpContext;
        var ip = IpCliente.De(http);
        http.RequestServices.GetRequiredService<MonitorPeticiones>().AnotarRechazo(ip);
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(LimitesDePeticiones))
            .LogWarning("Límite de peticiones superado por {Ip} en {Ruta}", ip, http.Request.Path);

        var esperaPorDefecto = HttpMethods.IsPost(http.Request.Method) &&
            http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == Acceso ? 900 : 60;
        var segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera)
            ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)) : esperaPorDefecto;
        http.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        http.Response.ContentType = "text/plain; charset=utf-8";
        await http.Response.WriteAsync(
            $"Demasiadas peticiones seguidas desde tu conexión. Espera {segundos} segundos y vuelve a intentarlo.", ct);
    }
}
