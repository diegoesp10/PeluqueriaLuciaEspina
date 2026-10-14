using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>El indicador de carga se sirve antes que el resto de scripts, en la web pública y en el panel.</summary>
public sealed class CargandoWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task La_web_publica_y_el_panel_cargan_el_indicador_antes_que_el_resto_de_scripts()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var publico = fabrica.Cliente();
        foreach (var ruta in new[] { "/", "/en/citas", "/admin/acceso" })
            ComprobarOrden(await publico.GetStringAsync(ruta), ruta);

        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var panel = await admin.GetStringAsync("/admin/visitas");
        ComprobarOrden(panel, "/admin/visitas");
        Assert.Contains("js/sesion.js", panel);
        Assert.Contains("data-sin-cargando", panel); // la descarga de Excel no cambia de página
    }

    private static void ComprobarOrden(string html, string ruta)
    {
        var cargando = html.IndexOf("/js/cargando.js", StringComparison.Ordinal);
        var app = html.IndexOf("/js/app.js", StringComparison.Ordinal);
        Assert.True(cargando > 0, $"{ruta} no carga cargando.js");
        Assert.True(cargando < app, $"{ruta} carga cargando.js después de app.js");
    }

    private async Task EntrarAsync(HttpClient cliente)
    {
        var recuperacion = await fixture.RecuperacionAsync("prueba");
        await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Usuario"] = "prueba", ["Contrasena"] = SeguridadWebFixture.ClavePrueba, ["__RequestVerificationToken"] = await TokenAsync(cliente, "/admin/acceso") }));
        var r = await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Codigo"] = recuperacion, ["__RequestVerificationToken"] = await TokenAsync(cliente, "/admin/acceso/verificar") }));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }

    private static async Task<string> TokenAsync(HttpClient cliente, string ruta)
    {
        var pagina = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var campo = Regex.Match(await pagina.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(campo.Success);
        return WebUtility.HtmlDecode(campo.Groups[1].Value);
    }
}
