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

    /// <summary>
    /// El panel nunca corta un menú ni un texto: hasta 1180 px pasa al menú hamburguesa (sin barra de solo iconos), las filas
    /// bajan de línea en vez de acabar en puntos suspensivos, los controles segmentados bajan a otra fila si no caben y las
    /// ventanas siguen centradas.
    /// </summary>
    [Fact]
    public async Task El_panel_pasa_al_menu_hamburguesa_y_no_corta_textos()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var publico = fabrica.Cliente();
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);

        Assert.Contains("data-menu-abrir data-menu-hasta=\"1180px\"", await admin.GetStringAsync("/admin"));
        Assert.Contains("botonMenu.dataset.menuHasta ?? '900px'", await publico.GetStringAsync("/js/app.js"));

        var maqueta = await publico.GetStringAsync("/css/layout.css");
        Assert.Contains("@media (max-width: 1180px)", maqueta);
        Assert.DoesNotContain("max-width: 900px", maqueta);
        Assert.DoesNotContain("--sidebar-w: 84px", maqueta);
        Assert.Matches(@"\.main > dialog \{\s*margin: auto;", maqueta);

        var componentes = await publico.GetStringAsync("/css/componentes.css");
        var filas = Regex.Match(componentes, @"\.row-title \{[^}]*\}\s*\.row-sub \{[^}]*\}").Value;
        Assert.NotEmpty(filas);
        Assert.DoesNotContain("ellipsis", filas);
        Assert.DoesNotContain("nowrap", filas);
        Assert.Matches(@"\.segmented \{[^}]*flex-wrap: wrap;", componentes);

        var paginas = await publico.GetStringAsync("/css/paginas.css");
        Assert.DoesNotContain("min-width:901px", paginas);
        Assert.DoesNotContain(".nav-label { display:none; }", paginas);
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
