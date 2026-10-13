using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;
using PeluqueriaLucia.Web.Publico;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Web pública en dos idiomas, páginas legales y página de error.</summary>
public sealed class IdiomaWebTests
{
    [Fact]
    public void Todas_las_claves_de_las_vistas_publicas_tienen_traduccion()
    {
        var textos = Textos.Cargar();
        var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/PeluqueriaLucia.Web"));
        var vistas = Directory.GetFiles(Path.Combine(raiz, "Views"), "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(raiz, "Controllers"), "*.cs"));
        var faltan = vistas
            .SelectMany(f => Regex.Matches(File.ReadAllText(f), "(?:\\bT|textos)(?:\\.F\\(|\\[)\"([^\"]+)\"").Select(m => m.Groups[1].Value))
            .Distinct().Where(k => !textos.ContainsKey(k)).ToList();
        Assert.True(faltan.Count == 0, "Sin traducción: " + string.Join(" | ", faltan));
    }

    [Fact]
    public async Task Ingles_bajo_en_con_enlaces_hreflang_y_formularios_con_language()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();

        var es = await cliente.GetStringAsync("/");
        Assert.Contains("<html lang=\"es\"", es); Assert.Contains("Tu cabello.", es);
        Assert.Contains("hreflang=\"en\" href=\"https://localhost/en/\"", es);

        var en = await cliente.GetStringAsync("/en");
        Assert.Contains("<html lang=\"en\"", en); Assert.Contains("Your hair.", en); Assert.DoesNotContain("Tu cabello.", en);
        Assert.Contains("href=\"/en/servicios\"", en); Assert.Contains("href=\"/en/citas\"", en);
        Assert.Contains("href=\"/admin\"", en); // el panel solo existe en español

        var citas = await cliente.GetStringAsync("/en/citas");
        Assert.Contains("Request my appointment", citas);
        Assert.Contains("name=\"language\" value=\"en\"", citas);
        Assert.Contains("data-url-solicitar=\"/en/citas/contacto/solicitar\"", citas);
        Assert.Contains("name=\"language\" value=\"es\"", await cliente.GetStringAsync("/citas"));

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/en/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/en/admin/agenda")).StatusCode);
    }

    [Fact]
    public async Task Validacion_responde_en_el_idioma_de_la_pagina()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();
        var html = await cliente.GetStringAsync("/en/citas");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var id = Regex.Match(html, "name=\"Formulario.SolicitudId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var respuesta = await cliente.PostAsync("/en/citas", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Formulario.SolicitudId"] = id, ["language"] = "en",
            ["Formulario.Dia"] = DateTime.Today.ToString("yyyy-MM-dd"), ["Formulario.Telefono"] = "612345678", ["Formulario.Consentimiento"] = "true",
        }));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Please enter your email address.", errores);
        Assert.DoesNotContain("Escribe tu correo electrónico.", errores);
    }

    [Theory]
    [InlineData("/privacidad", "Política de", "Agencia Española de Protección de Datos")]
    [InlineData("/en/privacidad", "Privacy", "Spanish Data Protection Agency")]
    [InlineData("/terminos", "Términos y", "Cómo funciona pedir cita")]
    [InlineData("/en/terminos", "Terms and", "How booking works")]
    public async Task Paginas_legales_en_los_dos_idiomas(string ruta, string titulo, string texto)
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();
        var html = WebUtility.HtmlDecode(await cliente.GetStringAsync(ruta));
        Assert.Contains(titulo, html); Assert.Contains(texto, html);
        Assert.Contains("659 306 659", html);
    }

    [Fact]
    public async Task Rutas_inexistentes_y_errores_muestran_la_pagina_de_error_traducida()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();

        var es = await cliente.GetAsync("/no-existe");
        Assert.Equal(HttpStatusCode.NotFound, es.StatusCode);
        Assert.Contains("No encontramos esta página", WebUtility.HtmlDecode(await es.Content.ReadAsStringAsync()));

        var en = await cliente.GetAsync("/en/no-existe");
        Assert.Equal(HttpStatusCode.NotFound, en.StatusCode);
        var html = await en.Content.ReadAsStringAsync();
        Assert.Contains("We can't find this page", WebUtility.HtmlDecode(html)); Assert.Contains("<html lang=\"en\"", html);

        // Los scripts redirigen aquí cuando una llamada falla: la respuesta conserva el código
        var directo = await cliente.GetAsync("/en/error/500");
        Assert.Equal(HttpStatusCode.InternalServerError, directo.StatusCode);
        Assert.Contains("Something went wrong", await directo.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/error/200")).StatusCode);
    }

    [Fact]
    public async Task Favicon_iconos_y_manifiesto_se_enlazan_y_se_sirven()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();
        foreach (var pagina in new[] { "/", "/en/servicios" })
        {
            var html = await cliente.GetStringAsync(pagina);
            Assert.Contains("rel=\"icon\" href=\"", html); Assert.Contains("rel=\"apple-touch-icon\"", html); Assert.Contains("rel=\"manifest\"", html);
            Assert.Contains("og:image\" content=\"https://localhost/iconos/icono-512.png\"", html);
        }
        foreach (var (ruta, tipo) in new[] { ("/favicon.ico", "image/x-icon"), ("/iconos/favicon-96.png", "image/png"), ("/iconos/icono-192.png", "image/png"),
            ("/iconos/icono-512.png", "image/png"), ("/iconos/icono-mascara-512.png", "image/png"), ("/iconos/apple-touch-icon.png", "image/png"), ("/site.webmanifest", "application/manifest+json") })
        {
            var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.Equal(tipo, respuesta.Content.Headers.ContentType!.MediaType);
        }
    }

    [Fact]
    public async Task Las_librerias_de_movimiento_se_sirven_desde_el_propio_sitio()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena); using var cliente = fabrica.Cliente();
        var html = await cliente.GetStringAsync("/");
        foreach (var recurso in new[] { "/lib/gsap/gsap.min.js", "/lib/gsap/ScrollTrigger.min.js", "/lib/gsap/SplitText.min.js", "/lib/lenis/lenis.min.js", "/js/movimiento.js" })
        {
            var url = Regex.Match(html, "src=\"(" + Regex.Escape(recurso) + "[^\"]*)\"").Groups[1].Value;
            Assert.False(string.IsNullOrEmpty(url), recurso);
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(WebUtility.HtmlDecode(url))).StatusCode);
        }
        Assert.DoesNotMatch("(?i)<script[^>]+src=\"(https?:)?//", html); // ningún script de otro dominio (CDN)
    }
}
