using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Comunicaciones.Correo;

/// <summary>En el JSON de configuración se admite como texto («StartTls», «Tls») o como número (0, 1).</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<SeguridadSmtp>))]
public enum SeguridadSmtp { StartTls, Tls }
public sealed record ConfiguracionSmtp(string Servidor, int Puerto, SeguridadSmtp Seguridad, string Usuario,
    string Contrasena, string Remitente, string NombreRemitente);
/// <summary>Imagen que va dentro del correo y se cita en el HTML como «cid:Id» (el logo): se ve aunque el lector bloquee las imágenes externas.</summary>
public sealed record ImagenIncrustada(string Id, string NombreFichero, byte[] Datos, string TipoMime);
public sealed record CorreoSaliente(string Destinatario, string Asunto, string Texto, string? Html = null, IReadOnlyList<ImagenIncrustada>? Imagenes = null);
public enum ResultadoEnvio { Aceptado, Rechazado, Incierto }
public interface IConfiguracionSmtp { ConfiguracionSmtp? Obtener(); }
public interface IEnviadorCorreo
{
    bool Configurado { get; }
    Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct = default);
}

/// <summary>Transporte SMTP reutilizable. No conoce reservas, base de datos ni configuración del anfitrión.</summary>
public sealed class EnviadorCorreoSmtp(IConfiguracionSmtp configuracion) : IEnviadorCorreo
{
    /// <summary>Mensaje MIME: texto plano y HTML alternativos; las imágenes incrustadas van con su Content-Id.</summary>
    public static MimeMessage Construir(ConfiguracionSmtp c, CorreoSaliente correo, MailboxAddress destino)
    {
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress(c.NombreRemitente, c.Remitente)); mensaje.To.Add(destino);
        mensaje.Subject = correo.Asunto;
        var cuerpo = new BodyBuilder { TextBody = correo.Texto, HtmlBody = correo.Html };
        if (correo.Html is not null)
            foreach (var imagen in correo.Imagenes ?? [])
                cuerpo.LinkedResources.Add(imagen.NombreFichero, imagen.Datos, ContentType.Parse(imagen.TipoMime)).ContentId = imagen.Id;
        mensaje.Body = cuerpo.ToMessageBody();
        return mensaje;
    }

    public bool Configurado => configuracion.Obtener() is { } c && EsValida(c);
    public static bool EsValida(ConfiguracionSmtp c) => !string.IsNullOrWhiteSpace(c.Servidor) &&
        c.Servidor.Length <= 253 && !c.Servidor.Any(char.IsWhiteSpace) && c.Puerto is > 0 and <= 65535 &&
        Enum.IsDefined(c.Seguridad) && !string.IsNullOrWhiteSpace(c.Usuario) && !string.IsNullOrEmpty(c.Contrasena) &&
        MailboxAddress.TryParse(c.Remitente, out var m) && m.Address == c.Remitente;

    public async Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct = default)
    {
        var c = configuracion.Obtener();
        if (c is null || !EsValida(c)) return ResultadoEnvio.Rechazado;
        if (!MailboxAddress.TryParse(correo.Destinatario, out var destino) || destino.Address != correo.Destinatario ||
            correo.Asunto.Any(ch => ch is '\r' or '\n')) return ResultadoEnvio.Rechazado;
        var mensaje = Construir(c, correo, destino);
        using var cliente = new SmtpClient { Timeout = 20000 };
        var enviando = false;
        try
        {
            await cliente.ConnectAsync(c.Servidor, c.Puerto,
                c.Seguridad == SeguridadSmtp.Tls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            await cliente.AuthenticateAsync(c.Usuario, c.Contrasena, ct);
            enviando = true;
            await cliente.SendAsync(mensaje, ct);
            // El servidor ya ha aceptado el correo: un fallo al desconectar no debe provocar un reenvío.
            try { await cliente.DisconnectAsync(true, ct); } catch (Exception) { }
            return ResultadoEnvio.Aceptado;
        }
        catch (SmtpCommandException) { return ResultadoEnvio.Rechazado; }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or SmtpProtocolException or
            System.Net.Sockets.SocketException or System.Security.Authentication.AuthenticationException or
            MailKit.Security.AuthenticationException or NotSupportedException)
        { return enviando ? ResultadoEnvio.Incierto : ResultadoEnvio.Rechazado; }
    }
}
