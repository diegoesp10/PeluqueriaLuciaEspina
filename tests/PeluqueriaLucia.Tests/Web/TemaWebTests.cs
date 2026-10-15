using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Toda la aplicación (web pública, acceso y panel) comparte la paleta morada de tokens.css, sin hojas de tema aparte.</summary>
public sealed class TemaWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task La_web_el_acceso_y_el_panel_usan_la_paleta_morada()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var publico = fabrica.Cliente();
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        foreach (var (ruta, html) in new[]
        {
            ("/", await publico.GetStringAsync("/")), ("/en/", await publico.GetStringAsync("/en/")),
            ("/admin/acceso", await publico.GetStringAsync("/admin/acceso")), ("/admin", await admin.GetStringAsync("/admin")),
        })
        {
            Assert.True(html.Contains("/css/tokens.css", StringComparison.Ordinal), $"{ruta} no carga tokens.css");
            Assert.DoesNotContain("panel-tema.css", html);
            Assert.Contains("name=\"theme-color\" media=\"(prefers-color-scheme: light)\" content=\"#5c2d91\"", html);
            Assert.Contains("name=\"theme-color\" media=\"(prefers-color-scheme: dark)\" content=\"#191424\"", html);
        }

        var tokens = await publico.GetStringAsync("/css/tokens.css");
        Assert.Contains("--accent: light-dark(#5c2d91, #c9b6ef)", tokens);
        Assert.Contains("--forest: #3b1f63", tokens);
        Assert.DoesNotContain("#183a35", tokens);
        Assert.Equal(HttpStatusCode.NotFound, (await publico.GetAsync("/css/panel-tema.css")).StatusCode);
        Assert.Contains("\"theme_color\": \"#5c2d91\"", await publico.GetStringAsync("/site.webmanifest"));
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
