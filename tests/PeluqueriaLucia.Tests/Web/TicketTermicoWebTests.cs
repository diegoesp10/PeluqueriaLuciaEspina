using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>El panel sirve el ticket ESC/POS de una visita al navegador, que lo manda a la térmica por WebUSB.</summary>
public sealed class TicketTermicoWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static readonly DateTime Fecha = new(2026, 10, 13, 11, 0, 0);

    [Fact]
    public async Task El_ticket_termico_exige_sesion_y_devuelve_los_bytes_esc_pos()
    {
        int visitaId;
        await using (var db = fixture.Bd.Contexto())
        {
            var servicio = Servicio.Crear("Corte térmico", 20m, Sexo.Mujer, Fecha);
            db.Add(servicio);
            await db.SaveChangesAsync();
            var visita = Visita.Registrar(Cliente.Crear("Eva", "Prieto", null, null, Sexo.Mujer, Fecha), Fecha, FormaPago.Efectivo, [servicio]);
            visita.Cobrar(FormaPago.Efectivo, null, null);
            db.Add(visita);
            await db.SaveChangesAsync();
            visitaId = visita.Id;
        }

        using var anonimo = fixture.Fabrica.Cliente();
        var sinSesion = await anonimo.GetAsync($"/admin/visitas/tickettermico/{visitaId}");
        Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
        Assert.Contains("/admin/acceso", sinSesion.Headers.Location!.ToString());

        using var cliente = fixture.Fabrica.Cliente();
        await EntrarAsync(cliente);

        var pagina = await cliente.GetAsync($"/admin/visitas/ticket/{visitaId}");
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var html = await pagina.Content.ReadAsStringAsync();
        Assert.Contains("data-imprimir-termica", html);
        Assert.Contains("data-vid=\"4070\"", html);
        Assert.Contains("js/ticket.js", html);

        var termico = await cliente.GetAsync($"/admin/visitas/tickettermico/{visitaId}");
        Assert.Equal(HttpStatusCode.OK, termico.StatusCode);
        Assert.Equal("application/octet-stream", termico.Content.Headers.ContentType!.MediaType);
        var bytes = await termico.Content.ReadAsByteArrayAsync();
        Assert.Equal([0x1B, 0x40], bytes[..2]);
        Assert.Equal([0x1D, 0x56, 0x42, 0x00], bytes[^4..]);
        Assert.Contains("Corte t", System.Text.Encoding.ASCII.GetString(bytes));

        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/admin/visitas/tickettermico/999999")).StatusCode);
    }

    private async Task EntrarAsync(HttpClient cliente)
    {
        var recuperacion = await fixture.RecuperacionAsync("prueba");
        var token = await TokenAsync(cliente, "/admin/acceso");
        var primerPaso = await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Usuario"] = "prueba", ["Contrasena"] = SeguridadWebFixture.ClavePrueba, ["Volver"] = "", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, primerPaso.StatusCode);
        token = await TokenAsync(cliente, "/admin/acceso/verificar");
        var segundoPaso = await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Codigo"] = recuperacion, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, segundoPaso.StatusCode);
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
