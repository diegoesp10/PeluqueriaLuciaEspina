using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

/// <summary>
/// Lo que piden los buscadores: <c>robots.txt</c> (qué no indexar y dónde está el mapa) y <c>sitemap.xml</c> con las
/// páginas públicas en español e inglés. La dirección base sale de <c>PELUQUERIA_URL_PUBLICA</c> y, si no está, de la petición.
/// </summary>
[AllowAnonymous]
[EnableRateLimiting(LimitesDePeticiones.Publico)]
public sealed class SeoController(IConfiguration configuracion) : Controller
{
    /// <summary>Páginas públicas que se indexan. Las de citas con token, la API y el panel quedan fuera.</summary>
    private static readonly (string Ruta, string Frecuencia, string Prioridad)[] Paginas =
    [
        ("/", "weekly", "1.0"),
        ("/servicios", "weekly", "0.9"),
        ("/citas", "monthly", "0.8"),
        ("/opiniones", "weekly", "0.7"),
        ("/privacidad", "yearly", "0.2"),
        ("/terminos", "yearly", "0.2"),
    ];

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Robots()
    {
        var texto = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Disallow: /admin")
            .AppendLine("Disallow: /api/")
            .AppendLine("Disallow: /citas/confirmar/")
            .AppendLine("Disallow: /citas/disponibilidad")
            .AppendLine("Disallow: /opiniones/foto/")
            .AppendLine()
            .AppendLine("Sitemap: " + Base() + "/sitemap.xml")
            .ToString();
        return Content(texto, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Sitemap()
    {
        XNamespace sm = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        var raiz = Base();
        string Es(string ruta) => raiz + ruta;
        string En(string ruta) => raiz + "/en" + (ruta == "/" ? "/" : ruta);

        var urls = Paginas.SelectMany(p => new[] { (Url: Es(p.Ruta), Idioma: "es"), (Url: En(p.Ruta), Idioma: "en") }
            .Select(v => new XElement(sm + "url",
                new XElement(sm + "loc", v.Url),
                new XElement(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "es"), new XAttribute("href", Es(p.Ruta))),
                new XElement(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "en"), new XAttribute("href", En(p.Ruta))),
                new XElement(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", "x-default"), new XAttribute("href", Es(p.Ruta))),
                new XElement(sm + "changefreq", p.Frecuencia),
                new XElement(sm + "priority", p.Prioridad))));

        var documento = new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement(sm + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml), urls));
        return Content(documento.Declaration + Environment.NewLine + documento, "application/xml", Encoding.UTF8);
    }

    /// <summary>Dirección pública sin barra final: la configurada o, en su defecto, la de esta petición.</summary>
    private string Base()
    {
        var configurada = configuracion["PELUQUERIA_URL_PUBLICA"];
        var raiz = string.IsNullOrWhiteSpace(configurada) ? Request.Scheme + "://" + Request.Host : configurada.Trim();
        return raiz.TrimEnd('/');
    }
}
