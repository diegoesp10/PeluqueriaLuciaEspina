using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Application.Abstracciones;

namespace PeluqueriaLucia.Tests.Web;

public sealed class MfaWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static async Task<string> Token(HttpClient cliente, string ruta)
    {
        var pagina = await cliente.GetAsync(ruta); Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var html = await pagina.Content.ReadAsStringAsync();
        return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    }
    private static async Task<HttpResponseMessage> Password(HttpClient cliente, string usuario)
    {
        var token = await Token(cliente, "/admin/acceso");
        return await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string,string> { ["Usuario"] = usuario, ["Contrasena"] = SeguridadWebFixture.ClavePrueba, ["__RequestVerificationToken"] = token }));
    }
    private static async Task<HttpResponseMessage> Codigo(HttpClient cliente, string codigo)
    {
        var token = await Token(cliente, "/admin/acceso/verificar");
        return await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string,string> { ["Codigo"] = codigo, ["__RequestVerificationToken"] = token }));
    }
    private async Task Crear(string usuario)
    {
        await using var scope = fixture.Fabrica.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<AccesoService>().EstablecerAdministradorAsync(usuario, SeguridadWebFixture.ClavePrueba)).Exito);
    }

    [Fact]
    public async Task Password_sola_y_preautenticacion_no_abren_el_panel_y_el_QR_solo_se_muestra_tras_ella()
    {
        await Crear("alta-mfa");
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var cliente = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/admin/acceso/verificar")).StatusCode);
        var password = await Password(cliente, "alta-mfa");
        Assert.Equal("/admin/acceso/verificar", password.Headers.Location!.ToString());
        var cookies = password.Headers.GetValues("Set-Cookie");
        Assert.DoesNotContain(cookies, c => c.StartsWith("PeluqueriaLucia.Panel="));
        Assert.Contains(cookies, c => c.StartsWith("PeluqueriaLucia.Verificacion=") && c.Contains("httponly") && c.Contains("samesite=strict"));
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/admin")).StatusCode);
        var html = await cliente.GetStringAsync("/admin/acceso/verificar");
        Assert.Contains("data:image/png;base64,", html);
        Assert.Contains("Microsoft Authenticator", html);
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string,string> { ["Codigo"] = "123456" }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Codigo(cliente, "abcdef")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Alta_confirmada_cifra_el_secreto_y_codigos_de_reserva_se_consumen_una_sola_vez()
    {
        await Crear("reserva-mfa");
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var cliente = fabrica.Cliente();
        await Password(cliente, "reserva-mfa");
        var html = await cliente.GetStringAsync("/admin/acceso/verificar");
        var secreto = Regex.Match(html, "<code>([A-Z2-7]+)</code>").Groups[1].Value;
        Assert.NotEmpty(secreto);
        var alta = await Codigo(cliente, new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secreto)).ComputeTotp());
        Assert.Equal(HttpStatusCode.OK, alta.StatusCode);
        var reservas = Regex.Matches(await alta.Content.ReadAsStringAsync(), "<code>([A-F0-9-]{39})</code>").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(10, reservas.Length);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/admin")).StatusCode);
        await using var db = fixture.Bd.Contexto();
        var admin = await db.Administradores.SingleAsync(a => a.Usuario == "reserva-mfa");
        Assert.NotEqual(secreto, admin.SecretoMfaProtegido);
        Assert.All(await db.RecuperacionesMfa.Where(x => x.AdministradorId == admin.Id).ToListAsync(), x => { Assert.Equal(64,x.Hash.Length); Assert.DoesNotContain(x.Hash,reservas); });
        using var reserva = fabrica.Cliente();
        await Password(reserva, "reserva-mfa");
        Assert.Equal(HttpStatusCode.Redirect, (await Codigo(reserva, reservas[0])).StatusCode);
        using var repetido = fabrica.Cliente();
        await Password(repetido, "reserva-mfa");
        Assert.Equal(HttpStatusCode.BadRequest, (await Codigo(repetido, reservas[0])).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await repetido.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Totp_aceptado_no_se_reutiliza_en_otro_desafio()
    {
        await Crear("replay-mfa"); await fixture.RecuperacionAsync("replay-mfa");
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var primero = fabrica.Cliente(); using var segundo = fabrica.Cliente();
        await Password(primero, "replay-mfa"); await Password(segundo, "replay-mfa");
        var codigo = SeguridadWebFixture.Codigo();
        Assert.Equal(HttpStatusCode.Redirect, (await Codigo(primero, codigo)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Codigo(segundo, codigo)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await segundo.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Cinco_fallos_mfa_bloquean_la_cuenta_y_el_desafio()
    {
        await Crear("bloqueo-mfa"); await fixture.RecuperacionAsync("bloqueo-mfa");
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena); using var cliente = fabrica.Cliente();
        await Password(cliente, "bloqueo-mfa");
        for (var i=0;i<4;i++) Assert.Equal(HttpStatusCode.BadRequest,(await Codigo(cliente,"abcdef")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Codigo(cliente,"abcdef")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Password(cliente,"bloqueo-mfa")).StatusCode);
    }

    private sealed class Reloj : TimeProvider { public DateTimeOffset Ahora = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Ahora; }
    [Fact]
    public async Task Confirmar_un_QR_invalida_los_otros_QR_pendientes_aunque_compartan_instante()
    {
        await Crear("qr-pendiente");
        var reloj = new Reloj(); using var fabrica = new FabricaWeb(fixture.Bd.Cadena,reloj:reloj);
        using var primero = fabrica.Cliente(); using var segundo = fabrica.Cliente();
        await Password(primero,"qr-pendiente"); await Password(segundo,"qr-pendiente");
        var secreto = Regex.Match(await primero.GetStringAsync("/admin/acceso/verificar"),"<code>([A-Z2-7]+)</code>").Groups[1].Value;
        var codigo = new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secreto)).ComputeTotp(reloj.Ahora.UtcDateTime);
        Assert.Equal(HttpStatusCode.OK,(await Codigo(primero,codigo)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await segundo.GetAsync("/admin/acceso/verificar")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await segundo.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Cambiar_password_exige_codigo_fresco_y_no_modifica_la_clave_si_falta_o_se_repite()
    {
        await Crear("cambio-con-mfa"); await fixture.RecuperacionAsync("cambio-con-mfa");
        var reloj = new Reloj(); using var fabrica = new FabricaWeb(fixture.Bd.Cadena,reloj:reloj); using var cliente = fabrica.Cliente();
        await Password(cliente,"cambio-con-mfa");
        var codigo = SeguridadWebFixture.Codigo(reloj.Ahora.UtcDateTime);
        Assert.Equal(HttpStatusCode.Redirect,(await Codigo(cliente,codigo)).StatusCode);
        var campos = new Dictionary<string,string> { ["Actual"]=SeguridadWebFixture.ClavePrueba,["Nueva"]="Otra password segura y larga 2026!",["Confirmacion"]="Otra password segura y larga 2026!" };
        foreach (var valor in new[] { "", codigo }) {
            campos["Codigo"]=valor; campos["__RequestVerificationToken"]=await Token(cliente,"/admin/Seguridad/Contrasena");
            Assert.Equal(HttpStatusCode.OK,(await cliente.PostAsync("/admin/Seguridad/Contrasena",new FormUrlEncodedContent(campos))).StatusCode);
        }
        reloj.Ahora += TimeSpan.FromSeconds(60);
        campos["Codigo"]=SeguridadWebFixture.Codigo(reloj.Ahora.UtcDateTime); campos["__RequestVerificationToken"]=await Token(cliente,"/admin/Seguridad/Contrasena");
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.PostAsync("/admin/Seguridad/Contrasena",new FormUrlEncodedContent(campos))).StatusCode);
    }
    [Fact]
    public async Task Desafio_caducado_y_cookie_copiada_sin_token_no_conceden_acceso()
    {
        await Crear("caduca-mfa"); await fixture.RecuperacionAsync("caduca-mfa");
        var reloj = new Reloj(); using var fabrica = new FabricaWeb(fixture.Bd.Cadena,reloj:reloj); using var cliente = fabrica.Cliente();
        await Password(cliente,"caduca-mfa");
        reloj.Ahora += TimeSpan.FromMinutes(5);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin/acceso/verificar")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Vincular_otro_movil_mantiene_el_anterior_hasta_confirmar_y_revoca_sesiones_y_reservas_antiguas()
    {
        await Crear("otro-movil");
        var reserva1=await fixture.RecuperacionAsync("otro-movil"); var reserva2=await fixture.RecuperacionAsync("otro-movil"); var reservaSinUsar=await fixture.RecuperacionAsync("otro-movil");
        using var fabrica=new FabricaWeb(fixture.Bd.Cadena); using var cliente=fabrica.Cliente(); using var otraSesion=fabrica.Cliente();
        await Password(cliente,"otro-movil"); await Codigo(cliente,reserva1);
        await Password(otraSesion,"otro-movil"); await Codigo(otraSesion,reserva2);
        await using var db=fixture.Bd.Contexto(); var anterior=await db.Administradores.SingleAsync(x=>x.Usuario=="otro-movil"); var protegido=anterior.SecretoMfaProtegido;
        var cambio=await cliente.PostAsync("/admin/Seguridad/Authenticator",new FormUrlEncodedContent(new Dictionary<string,string> { ["Contrasena"]=SeguridadWebFixture.ClavePrueba,["Codigo"]=reservaSinUsar,["__RequestVerificationToken"]=await Token(cliente,"/admin/Seguridad/Authenticator") }));
        Assert.Equal(HttpStatusCode.Redirect,cambio.StatusCode);
        db.ChangeTracker.Clear(); Assert.Equal(protegido,(await db.Administradores.FindAsync(anterior.Id))!.SecretoMfaProtegido);
        Assert.Equal(HttpStatusCode.OK,(await otraSesion.GetAsync("/admin")).StatusCode);
        var secreto=Regex.Match(await cliente.GetStringAsync("/admin/acceso/verificar"),"<code>([A-Z2-7]+)</code>").Groups[1].Value;
        Assert.NotEmpty(secreto);
        Assert.Equal(HttpStatusCode.OK,(await Codigo(cliente,new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secreto)).ComputeTotp())).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await otraSesion.GetAsync("/admin")).StatusCode);
        db.ChangeTracker.Clear(); Assert.NotEqual(protegido,(await db.Administradores.FindAsync(anterior.Id))!.SecretoMfaProtegido);
        Assert.Equal(10,await db.RecuperacionesMfa.CountAsync(x=>x.AdministradorId==anterior.Id && x.UsadoEn==null));
    }

    [Fact]
    public async Task Verificador_totp_coincide_con_vector_RFC_y_rechaza_codigos_fuera_de_ventana()
    {
        await using var scope = fixture.Fabrica.Services.CreateAsyncScope();
        var protector = scope.ServiceProvider.GetRequiredService<IProtectorSecretos>();
        var secreto = OtpNet.Base32Encoding.ToString(System.Text.Encoding.ASCII.GetBytes("12345678901234567890"));
        Assert.Equal(1L,protector.VerificarTotp(secreto,"287082",DateTime.UnixEpoch.AddSeconds(59)));
        Assert.Null(protector.VerificarTotp(secreto,"287082",DateTime.UnixEpoch.AddSeconds(180)));
        Assert.Null(protector.VerificarTotp(secreto,"abcdef",DateTime.UnixEpoch.AddSeconds(59)));
        Assert.Null(protector.Leer(protector.Proteger("privado","totp"),"ticket-resena"));
    }
}
