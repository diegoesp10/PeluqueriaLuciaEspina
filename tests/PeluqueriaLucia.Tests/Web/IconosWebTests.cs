using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// Los iconos van como SVG en línea (tag helper «icono»), nunca como caracteres de texto (↗ → ← ✳ ★ ✓), que cada
/// sistema pinta con una fuente distinta.
/// </summary>
public sealed class IconosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static readonly Regex Simbolos = new("[↗→←✳✂★☆✓✔▾]");

    [Fact]
    public async Task Ninguna_pagina_usa_caracteres_de_texto_como_iconos()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var publico = fabrica.Cliente();
        foreach (var ruta in new[] { "/", "/servicios", "/citas", "/opiniones", "/privacidad", "/en/", "/en/servicios", "/en/citas", "/admin/acceso", "/error/404" })
            Comprobar(ruta, await (await publico.GetAsync(ruta)).Content.ReadAsStringAsync()); // la página de error responde 404 a propósito

        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        foreach (var ruta in new[] { "/admin", "/admin/agenda", "/admin/comunicaciones", "/admin/visitas", "/admin/clientes", "/admin/catalogo", "/admin/tratamientos", "/admin/resenas", "/admin/seguridad", "/admin/productos", "/admin/productos/nuevo" })
            Comprobar(ruta, await admin.GetStringAsync(ruta));

        var portada = await publico.GetStringAsync("/");
        Assert.Contains("<svg", portada);
        Assert.Contains("M7 7h10v10", portada); // flecha-salir
        Assert.Contains("M12 5v14", portada); // asterisco
    }

    private static void Comprobar(string ruta, string html)
    {
        var sinScripts = Regex.Replace(html, "<script[^>]*>.*?</script>", "", RegexOptions.Singleline);
        var resto = Simbolos.Match(sinScripts);
        Assert.False(resto.Success, $"{ruta} sigue usando el símbolo '{resto.Value}' como icono: …{sinScripts[Math.Max(0, resto.Index - 60)..Math.Min(sinScripts.Length, resto.Index + 40)]}…");
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
