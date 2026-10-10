using System.Net;

namespace PeluqueriaLucia.Web.Seguridad;

public static class IpCliente
{
    /// <summary>IP de quien hace la petición, en texto. Las IPv4 que llegan como IPv6 («::ffff:1.2.3.4») se normalizan.</summary>
    public static string De(HttpContext contexto) => Direccion(contexto)?.ToString() ?? "desconocida";

    public static IPAddress? Direccion(HttpContext contexto) =>
        contexto.Connection.RemoteIpAddress is { IsIPv4MappedToIPv6: true } mapeada ? mapeada.MapToIPv4() : contexto.Connection.RemoteIpAddress;
}
