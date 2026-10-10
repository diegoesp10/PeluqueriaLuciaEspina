using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

public sealed class AgendaWebTests
{
    [Fact]
    public async Task Publico_puede_solicitar_sin_ver_telefonos_y_admin_requiere_sesion()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var fabrica=new FabricaWeb(bd.Cadena); using var cliente=fabrica.Cliente();
        var ahora=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"));
        var dia=DateOnly.FromDateTime(ahora.DateTime).AddDays(2); while(dia.DayOfWeek==DayOfWeek.Sunday) dia=dia.AddDays(1);
        var html=await cliente.GetStringAsync("/citas?dia="+dia.ToString("yyyy-MM-dd"));
        Assert.DoesNotMatch("(?i)\\bstyle\\s*=", html);
        Assert.Contains("<li hidden>", html);
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var id=Regex.Match(html,"name=\"Formulario.SolicitudId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var hora=Regex.Match(html,"name=\"Formulario.Hora\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token); Assert.NotEmpty(id); Assert.NotEmpty(hora);
        var invalida = await cliente.PostAsync("/citas", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token, ["Formulario.SolicitudId"] = id,
            ["Formulario.Dia"] = dia.ToString("yyyy-MM-dd"), ["Formulario.Hora"] = hora,
            ["Formulario.Telefono"] = "incorrecto", ["Formulario.Consentimiento"] = "true"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var errores = await invalida.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", errores);
        Assert.DoesNotMatch("(?i)\\bstyle\\s*=", errores);
        Assert.Contains("style-src 'self'", invalida.Headers.GetValues("Content-Security-Policy").Single());
        var r=await cliente.PostAsync("/citas",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=token,["Formulario.SolicitudId"]=id,["Formulario.Dia"]=dia.ToString("yyyy-MM-dd"),["Formulario.Hora"]=hora,["Formulario.Telefono"]="612345678",["Formulario.Consentimiento"]="true"}));
        Assert.Equal(HttpStatusCode.Redirect,r.StatusCode); Assert.Equal("/citas/recibida",r.Headers.Location!.ToString());
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas"));
        var disponibilidad=await cliente.GetStringAsync("/citas/disponibilidad?dia="+dia.ToString("yyyy-MM-dd"));
        Assert.DoesNotContain("612345678",disponibilidad);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin/agenda")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin/agenda/avisos")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/citas",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await cliente.PostAsync("/sms/entrada",new FormUrlEncodedContent(new Dictionary<string,string>{["MessageSid"]="SM00000000000000000000000000000000"}))).StatusCode);
    }
}
