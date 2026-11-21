using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Domain.Gastos;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// Gastos desde el panel: se anotan desde el listado de visitas, salen en su día con signo menos y en el Excel, y se corrigen
/// y se borran volviendo a la misma pantalla.
/// </summary>
public sealed class GastosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Un_gasto_se_anota_desde_las_visitas_sale_en_su_dia_y_en_el_excel_y_se_corrige()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        // Un día lejano y distinto en cada ejecución, para no mezclarse con otras pruebas en la misma base de datos
        var dia = new DateOnly(2090, 1, 1).AddDays(Random.Shared.Next(0, 3000));
        var textoDia = dia.ToString("yyyy-MM-dd");
        var listado = $"/admin/Visitas?periodo=Dia&fecha={textoDia}";

        var visitas = WebUtility.HtmlDecode(await admin.GetStringAsync(listado));
        Assert.Contains("Nuevo gasto", visitas);
        Assert.Contains("Sin gastos anotados", visitas);
        var nuevo = $"/admin/Gastos/Nuevo?fecha={textoDia}&volver={Uri.EscapeDataString(listado)}";
        var formulario = WebUtility.HtmlDecode(await admin.GetStringAsync(nuevo));
        Assert.Contains($"value=\"{textoDia}T", formulario);
        Assert.Contains("Cuota de autónomos", formulario);
        Assert.Contains("<option value=\"3\">Dietas</option>", formulario);
        Assert.Contains("Editar tipos", formulario);
        Assert.Contains($"name=\"volver\" value=\"{listado}\"", formulario);

        // Sin concepto no se anota y vuelve con lo escrito
        var sinConcepto = await admin.PostAsync(nuevo, Formulario(await TokenAsync(admin, nuevo),
            ("TipoId", "3"), ("Fecha", $"{textoDia}T13:30"), ("Concepto", ""), ("Importe", "23,40"), ("MedioPago", "Tarjeta"), ("volver", listado)));
        Assert.Equal(HttpStatusCode.OK, sinConcepto.StatusCode);
        var conError = WebUtility.HtmlDecode(await sinConcepto.Content.ReadAsStringAsync());
        Assert.Contains("Escribe el concepto", conError);
        Assert.Contains("value=\"23,40\"", conError);

        var anotado = await admin.PostAsync(nuevo, Formulario(await TokenAsync(admin, nuevo),
            ("TipoId", "3"), ("Fecha", $"{textoDia}T13:30"), ("Concepto", $"Comida feria {marca}"), ("Importe", "23,40"), ("MedioPago", "Tarjeta"),
            ("Nota", "Ticket 12"), ("volver", listado)));
        Assert.Equal(HttpStatusCode.Redirect, anotado.StatusCode);
        Assert.Equal(listado, anotado.Headers.Location!.OriginalString);
        int id;
        await using (var db = fixture.Bd.Contexto())
            id = await db.Gastos.Where(g => g.Concepto == $"Comida feria {marca}").Select(g => g.Id).SingleAsync();

        // En el día: la fila del gasto con signo menos, el total de gastos y el encabezado (los importes llevan espacio duro antes de «€»)
        var conGasto = WebUtility.HtmlDecode(await admin.GetStringAsync(listado));
        Assert.Contains($"Gasto anotado: Comida feria {marca}, 23,40\u00a0€.", conGasto);
        Assert.Contains($"Comida feria {marca}", conGasto);
        Assert.Contains("−23,40\u00a0€", conGasto);
        Assert.Contains("0 visitas · 1 gasto", conGasto);
        Assert.Contains("1 gasto · cobrado − gastos -23,40\u00a0€", conGasto);
        Assert.Contains($"href=\"/admin/Gastos/Editar/{id}?volver=", conGasto);
        // Y en el mes, agrupado por día
        var mes = WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas?periodo=Mes&fecha={textoDia}"));
        Assert.Contains($"Comida feria {marca}", mes);
        Assert.Contains("gastos −23,40\u00a0€", mes);

        // El Excel del día lleva su hoja de gastos
        var excel = await admin.GetAsync($"/admin/Visitas/Exportar?periodo=Dia&fecha={textoDia}");
        Assert.Equal(HttpStatusCode.OK, excel.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excel.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Visitas y gastos", excel.Content.Headers.ContentDisposition?.FileNameStar ?? excel.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        // Corregir: cambia el importe y la forma de pago, y vuelve al listado
        var editar = $"/admin/Gastos/Editar/{id}?volver={Uri.EscapeDataString(listado)}";
        var edicion = WebUtility.HtmlDecode(await admin.GetStringAsync(editar));
        Assert.Contains($"value=\"Comida feria {marca}\"", edicion);
        Assert.Contains("Ticket 12", edicion);
        var editado = await admin.PostAsync(editar, Formulario(await TokenAsync(admin, editar),
            ("TipoId", "4"), ("Fecha", $"{textoDia}T09:15"), ("Concepto", $"Tren {marca}"), ("Importe", "30"), ("MedioPago", "Efectivo"), ("volver", listado)));
        Assert.Equal(HttpStatusCode.Redirect, editado.StatusCode);
        await using (var db = fixture.Bd.Contexto())
        {
            var gasto = await db.Gastos.SingleAsync(g => g.Id == id);
            Assert.Equal((4, MedioPagoGasto.Efectivo, 30m, (string?)null), (gasto.TipoId, gasto.MedioPago, gasto.Importe, gasto.Nota));
        }

        // A otra web no se vuelve nunca: sin «volver» válido, al día del gasto
        var deFuera = await admin.PostAsync(editar, Formulario(await TokenAsync(admin, editar),
            ("TipoId", "4"), ("Fecha", $"{textoDia}T09:15"), ("Concepto", $"Tren {marca}"), ("Importe", "30"), ("MedioPago", "Efectivo"),
            ("volver", "https://example.com/robar")));
        Assert.Equal(HttpStatusCode.Redirect, deFuera.StatusCode);
        Assert.Equal($"/admin/Visitas?periodo=Dia&fecha={textoDia}", deFuera.Headers.Location!.OriginalString);

        // Borrar
        var borrado = await admin.PostAsync($"/admin/Gastos/Eliminar/{id}", Formulario(await TokenAsync(admin, editar), ("volver", listado)));
        Assert.Equal(HttpStatusCode.Redirect, borrado.StatusCode);
        Assert.Contains("Sin gastos anotados", WebUtility.HtmlDecode(await admin.GetStringAsync(listado)));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/admin/Gastos/Editar/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/admin/Gastos/Eliminar/{id}", Formulario(await TokenAsync(admin, nuevo)))).StatusCode);
    }

    [Fact]
    public async Task Anotar_y_añadir_otro_vuelve_a_un_formulario_del_mismo_dia()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var dia = new DateOnly(2090, 1, 1).AddDays(Random.Shared.Next(3000, 6000)).ToString("yyyy-MM-dd");
        var nuevo = $"/admin/Gastos/Nuevo?fecha={dia}";

        var anotado = await admin.PostAsync(nuevo, Formulario(await TokenAsync(admin, nuevo),
            ("TipoId", "2"), ("Fecha", $"{dia}T10:00"), ("Concepto", "Cuota de enero"), ("Importe", "294,00"), ("MedioPago", "Domiciliacion"),
            ("accion", "otro")));

        Assert.Equal(HttpStatusCode.Redirect, anotado.StatusCode);
        Assert.Equal($"/admin/Gastos/Nuevo?fecha={dia}", anotado.Headers.Location!.OriginalString);
        var otro = WebUtility.HtmlDecode(await admin.GetStringAsync(anotado.Headers.Location.OriginalString));
        Assert.Contains("Gasto anotado: Cuota de enero, 294,00\u00a0€.", otro);
        Assert.Contains($"value=\"{dia}T", otro);
    }

    [Fact]
    public async Task Los_tipos_de_gasto_se_editan_desde_el_panel()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var dia = new DateOnly(2090, 1, 1).AddDays(Random.Shared.Next(6000, 9000)).ToString("yyyy-MM-dd");
        const string tipos = "/admin/Gastos/Tipos";

        var pantalla = WebUtility.HtmlDecode(await admin.GetStringAsync(tipos));
        Assert.Contains("Tipos de gasto", pantalla);
        Assert.Contains("Cuota de autónomos", pantalla);

        // Añadir: sale en el desplegable del gasto
        var creado = await admin.PostAsync("/admin/Gastos/CrearTipo", Formulario(await TokenAsync(admin, tipos), ("Nuevo.Nombre", $"Limpieza {marca}")));
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        var repetido = await admin.PostAsync("/admin/Gastos/CrearTipo", Formulario(await TokenAsync(admin, tipos), ("Nuevo.Nombre", $"limpieza {marca}")));
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Contains($"Ya hay un tipo «Limpieza {marca}»", WebUtility.HtmlDecode(await repetido.Content.ReadAsStringAsync()));
        int id;
        await using (var db = fixture.Bd.Contexto())
            id = await db.TiposGasto.Where(t => t.Nombre == $"Limpieza {marca}").Select(t => t.Id).SingleAsync();
        var nuevo = $"/admin/Gastos/Nuevo?fecha={dia}";
        Assert.Contains($"<option value=\"{id}\">Limpieza {marca}</option>", WebUtility.HtmlDecode(await admin.GetStringAsync(nuevo)));

        // Un gasto con él; al renombrarlo, el gasto sale con el nombre nuevo
        await admin.PostAsync(nuevo, Formulario(await TokenAsync(admin, nuevo),
            ("TipoId", id.ToString(CultureInfo.InvariantCulture)), ("Fecha", $"{dia}T10:00"), ("Concepto", $"Lejía {marca}"), ("Importe", "4,50"), ("MedioPago", "Efectivo")));
        var renombrado = await admin.PostAsync($"/admin/Gastos/RenombrarTipo/{id}", Formulario(await TokenAsync(admin, tipos), ("nombre", $"Limpieza del local {marca}")));
        Assert.Equal(HttpStatusCode.Redirect, renombrado.StatusCode);
        Assert.Contains($"Gasto · Limpieza del local {marca}", WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Visitas?periodo=Dia&fecha={dia}")));

        // Con gastos no se borra; archivado, deja de salir en los gastos nuevos pero el suyo lo conserva
        await admin.PostAsync($"/admin/Gastos/EliminarTipo/{id}", Formulario(await TokenAsync(admin, tipos)));
        Assert.Contains("no se puede borrar, pero sí archivar", WebUtility.HtmlDecode(await admin.GetStringAsync(tipos)));
        var archivado = await admin.PostAsync($"/admin/Gastos/ArchivarTipo/{id}?archivar=true", Formulario(await TokenAsync(admin, tipos)));
        Assert.Equal(HttpStatusCode.Redirect, archivado.StatusCode);
        Assert.DoesNotContain($"Limpieza del local {marca}", WebUtility.HtmlDecode(await admin.GetStringAsync(nuevo)));
        int gasto;
        await using (var db = fixture.Bd.Contexto())
            gasto = await db.Gastos.Where(g => g.Concepto == $"Lejía {marca}").Select(g => g.Id).SingleAsync();
        Assert.Contains($"Limpieza del local {marca} (archivado)</option>", WebUtility.HtmlDecode(await admin.GetStringAsync($"/admin/Gastos/Editar/{gasto}")));

        // Uno sin gastos sí se borra
        await admin.PostAsync("/admin/Gastos/CrearTipo", Formulario(await TokenAsync(admin, tipos), ("Nuevo.Nombre", $"Borrable {marca}")));
        int borrable;
        await using (var db = fixture.Bd.Contexto())
            borrable = await db.TiposGasto.Where(t => t.Nombre == $"Borrable {marca}").Select(t => t.Id).SingleAsync();
        await admin.PostAsync($"/admin/Gastos/EliminarTipo/{borrable}", Formulario(await TokenAsync(admin, tipos)));
        await using (var db = fixture.Bd.Contexto())
            Assert.False(await db.TiposGasto.AnyAsync(t => t.Id == borrable));
    }

    [Fact]
    public async Task El_panel_enseña_los_gastos_de_hoy_y_los_del_mes_por_tipo()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var panel = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin"));
        Assert.Contains("Gastos hoy", panel);
        Assert.Contains("Nuevo gasto", panel);
        // El día del salón, del enlace de «Gastos hoy»
        var hoy = Regex.Match(panel, @"periodo=Dia&fecha=(\d{4}-\d{2}-\d{2})").Groups[1].Value;
        Assert.NotEmpty(hoy);

        const string tipos = "/admin/Gastos/Tipos";
        await admin.PostAsync("/admin/Gastos/CrearTipo", Formulario(await TokenAsync(admin, tipos), ("Nuevo.Nombre", $"Panel {marca}")));
        int tipo;
        await using (var db = fixture.Bd.Contexto())
            tipo = await db.TiposGasto.Where(t => t.Nombre == $"Panel {marca}").Select(t => t.Id).SingleAsync();
        var nuevo = $"/admin/Gastos/Nuevo?fecha={hoy}";
        Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync(nuevo, Formulario(await TokenAsync(admin, nuevo),
            ("TipoId", tipo.ToString(CultureInfo.InvariantCulture)), ("Fecha", $"{hoy}T00:05"), ("Concepto", $"Toallas {marca}"), ("Importe", "12,34"),
            ("MedioPago", "Efectivo")))).StatusCode);

        var conGasto = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin"));
        Assert.Contains("−12,34\u00a0€", conGasto);
        Assert.Contains("1 gasto · 12,34\u00a0€ de la caja", conGasto);
        Assert.Contains("Cobros y gastos de", conGasto);
        Assert.Contains($"Panel {marca}", conGasto);
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
