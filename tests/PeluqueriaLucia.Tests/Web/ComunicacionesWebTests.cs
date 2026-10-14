using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Calendario mensual del panel, filtros y borrado de citas cerradas, y la página de Comunicaciones.</summary>
public sealed class ComunicacionesWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Calendario_filtros_borrado_y_comunicaciones_exigen_sesion_y_funcionan()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var anonimo = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await anonimo.GetAsync("/admin/comunicaciones")).StatusCode);
        foreach (var ruta in new[] { "/admin/comunicaciones/texto", "/admin/comunicaciones/enviar", "/admin/comunicaciones/whatsapp-enviado", "/admin/agenda/eliminar" })
            Assert.Equal(HttpStatusCode.Redirect, (await anonimo.PostAsync(ruta, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);

        // Una cita cancelada y otra confirmada el mismo día, creadas directamente en la base
        var dia = new DateTime(2026, 11, 18, 10, 0, 0);
        Guid cancelada, confirmada;
        await using (var db = fixture.Bd.Contexto())
        {
            ReservaCita Cita(DateTime inicio, EstadoCita estado) => new()
            {
                Id = Guid.NewGuid(), SolicitudId = Guid.NewGuid(), Inicio = inicio, Fin = inicio.AddMinutes(30), Nombre = "Prueba calendario", Telefono = "+34612345678",
                Correo = "cal@example.test", Canal = CanalConfirmacion.Correo, Origen = OrigenCita.Panel, Estado = estado, ServicioNombre = "Corte",
                CreadaUtc = DateTime.UtcNow, ActualizadaUtc = DateTime.UtcNow, CaducaUtc = DateTime.UtcNow.AddDays(30), IpHash = "x",
            };
            var a = Cita(dia, EstadoCita.Cancelada); var b = Cita(dia.AddHours(2), EstadoCita.Confirmada);
            db.AddRange(a, b); await db.SaveChangesAsync();
            cancelada = a.Id; confirmada = b.Id;
        }

        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var abiertas = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/agenda?dia=2026-11-18"));
        Assert.Contains("calendario-grid", abiertas);
        Assert.Contains("noviembre 2026", abiertas);
        Assert.Contains("12:00", abiertas);
        Assert.DoesNotContain("Eliminar de la agenda", abiertas);
        var canceladas = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/agenda?dia=2026-11-18&estado=Canceladas"));
        Assert.Contains("Eliminar de la agenda", canceladas);
        Assert.DoesNotContain("12:00", canceladas);
        Assert.DoesNotContain("Comunicaciones</h2>", canceladas);

        var token = await TokenAsync(admin, "/admin/agenda?dia=2026-11-18&estado=Canceladas");
        var noPermitido = await admin.PostAsync("/admin/agenda/eliminar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["id"] = confirmada.ToString(), ["dia"] = "2026-11-18", ["estado"] = "Abiertas", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, noPermitido.StatusCode);
        var borrada = await admin.PostAsync("/admin/agenda/eliminar", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["id"] = cancelada.ToString(), ["dia"] = "2026-11-18", ["estado"] = "Canceladas", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, borrada.StatusCode);
        await using (var db = fixture.Bd.Contexto())
        {
            Assert.NotNull(await db.ReservasCitas.FindAsync(confirmada));
            Assert.Null(await db.ReservasCitas.FindAsync(cancelada));
        }

        var comunicaciones = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/comunicaciones"));
        Assert.Contains("<h1>Comunicaciones</h1>", comunicaciones);
        Assert.Contains("href=\"/admin/comunicaciones?filtro=enviados\"", comunicaciones);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/admin/comunicaciones?filtro=todos")).StatusCode);
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
