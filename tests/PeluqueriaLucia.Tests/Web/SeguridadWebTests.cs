using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

public sealed class SeguridadWebFixture : IAsyncLifetime
{
    internal const string ClavePrueba = "Una frase aleatoria extensa 876!";
    internal BaseDeDatosPrueba Bd { get; private set; } = null!;
    internal FabricaWeb Fabrica { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Bd = await BaseDeDatosPrueba.CrearAsync();
        Fabrica = new FabricaWeb(Bd.Cadena);
        await using var scope = Fabrica.Services.CreateAsyncScope();
        var creado = await scope.ServiceProvider.GetRequiredService<AccesoService>().EstablecerAdministradorAsync("prueba", ClavePrueba);
        Assert.True(creado.Exito, creado.Error?.Mensaje);
    }

    public async Task DisposeAsync()
    {
        await Fabrica.DisposeAsync();
        await Bd.DisposeAsync();
    }
}

internal sealed class FabricaWeb(string cadena, Dictionary<string, string?>? ajustes = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = cadena, ["Seguridad:RequerirHttps"] = "false" }
                .Concat(ajustes ?? []).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value)));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<PeluqueriaDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<PeluqueriaDbContext>>();
            services.AddDbContext<PeluqueriaDbContext>(o => o.UseSqlServer(cadena));
        });
    }

    internal HttpClient Cliente(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    { AllowAutoRedirect = false, HandleCookies = cookies, BaseAddress = new Uri("https://localhost") });
}

public sealed class SeguridadWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    private static async Task<string> TokenAsync(HttpClient cliente, string ruta)
    {
        var pagina = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, pagina.StatusCode);
        var html = await pagina.Content.ReadAsStringAsync();
        var campo = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(campo.Success, html);
        return WebUtility.HtmlDecode(campo.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string usuario = "prueba", string clave = SeguridadWebFixture.ClavePrueba, string volver = "")
    {
        var token = await TokenAsync(cliente, "/admin/acceso");
        return await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Usuario"] = usuario, ["Contrasena"] = clave, ["Volver"] = volver, ["__RequestVerificationToken"] = token }));
    }

    [Fact]
    public async Task Publico_es_accesible_y_las_rutas_internas_exigen_sesion()
    {
        using var cliente = fixture.Fabrica.Cliente();
        foreach (var ruta in new[] { "/", "/servicios", "/css/publico.css", "/publico/salon.jpg" })
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(ruta)).StatusCode);
        foreach (var ruta in new[] { "/admin", "/admin/Clientes", "/admin/Clientes/Buscar?q=Ana", "/admin/Visitas/Exportar", "/admin/Visitas/Ticket/1", "/admin/Seguridad", "/admin/Seguridad/Contrasena", "/Clientes", "/imagenes/11470656-4775-424d-aa59-d4cd442c3268.jpg" })
        {
            var respuesta = await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            Assert.Contains("/admin/acceso", respuesta.Headers.Location!.ToString());
        }
        cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync("/admin/Clientes/Buscar")).StatusCode);
    }

    [Fact]
    public async Task Entrar_genera_token_cookie_segura_y_salir_impide_reutilizarla()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena, new() { ["Seguridad:RequerirHttps"] = "true" });
        using var cliente = fabrica.Cliente();
        var entrada = await EntrarAsync(cliente);
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        var cookie = entrada.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PeluqueriaLucia.Panel="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/admin", cookie);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/admin/Seguridad")).StatusCode);

        var token = await TokenAsync(cliente, "/admin/Seguridad");
        var salida = await cliente.PostAsync("/admin/acceso/salir", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, salida.StatusCode);
        using var repeticion = fabrica.Cliente(cookies: false);
        repeticion.DefaultRequestHeaders.Add("Cookie", cookie.Split(';')[0]);
        Assert.Equal(HttpStatusCode.Redirect, (await repeticion.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task No_se_aceptan_posts_sin_antifalsificacion_ni_redirecciones_externas()
    {
        using var cliente = fixture.Fabrica.Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Usuario"] = "prueba", ["Contrasena"] = SeguridadWebFixture.ClavePrueba }))).StatusCode);
        var entrada = await EntrarAsync(cliente, volver: "https://sitio-ajeno.example/admin");
        Assert.Equal(HttpStatusCode.Redirect, entrada.StatusCode);
        Assert.Equal("/admin", entrada.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/admin/Clientes/Nuevo", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Nombre"] = "No debe guardarse" }))).StatusCode);
    }

    [Fact]
    public async Task Todas_las_pantallas_existentes_y_el_buscador_funcionan_dentro_del_area()
    {
        using var cliente = fixture.Fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await EntrarAsync(cliente)).StatusCode);
        foreach (var ruta in new[] { "/admin/Visitas", "/admin/Visitas/Nueva", "/admin/Clientes", "/admin/Clientes/Nuevo", "/admin/Catalogo", "/admin/Catalogo/Nuevo", "/admin/Seguridad/Contrasena" })
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync(ruta)).StatusCode);
        var html = await cliente.GetStringAsync("/admin/Visitas/Nueva");
        Assert.Contains("data-url-buscar=\"/admin/Clientes/Buscar\"", html);
        var respuesta = await cliente.GetAsync("/admin/Clientes/Buscar?q=Ana");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType!.MediaType);
        Assert.Equal("no-store", respuesta.Headers.CacheControl!.ToString());
        var foto = await cliente.GetAsync("/admin/imagenes/11470656-4775-424d-aa59-d4cd442c3268.jpg");
        Assert.Equal(HttpStatusCode.OK, foto.StatusCode);
        Assert.Equal("no-store", foto.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Limite_por_ip_rechaza_con_429_y_retry_after_sin_confiar_en_cabecera_falsa()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena, new() { ["Seguridad:Limites:PublicoPorMinuto"] = "2" });
        using var cliente = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/")).StatusCode);
        cliente.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.100");
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/servicios")).StatusCode);
        cliente.DefaultRequestHeaders.Remove("X-Forwarded-For");
        cliente.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.101");
        var rechazo = await cliente.GetAsync("/");
        Assert.Equal(HttpStatusCode.TooManyRequests, rechazo.StatusCode);
        Assert.NotNull(rechazo.Headers.RetryAfter);
    }

    [Fact]
    public async Task Limite_de_login_y_lista_de_redes_se_aplican_antes_del_acceso()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena, new() { ["Seguridad:Limites:AccesoPorCuartoDeHora"] = "1" });
        using var cliente = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await cliente.PostAsync("/admin/acceso", new StringContent(""))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await cliente.PostAsync("/admin/acceso", new StringContent(""))).StatusCode);
        using var restringida = new FabricaWeb(fixture.Bd.Cadena, new() { ["Seguridad:RedesPermitidasPanel:0"] = "192.0.2.0/24" });
        using var otro = restringida.Cliente();
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync("/admin/acceso")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await otro.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task La_confirmacion_incorrecta_muestra_el_error_sin_devolver_contrasenas()
    {
        using var cliente = fixture.Fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await EntrarAsync(cliente)).StatusCode);
        var token = await TokenAsync(cliente, "/admin/Seguridad/Contrasena");
        const string nueva = "Otra frase aleatoria larga 456!";
        var respuesta = await cliente.PostAsync("/admin/Seguridad/Contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Actual"] = SeguridadWebFixture.ClavePrueba, ["Nueva"] = nueva, ["Confirmacion"] = "distinta", ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var html = WebUtility.HtmlDecode(await respuesta.Content.ReadAsStringAsync());
        Assert.Contains("Las contraseñas no coinciden.", html);
        Assert.DoesNotContain(nueva, html);
        Assert.DoesNotContain(SeguridadWebFixture.ClavePrueba, html);
    }

    [Fact]
    public async Task Cambiar_contrasena_desde_el_panel_conserva_la_sesion_actual_y_revoca_las_otras()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        await using (var scope = fabrica.Services.CreateAsyncScope())
        {
            var creado = await scope.ServiceProvider.GetRequiredService<AccesoService>()
                .EstablecerAdministradorAsync("cambio", SeguridadWebFixture.ClavePrueba);
            Assert.True(creado.Exito, creado.Error?.Mensaje);
        }
        using var actual = fabrica.Cliente();
        using var otra = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.Redirect, (await EntrarAsync(actual, "cambio")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EntrarAsync(otra, "cambio")).StatusCode);
        Assert.Contains("href=\"/admin/Seguridad/Contrasena\"", await actual.GetStringAsync("/admin"));

        var token = await TokenAsync(actual, "/admin/Seguridad/Contrasena");
        Assert.Equal(HttpStatusCode.BadRequest, (await actual.PostAsync("/admin/Seguridad/Contrasena",
            new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Actual"] = SeguridadWebFixture.ClavePrueba, ["Nueva"] = "Otra frase extensa distinta 456!", ["Confirmacion"] = "Otra frase extensa distinta 456!" }))).StatusCode);

        const string nueva = "Otra frase extensa distinta 456!";
        var rechazo = await actual.PostAsync("/admin/Seguridad/Contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Actual"] = "actual incorrecta", ["Nueva"] = nueva, ["Confirmacion"] = nueva, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
        Assert.Contains("La contraseña actual no es correcta.", WebUtility.HtmlDecode(await rechazo.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await otra.GetAsync("/admin")).StatusCode);

        var respuesta = await actual.PostAsync("/admin/Seguridad/Contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Actual"] = SeguridadWebFixture.ClavePrueba, ["Nueva"] = nueva, ["Confirmacion"] = nueva, ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/admin/Seguridad", respuesta.Headers.Location!.ToString());
        Assert.Contains("Contraseña cambiada.", WebUtility.HtmlDecode(await actual.GetStringAsync("/admin/Seguridad")));
        Assert.Equal(HttpStatusCode.OK, (await actual.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await otra.GetAsync("/admin")).StatusCode);
        using var entrada = fabrica.Cliente();
        Assert.Equal(HttpStatusCode.BadRequest, (await EntrarAsync(entrada, "cambio")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await EntrarAsync(entrada, "cambio", nueva)).StatusCode);
    }

    [Fact]
    public async Task Token_manipulado_y_contrasena_restablecida_invalidan_la_cookie()
    {
        using var cliente = fixture.Fabrica.Cliente();
        var entrada = await EntrarAsync(cliente);
        var cookie = entrada.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PeluqueriaLucia.Panel=")).Split(';')[0];
        using var manipulado = fixture.Fabrica.Cliente(cookies: false);
        manipulado.DefaultRequestHeaders.Add("Cookie", cookie[..^6] + "aaaaaa");
        Assert.Equal(HttpStatusCode.Redirect, (await manipulado.GetAsync("/admin")).StatusCode);
        await using var scope = fixture.Fabrica.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AccesoService>().EstablecerAdministradorAsync("prueba", SeguridadWebFixture.ClavePrueba);
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/admin")).StatusCode);
        await using var db = fixture.Bd.Contexto();
        Assert.All(await db.SesionesAdministrador.ToListAsync(), s => Assert.Equal(64, s.TokenHash.Length));
        Assert.DoesNotContain(SeguridadWebFixture.ClavePrueba, (await db.Administradores.SingleAsync(a => a.Usuario == "prueba")).HashContrasena);
    }
}
