using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>
/// Todo formulario POST del panel lleva su token antifalsificación dentro. Un <c>&lt;form method="post" action="…"&gt;</c>
/// con la URL escrita a mano no recibe el token del tag helper y el envío acaba en «La petición no es válida» (400):
/// así se perdía la cita nueva del salón.
/// </summary>
public sealed class FormulariosWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static readonly Regex Formularios = new("<form\\b[^>]*>.*?</form>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    private static readonly Regex MetodoPost = new("<form\\b[^>]*\\bmethod\\s*=\\s*\"post\"", RegexOptions.IgnoreCase);

    [Fact]
    public async Task Todos_los_formularios_post_del_panel_llevan_token_antifalsificacion()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var cliente = fabrica.Cliente();
        Comprobar("/admin/acceso", await cliente.GetStringAsync("/admin/acceso"));
        await EntrarAsync(cliente);
        int clienteId, productoId, categoriaId;
        await using (var db = fixture.Bd.Contexto())
        {
            var ficha = Cliente.Crear("Formularios", "Prueba", null, "600000000", Sexo.Mujer, DateTime.UtcNow);
            var categoria = PeluqueriaLucia.Domain.Productos.CategoriaProducto.Crear("Formularios " + Guid.NewGuid().ToString("N")[..8], DateTime.Now);
            var producto = PeluqueriaLucia.Domain.Productos.Producto.Crear(new("FORM-" + Guid.NewGuid().ToString("N")[..8], null, "Producto de formularios",
                null, null, 9.90m, null, 1), categoria, DateTime.Now);
            db.AddRange(ficha, producto); await db.SaveChangesAsync(); clienteId = ficha.Id; productoId = producto.Id; categoriaId = categoria.Id;
        }
        var dia = DateOnly.FromDateTime(DateTime.Today).AddDays(200).ToString("yyyy-MM-dd");
        foreach (var ruta in new[]
        {
            "/admin", "/admin/agenda", $"/admin/agenda?dia={dia}&estado=Todas", "/admin/agenda/nueva", $"/admin/agenda/nueva?dia={dia}&hora=&clienteId={clienteId}",
            "/admin/agenda/nueva?bloqueo=true", "/admin/agenda/horarios", "/admin/comunicaciones", "/admin/comunicaciones?filtro=todos",
            "/admin/Visitas", "/admin/Visitas/Nueva", $"/admin/Visitas/Nueva?clienteId={clienteId}", "/admin/Clientes", "/admin/Clientes/Nuevo",
            $"/admin/Clientes/Detalle/{clienteId}", $"/admin/Clientes/Editar/{clienteId}", "/admin/Catalogo", "/admin/Catalogo/Nuevo",
            "/admin/Tratamientos", "/admin/Tratamientos/Nuevo", "/admin/Tratamientos/Categorias", "/admin/Tratamientos/Categoria",
            "/admin/Resenas", "/admin/Seguridad", "/admin/Seguridad/Contrasena", "/admin/Seguridad/Authenticator",
            "/admin/Productos", "/admin/Productos/Nuevo", $"/admin/Productos/Ficha/{productoId}", $"/admin/Productos/Editar/{productoId}",
            "/admin/Productos/Categorias", $"/admin/Productos/Categorias?editar={categoriaId}",
        })
        {
            var respuesta = await cliente.GetAsync(ruta);
            Assert.True(respuesta.StatusCode == HttpStatusCode.OK, $"{ruta} responde {(int)respuesta.StatusCode}");
            Comprobar(ruta, await respuesta.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Los_desplegables_de_servicios_y_categorias_se_pueden_buscar_escribiendo()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var cliente = fabrica.Cliente();
        await EntrarAsync(cliente);
        var cita = await cliente.GetStringAsync("/admin/agenda/nueva");
        Assert.Matches("<select[^>]*id=\"ServicioId\"[^>]*data-buscable=", cita);
        Assert.Contains("/js/select-buscable.js", cita);
        var publicar = await cliente.GetStringAsync("/admin/Tratamientos/Nuevo");
        Assert.Matches("<select[^>]*id=\"ServicioId\"[^>]*data-buscable=", publicar);
        Assert.Matches("<select[^>]*id=\"CategoriaId\"[^>]*data-buscable=", publicar);
        var catalogo = await cliente.GetStringAsync("/admin/Catalogo/Nuevo");
        Assert.Matches("<select[^>]*id=\"CategoriaPublicaId\"[^>]*data-buscable=", catalogo);
        Assert.Contains("name=\"Dualidad\"", catalogo);
        // Preferencia de aviso en la ficha y aviso al dar una cita en el salón
        Assert.Contains("name=\"CanalPreferido\"", await cliente.GetStringAsync("/admin/Clientes/Nuevo"));
        Assert.Contains("data-aviso-cita", cita);
        Assert.Contains("name=\"Aviso\"", cita);
        // Los buscadores de clientes sugieren mientras se escribe; el botón «Buscar» solo queda para navegadores sin JavaScript
        Assert.Contains("data-solo-sin-js>Buscar</button>", cita);
        Assert.Contains("data-combo-input", await cliente.GetStringAsync("/admin/Visitas/Nueva"));
        // Con algún producto a la venta, «Nueva visita» ofrece su buscador (también para el lector de códigos de barras)
        await using (var db = fixture.Bd.Contexto())
        {
            db.Add(PeluqueriaLucia.Domain.Productos.Producto.Crear(new("BUSC-" + Guid.NewGuid().ToString("N")[..8], null, "Producto buscable",
                null, null, 5m, null, 0), PeluqueriaLucia.Domain.Productos.CategoriaProducto.Crear("Buscables " + Guid.NewGuid().ToString("N")[..8], DateTime.Now), DateTime.Now));
            await db.SaveChangesAsync();
        }
        Assert.Matches("<select[^>]*id=\"producto-elegir\"[^>]*data-buscable=", await cliente.GetStringAsync("/admin/Visitas/Nueva"));
        // La categoría del producto se busca (o se escribe una nueva) y el inventario se filtra con otro buscador de categorías
        Assert.Matches("<select[^>]*id=\"CategoriaId\"[^>]*data-buscable=[^>]*data-buscable-libre=\"CategoriaNueva\"", await cliente.GetStringAsync("/admin/Productos/Nuevo"));
        Assert.Matches("<select[^>]*id=\"categoria-productos\"[^>]*data-buscable=", await cliente.GetStringAsync("/admin/Productos"));
        foreach (var ruta in new[] { "/admin/Clientes", "/admin/Catalogo", "/admin/Tratamientos", "/admin/Resenas", "/admin/comunicaciones", "/admin/Productos" })
            Assert.Contains("data-buscar-al-escribir", await cliente.GetStringAsync(ruta));
    }

    private static void Comprobar(string ruta, string html)
    {
        var post = Formularios.Matches(html).Select(m => m.Value).Where(f => MetodoPost.IsMatch(f)).ToList();
        foreach (var formulario in post)
            Assert.True(formulario.Contains("name=\"__RequestVerificationToken\"", StringComparison.Ordinal),
                $"{ruta}: formulario POST sin token antifalsificación: {formulario[..Math.Min(160, formulario.Length)]}");
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
