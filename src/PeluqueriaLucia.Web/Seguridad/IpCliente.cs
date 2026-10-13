using System.Net;

namespace PeluqueriaLucia.Web.Seguridad;

public static class IpCliente
{
    /// <summary>IP de quien hace la petición, en texto. Las IPv4 que llegan como IPv6 («::ffff:1.2.3.4») se normalizan.</summary>
    public static string De(HttpContext contexto) => Direccion(contexto)?.ToString() ?? "desconocida";

    public static IPAddress? Direccion(HttpContext contexto) =>
        contexto.Connection.RemoteIpAddress is { IsIPv4MappedToIPv6: true } mapeada ? mapeada.MapToIPv4() : contexto.Connection.RemoteIpAddress;

    /// <summary>
    /// Toma la IP del visitante de la cabecera de un proxy de confianza (Cloudflare: «CF-Connecting-IP») para que los
    /// límites por IP, el filtro del panel y los registros vean al visitante y no al proxy. Si la cabecera falta o no es
    /// una IP válida, se conserva la de la conexión. Ver <see cref="OpcionesSeguridad.CabeceraIpCliente"/>.
    /// </summary>
    public static IApplicationBuilder UseIpDeCabecera(this IApplicationBuilder app, string cabecera) =>
        app.Use((contexto, siguiente) =>
        {
            if (IPAddress.TryParse(contexto.Request.Headers[cabecera].ToString().Trim(), out var ip))
                contexto.Connection.RemoteIpAddress = ip;
            return siguiente(contexto);
        });
}
