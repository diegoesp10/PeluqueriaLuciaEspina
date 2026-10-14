using System.Text.Json;
using Comunicaciones.Correo;
using PeluqueriaLucia.Application.Agenda;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

/// <summary>
/// El anfitrión obtiene el secreto SMTP (JSON de <see cref="ConfiguracionSmtp"/>); la biblioteca SMTP no sabe de dónde
/// sale. En Azure, de la variable PELUQUERIA_CORREO_SMTP, que en la Web App es una referencia a un secreto de Key Vault
/// («@Microsoft.KeyVault(SecretUri=…)»); en el IIS local, del almacén de credenciales de Windows.
/// </summary>
public sealed class ConfiguracionCorreo : IConfiguracionSmtp
{
    public const string Destino = "PeluqueriaLucia/CorreoSmtp";
    public const string VariableEntorno = "PELUQUERIA_CORREO_SMTP";
    public ConfiguracionSmtp? Obtener()
    {
        if (Environment.GetEnvironmentVariable("PELUQUERIA_CORREO_ACTIVO") != "true") return null;
        try
        {
            var secreto = Environment.GetEnvironmentVariable(VariableEntorno) is { Length: > 0 } variable ? variable : ServicioConexionSql.LeerCredencial(Destino);
            return secreto is null ? null : JsonSerializer.Deserialize<ConfiguracionSmtp>(secreto);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }
}
/// <summary>Por defecto los correos de citas se revisan y envían a mano desde Comunicaciones; PELUQUERIA_CORREO_AUTOMATICO=true los envía la cola.</summary>
internal sealed class AjustesComunicaciones : IAjustesComunicaciones
{
    public bool EnvioAutomatico => Environment.GetEnvironmentVariable("PELUQUERIA_CORREO_AUTOMATICO") == "true";
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
