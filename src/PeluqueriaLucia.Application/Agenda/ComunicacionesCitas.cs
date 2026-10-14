using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public sealed class ComunicacionesCitas(IRepositorioAgenda datos, IProtectorSecretos secretos, IEnviadorCorreo correo,
    IWhatsAppManual whatsApp, IUrlPublica urlPublica, TimeProvider reloj, IAjustesComunicaciones ajustes) : IComunicacionesCitas
{
    public const int LongitudTexto = 2000;
    private DateTime Utc => reloj.GetUtcNow().UtcDateTime;
    public bool CorreoConfigurado => correo.Configurado;
    public bool EnlaceConfigurado => urlPublica.Obtener() is not null;
    public bool EnvioAutomatico => ajustes.EnvioAutomatico;
    private static bool Editable(MensajeCita m) => m.Estado is EstadoNotificacion.Pendiente or EstadoNotificacion.Error;
    internal static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static bool TokenValido(string? token) => token is { Length: 64 } && Regex.IsMatch(token, "^[A-F0-9]{64}$");
    private async Task<ReservaCita?> PropuestaAsync(string token, CancellationToken ct)
    {
        if (!TokenValido(token)) return null;
        var hash = Hash(token);
        return await datos.BuscarAsync<ReservaCita>(x => x.ConfirmacionHash == hash && x.Estado == EstadoCita.PendienteAceptacion && x.CaducaUtc > Utc, ct);
    }
    public async Task<PropuestaCita?> ConsultarPropuestaAsync(string token, CancellationToken ct)
    {
        var r = await PropuestaAsync(token, ct);
        return r is null ? null : new(r.Inicio, r.Fin, r.ServicioNombre, r.CaducaUtc);
    }
    public Task<Resultado> AceptarAsync(string token, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var r = await PropuestaAsync(token, ct);
        if (r is null) return new Error("El enlace ha caducado, ya se ha utilizado o la propuesta ha cambiado. Contacta con el equipo.");
        var ahora = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), AgendaService.Zona).DateTime;
        if (r.Inicio <= ahora) return new Error("La hora de esta propuesta ya ha pasado. Contacta con el equipo.");
        if (await datos.ExisteAsync<ReservaCita>(x => x.Id != r.Id && x.Inicio < r.Fin && x.Fin > r.Inicio &&
            (x.Estado == EstadoCita.Confirmada || ((x.Estado == EstadoCita.Pendiente || x.Estado == EstadoCita.PendienteAceptacion) && x.CaducaUtc > Utc)), ct))
            return new Error("Este horario ya no está disponible. Contacta con el equipo.");
        await ClientesAgendaService.VincularAsync(datos, r, Utc, ct);
        r.Estado = EstadoCita.Confirmada; r.AceptadaUtc = Utc; r.ActualizadaUtc = Utc; r.ConfirmacionHash = null;
        // Una propuesta aceptada no debe volver a enviarse desde una cola que aún no se haya procesado.
        foreach (var m in await datos.ListarAsync<MensajeCita>(x => x.ReservaId == r.Id && x.Estado == EstadoNotificacion.Pendiente, ct)) m.Estado = EstadoNotificacion.Obsoleto;
        return Resultado.Ok();
    }, ct);

    private async Task<string?> TextoAsync(MensajeCita m, CancellationToken ct)
    {
        if (m.TokenProtegido is null) return m.Texto;
        var token = secretos.Leer(m.TokenProtegido, "confirmacion-cita");
        if (token is null || await PropuestaAsync(token, ct) is null || urlPublica.Obtener() is not { } url) return null;
        return m.Texto + "\n\n" + new Uri(url, "citas/confirmar/" + token).AbsoluteUri;
    }
    public async Task<IReadOnlyList<ComunicacionCita>> ListarAsync(CancellationToken ct)
    {
        var lista = new List<ComunicacionCita>();
        foreach (var m in await datos.UltimosMensajesAsync(ct))
        {
            var texto = m.Estado == EstadoNotificacion.Obsoleto ? null : await TextoAsync(m, ct);
            var enlace = texto is not null && m.Canal == CanalConfirmacion.WhatsApp && m.Estado == EstadoNotificacion.Pendiente
                ? whatsApp.Preparar(m.Destinatario, texto) : null;
            lista.Add(new(m.Id, m.ReservaId, m.Destinatario, m.Canal, m.Estado, texto ?? m.Texto, enlace, m.CreadoUtc, Editable(m), m.Texto));
        }
        return lista;
    }
    public Task<Resultado> ActualizarTextoAsync(Guid id, string texto, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length is < 10 or > LongitudTexto) return new Error($"El texto del aviso debe tener entre 10 y {LongitudTexto} caracteres.", "Texto");
        var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, ct);
        if (m is null || !Editable(m)) return new Error("Este aviso ya se ha enviado o gestionado y no se puede cambiar.");
        m.Texto = limpio;
        return Resultado.Ok();
    }, ct);

    /// <summary>Correo listo para salir: texto plano con el enlace al final y versión HTML con botón.</summary>
    private static CorreoSaliente CorreoDe(MensajeCita m, string texto)
    {
        var boton = m.TokenProtegido is null ? "" : "<p><a href=\"" + WebUtility.HtmlEncode(texto[(texto.LastIndexOf("\n\n", StringComparison.Ordinal) + 2)..]) + "\" style=\"display:inline-block;padding:14px 24px;border-radius:12px;background:#183a35;color:#fff;text-decoration:none\">Revisar y aceptar cita</a></p>";
        var parrafos = string.Concat(m.Texto.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(p => "<p>" + WebUtility.HtmlEncode(p.Trim()) + "</p>"));
        return new CorreoSaliente(m.Destinatario, "Tu cita en el salón", texto,
            "<html lang=\"es\"><body style=\"background:#f4f1e9;color:#183a35;font-family:'Source Serif 4',Georgia,serif;padding:24px\"><h1>Tu próxima cita</h1>" + parrafos + boton + "<p>El equipo del salón</p></body></html>");
    }

    public async Task<Resultado> EnviarCorreoAsync(Guid id, string? texto, CancellationToken ct)
    {
        if (!correo.Configurado) return new Error("El correo no está configurado todavía. Revisa la configuración SMTP del servidor.");
        var preparado = await datos.ExclusivoAsync<Resultado<CorreoSaliente>>(async () =>
        {
            var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, ct);
            if (m is null || m.Canal != CanalConfirmacion.Correo || !Editable(m)) return new Error("Este correo ya se ha enviado o gestionado.");
            if (texto is not null)
            {
                var limpio = texto.Trim();
                if (limpio.Length is < 10 or > LongitudTexto) return new Error($"El texto del aviso debe tener entre 10 y {LongitudTexto} caracteres.", "Texto");
                m.Texto = limpio;
            }
            var completo = await TextoAsync(m, ct);
            if (completo is null)
            {
                if (EnlaceConfigurado) m.Estado = EstadoNotificacion.Obsoleto;
                return new Error(EnlaceConfigurado ? "La propuesta ha caducado o el cliente ya la ha aceptado: el aviso queda obsoleto." : "Falta configurar la URL pública para generar el enlace de aceptación.");
            }
            m.Estado = EstadoNotificacion.Enviando; m.ProcesadoUtc = Utc;
            return Resultado.Ok(CorreoDe(m, completo));
        }, ct);
        if (!preparado.Exito) return preparado.Error!;
        var resultado = await EnviarYAnotarAsync(id, preparado.Valor!, ct);
        return resultado switch
        {
            ResultadoEnvio.Aceptado => Resultado.Ok(),
            ResultadoEnvio.Rechazado => new Error("El servidor de correo ha rechazado el envío. Revisa la dirección o la configuración SMTP y vuelve a intentarlo."),
            _ => new Error("No se sabe si el correo llegó a salir. Compruébalo antes de volver a enviarlo para no duplicarlo."),
        };
    }

    private async Task<ResultadoEnvio> EnviarYAnotarAsync(Guid id, CorreoSaliente mensaje, CancellationToken ct)
    {
        ResultadoEnvio resultado;
        try { resultado = await correo.EnviarAsync(mensaje, ct); }
        catch (Exception) { resultado = ResultadoEnvio.Incierto; }
        await datos.ExclusivoAsync(async () =>
        {
            var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, CancellationToken.None);
            if (m is { Estado: EstadoNotificacion.Enviando })
            { m.Estado = resultado switch { ResultadoEnvio.Aceptado => EstadoNotificacion.AceptadoServidor, ResultadoEnvio.Rechazado => EstadoNotificacion.Error, _ => EstadoNotificacion.Incierto }; m.ProcesadoUtc = Utc; }
            return true;
        }, CancellationToken.None);
        return resultado;
    }
    public Task<Resultado> MarcarWhatsAppAsync(Guid id, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, ct);
        if (m is null || m.Canal != CanalConfirmacion.WhatsApp || m.Estado != EstadoNotificacion.Pendiente || await TextoAsync(m, ct) is null)
            return new Error("El mensaje ya se ha gestionado, la propuesta ha caducado o falta configurar el enlace público.");
        m.Estado = EstadoNotificacion.Manual; m.ProcesadoUtc = Utc;
        return Resultado.Ok();
    }, ct);
    public Task<Resultado> ReintentarCorreoAsync(Guid id, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, ct);
        if (m is null || m.Canal != CanalConfirmacion.Correo || m.Estado != EstadoNotificacion.Error || await TextoAsync(m, ct) is null)
            return new Error("Solo se puede reintentar un correo rechazado de una propuesta vigente. Los envíos inciertos requieren revisión.");
        m.Estado = EstadoNotificacion.Pendiente; m.ProcesadoUtc = null;
        return Resultado.Ok();
    }, ct);

    public async Task ProcesarColaAsync(CancellationToken ct)
    {
        // Recuperar envíos interrumpidos sin repetir un correo cuyo resultado se desconoce.
        await datos.ExclusivoAsync(async () =>
        {
            foreach (var m in await datos.ListarAsync<MensajeCita>(x => x.Estado == EstadoNotificacion.Enviando && x.ProcesadoUtc < Utc.AddMinutes(-10), ct)) m.Estado = EstadoNotificacion.Incierto;
            return true;
        }, ct);
        // Sin envío automático, los correos esperan en Comunicaciones a que el salón los revise y los envíe
        if (!correo.Configurado || !ajustes.EnvioAutomatico) return;
        var pendientes = (await datos.ListarAsync<MensajeCita>(x => x.Canal == CanalConfirmacion.Correo && x.Estado == EstadoNotificacion.Pendiente, ct)).OrderBy(x => x.CreadoUtc).Take(10).Select(x => x.Id).ToArray();
        foreach (var id in pendientes)
        {
            var mensaje = await datos.ExclusivoAsync(async () =>
            {
                var m = await datos.BuscarAsync<MensajeCita>(x => x.Id == id, ct);
                if (m is null || m.Estado != EstadoNotificacion.Pendiente) return null;
                var texto = await TextoAsync(m, ct);
                if (texto is null)
                {
                    if (EnlaceConfigurado) m.Estado = EstadoNotificacion.Obsoleto;
                    return null;
                }
                m.Estado = EstadoNotificacion.Enviando; m.ProcesadoUtc = Utc;
                return CorreoDe(m, texto);
            }, ct);
            if (mensaje is null) continue;
            await EnviarYAnotarAsync(id, mensaje, ct);
            ct.ThrowIfCancellationRequested();
        }
    }
}
