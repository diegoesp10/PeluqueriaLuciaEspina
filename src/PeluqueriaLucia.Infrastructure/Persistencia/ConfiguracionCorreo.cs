using System.Text.Json;
using Comunicaciones.Correo;
using PeluqueriaLucia.Application.Agenda;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>El anfitrión obtiene secretos del almacén de Windows; la biblioteca SMTP no conoce ese almacén.</summary>
public sealed class ConfiguracionCorreo : IConfiguracionSmtp
{
    public const string Destino = "PeluqueriaLucia/CorreoSmtp";
    public ConfiguracionSmtp? Obtener()
    {
        if (Environment.GetEnvironmentVariable("PELUQUERIA_CORREO_ACTIVO") != "true") return null;
        try
        {
            var secreto = ServicioConexionSql.LeerCredencial(Destino);
            return secreto is null ? null : JsonSerializer.Deserialize<ConfiguracionSmtp>(secreto);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }
}
internal sealed class UrlPublica : IUrlPublica
{
    public Uri? Obtener()
    {
        var valor = Environment.GetEnvironmentVariable("PELUQUERIA_URL_PUBLICA");
        return Uri.TryCreate(valor, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
            uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && string.IsNullOrEmpty(uri.UserInfo) ? uri : null;
    }
}
