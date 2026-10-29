using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Sorteos del panel a la web pública (sin datos de participantes) y el coste de compra en la ficha del producto.</summary>
public sealed class SorteosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Crear_apuntar_anunciar_sortear_y_avisar_sin_que_la_web_enseñe_a_nadie()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        using var publico = fabrica.Cliente();
        await EntrarAsync(admin);
        var marca = Guid.NewGuid().ToString("N")[..6];
        var servicio = await ServicioAsync($"Hidratación {marca}");
        var ana = await ClienteAsync($"Ana{marca}", "+34611000001");
        var bea = await ClienteAsync($"Bea{marca}", "+34611000002");

        var lista = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Sorteos"));
        Assert.Contains("href=\"/admin/Sorteos\"", lista);
        Assert.Contains("Nuevo sorteo", lista);

        // Sin el título en inglés no se crea
        var sinIngles = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("Premio", $"Servicio-{servicio}"), ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        Assert.Equal(HttpStatusCode.OK, sinIngles.StatusCode);
        Assert.Contains("Escribe también el título en inglés", WebUtility.HtmlDecode(await sinIngles.Content.ReadAsStringAsync()));

        var creado = await admin.PostAsync("/admin/Sorteos/Nuevo", Formulario(await TokenAsync(admin, "/admin/Sorteos/Nuevo"),
            ("Titulo", $"Sorteo {marca}"), ("TituloEn", $"Giveaway {marca}"), ("Premio", $"Servicio-{servicio}"),
            ("FechaSorteo", DateTime.Today.AddDays(5).ToString("yyyy-MM-ddT18:00"))));
        Assert.Equal(HttpStatusCode.Redirect, creado.StatusCode);
        var detalle = creado.Headers.Location!.OriginalString;
        var id = detalle[(detalle.LastIndexOf('/') + 1)..];

        foreach (var cliente in new[] { ana, bea })
            Assert.Equal(HttpStatusCode.Redirect, (await admin.PostAsync($"/admin/Sorteos/AgregarParticipante/{id}", Formulario(await TokenAsync(admin, detalle),
                ("clienteId", cliente.ToString(CultureInfo.InvariantCulture))))).StatusCode);
        var conParticipantes = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("Participantes · 2", conParticipantes);
        Assert.Contains($"Ana{marca} Pérez", conParticipantes);

        var anuncio = await admin.PostAsync($"/admin/Sorteos/Anunciar/{id}", Formulario(await TokenAsync(admin, detalle),
            ("TextoAnuncio", $"Participa con tu visita {marca}"), ("TextoAnuncioEn", $"Join with your visit {marca}"),
            ("DesdeAnuncio", DateTime.Today.ToString("yyyy-MM-dd")), ("HastaAnuncio", DateTime.Today.AddDays(5).ToString("yyyy-MM-dd"))));
        Assert.Equal(HttpStatusCode.Redirect, anuncio.StatusCode);

        // La web lo anuncia (aviso flotante, menú y página) en los dos idiomas, sin nombres de clientes
        var portada = WebUtility.HtmlDecode(await publico.GetStringAsync("/"));
        Assert.Contains("data-aviso-sorteo", portada);
        Assert.Contains($"Sorteo {marca}", portada);
        Assert.Contains("href=\"/sorteos\"", portada);
        Assert.Contains("/js/aviso-sorteo.js", portada);
        var pagina = WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos"));
        Assert.Contains($"Participa con tu visita {marca}", pagina);
        Assert.Contains($"Hidratación {marca}", pagina);
        Assert.DoesNotContain($"Ana{marca}", pagina);
        Assert.DoesNotContain("data-aviso-sorteo", pagina);
        var ingles = WebUtility.HtmlDecode(await publico.GetStringAsync("/en/sorteos"));
        Assert.Contains("Giveaways", ingles);
        Assert.Contains($"Giveaway {marca}", ingles);
        Assert.Contains($"Join with your visit {marca}", ingles);

        // Sortear: la pantalla descubre al ganador con la ruleta la primera vez
        var sorteo = await admin.PostAsync($"/admin/Sorteos/Sortear/{id}", Formulario(await TokenAsync(admin, detalle)));
        Assert.Equal(HttpStatusCode.Redirect, sorteo.StatusCode);
        var revelado = WebUtility.HtmlDecode(await admin.GetStringAsync(detalle));
        Assert.Contains("¡Tenemos ganador!", revelado);
        Assert.Contains("data-revelar=\"true\"", revelado);
        Assert.Contains("Al azar entre 2 participantes", revelado);
        Assert.Contains("data-revelar=\"false\"", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        // WhatsApp: se anota el aviso y se abre la conversación con el mensaje
        var whatsApp = await admin.PostAsync($"/admin/Sorteos/AvisarWhatsApp/{id}", Formulario(await TokenAsync(admin, detalle),
            ("textoAviso", "¡Enhorabuena! Has ganado el sorteo.")));
        Assert.Equal(HttpStatusCode.Redirect, whatsApp.StatusCode);
        Assert.Matches(@"^https://wa\.me/3461100000[12]\?text=", whatsApp.Headers.Location!.AbsoluteUri);
        Assert.Contains("Avisado por WhatsApp", WebUtility.HtmlDecode(await admin.GetStringAsync(detalle)));

        var realizado = WebUtility.HtmlDecode(await publico.GetStringAsync("/sorteos"));
        Assert.Contains("¡Ya tenemos ganador!", realizado);
        Assert.DoesNotContain($"Ana{marca}", realizado);
        Assert.DoesNotContain($"Bea{marca}", realizado);
        var historial = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/Sorteos?vista=Historial"));
        Assert.Contains($"Sorteo {marca}", historial);
        Assert.Matches($"(Ana|Bea){marca} Pérez", historial);
    }

    [Fact]
    public async Task La_ficha_del_producto_enseña_y_anota_su_coste_de_compra()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var admin = fabrica.Cliente();
        await EntrarAsync(admin);
        var id = await ProductoAsync();
        var ficha = $"/admin/Productos/Ficha/{id}";
        Assert.Contains("Coste de compra", WebUtility.HtmlDecode(await admin.GetStringAsync(ficha)));

        var anotado = await admin.PostAsync($"/admin/Productos/RegistrarCoste/{id}", Formulario(await TokenAsync(admin, ficha),
            ("Coste", "12,40"), ("Desde", DateTime.Today.ToString("yyyy-MM-dd")), ("NotaCoste", "Proveedor nuevo")));
        var futuro = await admin.PostAsync($"/admin/Productos/RegistrarCoste/{id}", Formulario(await TokenAsync(admin, ficha),
            ("Coste", "1"), ("Desde", DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"))));

        Assert.Equal(HttpStatusCode.Redirect, anotado.StatusCode);
        Assert.EndsWith("#costes", anotado.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.OK, futuro.StatusCode);
        Assert.Contains("La fecha del coste no puede ser futura.", WebUtility.HtmlDecode(await futuro.Content.ReadAsStringAsync()));
        var html = WebUtility.HtmlDecode(await admin.GetStringAsync(ficha));
        Assert.Contains("Proveedor nuevo", html);
        Assert.Contains("12,40", html);
        Assert.Contains("Actual", html);
    }

    private async Task<int> ServicioAsync(string nombre)
    {
        await using var db = fixture.Bd.Contexto();
        var servicio = Servicio.Crear(nombre, 25m, Sexo.Mujer, DateTime.Now, nombre + " (en)");
        db.Add(servicio);
        await db.SaveChangesAsync();
        return servicio.Id;
    }

    private async Task<int> ClienteAsync(string nombre, string telefono)
    {
        await using var db = fixture.Bd.Contexto();
        var cliente = Cliente.Crear(nombre, "Pérez", null, telefono, Sexo.Mujer, DateTime.Now);
        db.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    private async Task<int> ProductoAsync()
    {
        await using var db = fixture.Bd.Contexto();
        var categoria = CategoriaProducto.Crear("Costes " + Guid.NewGuid().ToString("N")[..8], DateTime.Now);
        var producto = Producto.Crear(new FichaProducto("COS-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), null, "Mascarilla de coste", null, null, 20m, 10m, 1), categoria, DateTime.Now);
        db.Add(producto);
        await db.SaveChangesAsync();
        return producto.Id;
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
