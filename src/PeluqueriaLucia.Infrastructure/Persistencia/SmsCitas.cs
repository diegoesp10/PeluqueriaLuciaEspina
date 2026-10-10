using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

public sealed record CredencialSms(string Cuenta, string Token, string Numero, DateTime ConfiguradaUtc);
internal sealed class SmsCitas(PeluqueriaDbContext db, IHttpClientFactory clientes, TimeProvider reloj) : ISmsCitas
{
    public const string Credencial = "PeluqueriaLucia/TwilioSms";
    public bool Activo => Environment.GetEnvironmentVariable("PELUQUERIA_SMS_ACTIVO") == "true" && Obtener() is not null;
    private static CredencialSms? Obtener()
    {
        var valor = ServicioConexionSql.LeerCredencial(Credencial);
        return valor is null ? null : JsonSerializer.Deserialize<CredencialSms>(valor);
    }
    private static string? UrlPublica()
    {
        var valor = Environment.GetEnvironmentVariable("PELUQUERIA_SMS_URL_PUBLICA");
        return Uri.TryCreate(valor, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
    public bool ValidarFirma(string ruta, string firma, IReadOnlyDictionary<string, string> campos)
    {
        var credencial = Obtener(); var url = UrlPublica();
        if (credencial is null || url is null || firma.Length > 100) return false;
        var datos = url + ruta + string.Concat(campos.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key + x.Value));
        var hash = HMACSHA1.HashData(Encoding.UTF8.GetBytes(credencial.Token), Encoding.UTF8.GetBytes(datos));
        try { return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(firma)); }
        catch (FormatException) { return false; }
    }
    public async Task ProcesarWebhookAsync(IReadOnlyDictionary<string, string> campos, bool entrada, CancellationToken ct)
    {
        if (!campos.TryGetValue("MessageSid", out var sid) || !Regex.IsMatch(sid, "^SM[a-fA-F0-9]{32}$")) return;
        if (entrada)
        {
            if (!campos.TryGetValue("From", out var telefono) || AgendaService.NormalizarTelefono(telefono) is not { } movil || !campos.TryGetValue("Body", out var texto)) return;
            if (await db.SmsEntrantes.AnyAsync(x => x.ProveedorId == sid, ct)) return;
            db.SmsEntrantes.Add(new SmsEntrante { Id = Guid.NewGuid(), ProveedorId = sid, Telefono = movil, Texto = texto[..Math.Min(500, texto.Length)], RecibidoUtc = reloj.GetUtcNow().UtcDateTime });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { db.ChangeTracker.Clear(); }
        }
        else if (campos.TryGetValue("MessageStatus", out var estado))
        {
            var nuevo = estado switch { "delivered" => EstadoSms.Entregado, "failed" or "undelivered" => EstadoSms.Error, _ => (EstadoSms?)null };
            if (nuevo is not null) await db.MensajesCitas.Where(x => x.ProveedorId == sid && x.Estado != EstadoSms.Entregado)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, nuevo.Value), ct);
        }
    }
    public async Task<IReadOnlyList<SmsEntrante>> EntrantesAsync(CancellationToken ct) => await db.SmsEntrantes.AsNoTracking().OrderByDescending(x => x.RecibidoUtc).Take(100).ToListAsync(ct);

    public async Task ProcesarColaAsync(CancellationToken ct)
    {
        if (!Activo || Obtener() is not { } c || UrlPublica() is not { } url) return;
        var utc = reloj.GetUtcNow().UtcDateTime;
        // Caída después de un envío: no repetir automáticamente un SMS cuyo resultado se desconoce.
        await db.MensajesCitas.Where(x => x.Estado == EstadoSms.Enviando && x.ProcesadoUtc < utc.AddMinutes(-10))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoSms.Incierto), ct);
        var pendientes = await db.MensajesCitas.AsNoTracking().Where(x => x.Estado == EstadoSms.Pendiente && x.CreadoUtc >= c.ConfiguradaUtc)
            .OrderBy(x => x.CreadoUtc).Take(10).ToListAsync(ct);
        foreach (var m in pendientes)
        {
            if (await db.MensajesCitas.Where(x => x.Id == m.Id && x.Estado == EstadoSms.Pendiente)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoSms.Enviando).SetProperty(x => x.ProcesadoUtc, utc), ct) != 1) continue;
            // STOP/BAJA impide nuevos SMS. La comunicación se gestiona telefónicamente.
            var ultimo = await db.SmsEntrantes.Where(x => x.Telefono == m.Telefono && (x.Texto == "STOP" || x.Texto == "BAJA" || x.Texto == "START" || x.Texto == "ALTA"))
                .OrderByDescending(x => x.RecibidoUtc).Select(x => x.Texto).FirstOrDefaultAsync(ct);
            if (ultimo?.Trim().ToUpperInvariant() is "STOP" or "BAJA")
            { await EstadoAsync(m.Id, EstadoSms.Error, null, ct); continue; }
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.twilio.com/2010-04-01/Accounts/{c.Cuenta}/Messages.json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(c.Cuenta + ":" + c.Token)));
                request.Content = new FormUrlEncodedContent(new Dictionary<string,string> { ["From"] = c.Numero, ["To"] = m.Telefono,
                    ["Body"] = m.Texto, ["StatusCallback"] = url + "/sms/estado" });
                using var respuesta = await clientes.CreateClient("sms-citas").SendAsync(request, ct);
                if (!respuesta.IsSuccessStatusCode) { await EstadoAsync(m.Id, EstadoSms.Error, null, ct); continue; }
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
                var sid = json.RootElement.GetProperty("sid").GetString();
                if (sid is null || !Regex.IsMatch(sid, "^SM[a-fA-F0-9]{32}$")) { await EstadoAsync(m.Id, EstadoSms.Incierto, null, ct); continue; }
                await EstadoAsync(m.Id, EstadoSms.AceptadoProveedor, sid, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            { await EstadoAsync(m.Id, EstadoSms.Incierto, null, CancellationToken.None); }
        }
    }
    private Task<int> EstadoAsync(Guid id, EstadoSms estado, string? sid, CancellationToken ct) => db.MensajesCitas.Where(x => x.Id == id)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, estado).SetProperty(x => x.ProveedorId, sid), ct);
}
