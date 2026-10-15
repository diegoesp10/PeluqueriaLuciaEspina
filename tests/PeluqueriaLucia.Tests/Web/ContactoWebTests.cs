using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Los teléfonos de contacto se editan en el panel y la web los enseña todos, en su orden.</summary>
public sealed class ContactoWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task El_administrador_cambia_los_telefonos_y_la_web_los_muestra()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var publico = fabrica.Cliente();
        using var admin = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await publico.GetAsync("/admin/contacto")).StatusCode);
        await EntrarAsync(admin);

        // Sin teléfonos guardados, el panel y la web parten del de contenido-publico.json
        var pagina = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/contacto"));
        Assert.Contains("name=\"telefonos\" value=\"659 306 659\"", pagina);
        Assert.Contains("Contacto de la web", pagina);
        var token = await TokenAsync(admin, "/admin/contacto");

        // «Añadir otro teléfono» sin JavaScript: una fila más y nada guardado
        var conFila = WebUtility.HtmlDecode(await (await admin.PostAsync("/admin/contacto", Formulario(token, ("telefonos", "659 306 659"), ("anadir", "1")))).Content.ReadAsStringAsync());
        Assert.Equal(2, Regex.Matches(conFila, "name=\"telefonos\"").Count - 1); // la plantilla de fila cuenta una más

        var invalido = await admin.PostAsync("/admin/contacto", Formulario(token, ("telefonos", "659 306 659"), ("telefonos", "abc")));
        Assert.Equal(HttpStatusCode.OK, invalido.StatusCode);
        Assert.Contains("Revisa el teléfono «abc»", WebUtility.HtmlDecode(await invalido.Content.ReadAsStringAsync()));

        var guardado = await admin.PostAsync("/admin/contacto", Formulario(token, ("telefonos", "659 306 659"), ("telefonos", "985757692")));
        Assert.Equal(HttpStatusCode.Redirect, guardado.StatusCode);

        var portada = WebUtility.HtmlDecode(await publico.GetStringAsync("/"));
        Assert.Contains("class=\"telefonos-grandes\"", portada);
        Assert.Contains("href=\"tel:+34659306659\"", portada); Assert.Contains("href=\"tel:+34985757692\"", portada);
        Assert.True(portada.IndexOf("tel:+34659306659", StringComparison.Ordinal) < portada.IndexOf("tel:+34985757692", StringComparison.Ordinal));
        Assert.Matches("\"telephone\":\"[^\"]*34 659 306 659\"", portada); // el «+» puede ir escapado como secuencia unicode
        Assert.Contains("985757692", WebUtility.HtmlDecode(await publico.GetStringAsync("/privacidad")));
    }

    private static FormUrlEncodedContent Formulario(string token, params (string Clave, string Valor)[] campos) =>
        new([new("__RequestVerificationToken", token), .. campos.Select(c => new KeyValuePair<string, string>(c.Clave, c.Valor))]);

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
