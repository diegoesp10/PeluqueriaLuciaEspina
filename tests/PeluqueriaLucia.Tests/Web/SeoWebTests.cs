using System.Net;
using System.Xml.Linq;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Lo que leen Google y Bing: robots.txt, sitemap.xml en los dos idiomas, canónicas y el panel sin indexar.</summary>
public sealed class SeoWebTests
{
    [Fact]
    public async Task Robots_y_sitemap_usan_la_url_publica_y_dejan_fuera_el_panel()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena, new() { ["PELUQUERIA_URL_PUBLICA"] = "https://luciapeluqueriaecologica.com/" });
        using var cliente = fabrica.Cliente();

        var robots = await cliente.GetAsync("/robots.txt");
        Assert.Equal(HttpStatusCode.OK, robots.StatusCode);
        Assert.StartsWith("text/plain", robots.Content.Headers.ContentType!.ToString());
        var texto = await robots.Content.ReadAsStringAsync();
        Assert.Contains("Disallow: /admin", texto);
        Assert.Contains("Disallow: /api/", texto);
        Assert.Contains("Sitemap: https://luciapeluqueriaecologica.com/sitemap.xml", texto);

        var sitemap = await cliente.GetAsync("/sitemap.xml");
        Assert.Equal(HttpStatusCode.OK, sitemap.StatusCode);
        Assert.StartsWith("application/xml", sitemap.Content.Headers.ContentType!.ToString());
        var xml = XDocument.Parse(await sitemap.Content.ReadAsStringAsync());
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var urls = xml.Root!.Elements(sm + "url").Select(u => u.Element(sm + "loc")!.Value).ToList();
        Assert.Contains("https://luciapeluqueriaecologica.com/", urls);
        Assert.Contains("https://luciapeluqueriaecologica.com/en/", urls);
        Assert.Contains("https://luciapeluqueriaecologica.com/servicios", urls);
        Assert.Contains("https://luciapeluqueriaecologica.com/en/servicios", urls);
        Assert.Contains("https://luciapeluqueriaecologica.com/en/opiniones", urls);
        Assert.DoesNotContain(urls, u => u.Contains("/admin") || u.Contains("/api/") || u.Contains("confirmar"));
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        Assert.All(xml.Root.Elements(sm + "url"), u => Assert.Equal(3, u.Elements(xhtml + "link").Count()));
    }

    [Fact]
    public async Task Sin_url_publica_configurada_el_sitemap_usa_la_de_la_peticion()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena);
        using var cliente = fabrica.Cliente();
        var texto = await cliente.GetStringAsync("/robots.txt");
        Assert.Contains("Sitemap: https://localhost/sitemap.xml", texto);
    }

    [Fact]
    public async Task Las_paginas_publicas_llevan_canonica_y_el_panel_no_se_indexa()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena);
        using var cliente = fabrica.Cliente();

        Assert.Contains("<link rel=\"canonical\" href=\"https://localhost/servicios\" />", await cliente.GetStringAsync("/servicios"));
        Assert.Contains("<link rel=\"canonical\" href=\"https://localhost/en/servicios\" />", await cliente.GetStringAsync("/en/servicios"));
        Assert.Contains("<meta property=\"og:url\" content=\"https://localhost/\" />", await cliente.GetStringAsync("/"));
        Assert.Contains("<meta name=\"robots\" content=\"noindex, nofollow\" />", await cliente.GetStringAsync("/admin/acceso"));
    }
}
