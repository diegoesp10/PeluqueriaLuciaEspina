using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using PeluqueriaLucia.Web.Seguridad;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

public sealed class SeguridadWebFixture : IAsyncLifetime
{
    internal const string ClavePrueba = "Una frase aleatoria extensa 876!";
    internal BaseDeDatosPrueba Bd { get; private set; } = null!;
    internal FabricaWeb Fabrica { get; private set; } = null!;
    internal const string SecretoPrueba = "JBSWY3DPEHPK3PXP";
    internal static string Codigo(DateTime? ahora = null) => new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(SecretoPrueba)).ComputeTotp(ahora ?? DateTime.UtcNow);
    internal async Task<string> RecuperacionAsync(string usuario)
    {
        await using var scope = Fabrica.Services.CreateAsyncScope();
        await using var db = Bd.Contexto();
        var administrador = await db.Administradores.SingleOrDefaultAsync(x => x.Usuario == usuario);
        if (administrador is null) return "inexistente";
        if (!administrador.MfaActivo) administrador.ActivarMfa(scope.ServiceProvider.GetRequiredService<PeluqueriaLucia.Application.Abstracciones.IProtectorSecretos>().Proteger(SecretoPrueba, "totp"), -1);
        var codigo = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        db.RecuperacionesMfa.Add(PeluqueriaLucia.Domain.Seguridad.RecuperacionMfa.Crear(administrador.Id, MfaService.Hash(codigo)));
        await db.SaveChangesAsync();
        return codigo;
    }

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

internal sealed class FabricaWeb(string cadena, Dictionary<string, string?>? ajustes = null, TimeProvider? reloj = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Seguridad:RequerirHttps"] = "false", ["Seguridad:Limites:AccesoPorCuartoDeHora"] = "100" }
                .Concat(ajustes ?? []).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value)));
        builder.ConfigureServices(services =>
        {
            // Ninguna prueba web puede enviar correo, aunque el equipo tenga SMTP activado.
            services.RemoveAll<Comunicaciones.Correo.IEnviadorCorreo>();
            services.AddSingleton<Comunicaciones.Correo.IEnviadorCorreo, CorreoPrueba>();
            if (reloj is not null) { services.RemoveAll<TimeProvider>(); services.AddSingleton(reloj); }
            services.RemoveAll<DbContextOptions<PeluqueriaDbContext>>();
            services.RemoveAll<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration<PeluqueriaDbContext>>();
            services.AddDbContext<PeluqueriaDbContext>(o => o.UseSqlServer(cadena));
        });
    }

    internal HttpClient Cliente(bool cookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    { AllowAutoRedirect = false, HandleCookies = cookies, BaseAddress = new Uri("https://localhost") });
}

internal sealed class CorreoPrueba : Comunicaciones.Correo.IEnviadorCorreo
{
    public bool Configurado => false;
    public Task<Comunicaciones.Correo.ResultadoEnvio> EnviarAsync(Comunicaciones.Correo.CorreoSaliente correo, CancellationToken ct = default)
        => throw new InvalidOperationException("Las pruebas web no envían correos reales.");
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

    private async Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string usuario = "prueba", string clave = SeguridadWebFixture.ClavePrueba, string volver = "")
    {
        var recuperacion = await fixture.RecuperacionAsync(usuario);
        var token = await TokenAsync(cliente, "/admin/acceso");
        var primerPaso = await cliente.PostAsync("/admin/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Usuario"] = usuario, ["Contrasena"] = clave, ["Volver"] = volver, ["__RequestVerificationToken"] = token }));
        if (primerPaso.StatusCode != HttpStatusCode.Redirect) return primerPaso;
        Assert.Equal("/admin/acceso/verificar", primerPaso.Headers.Location!.ToString());
        Assert.DoesNotContain(primerPaso.Headers.GetValues("Set-Cookie"), c => c.StartsWith("PeluqueriaLucia.Panel="));
        token = await TokenAsync(cliente, "/admin/acceso/verificar");
        return await cliente.PostAsync("/admin/acceso/verificar", new FormUrlEncodedContent(new Dictionary<string,string> { ["Codigo"] = recuperacion, ["__RequestVerificationToken"] = token }));
    }

    private static async Task<Guid> SesionAsync(HttpClient cliente)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Get, Autenticacion.RutaEstado);
        solicitud.Headers.Accept.ParseAdd("application/json");
        var respuesta = await cliente.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("no-store", respuesta.Headers.CacheControl!.ToString());
        using var estado = System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Single(estado.RootElement.EnumerateObject());
        return estado.RootElement.GetProperty("sesionId").GetGuid();
    }

    [Fact]
    public async Task Nueva_cita_del_salon_busca_la_ficha_y_aparece_en_la_agenda()
    {
        using var cliente=fixture.Fabrica.Cliente();Assert.Equal(HttpStatusCode.Redirect,(await EntrarAsync(cliente)).StatusCode);
        var sufijo=Guid.NewGuid().ToString("N")[..8];
        await using var db=fixture.Bd.Contexto();
        var ficha=PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Lola"+sufijo,"Agenda",null,null,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.UtcNow);db.Add(ficha);await db.SaveChangesAsync();
        var dia=DateOnly.FromDateTime(DateTime.Today).AddDays(Random.Shared.Next(100,300)).ToString("yyyy-MM-dd");
        var busqueda=await cliente.GetStringAsync($"/admin/agenda/nueva?dia={dia}&q=Lola{sufijo}");
        Assert.Contains($"clienteId={ficha.Id}",busqueda);
        var token=await TokenAsync(cliente,$"/admin/agenda/nueva?dia={dia}&clienteId={ficha.Id}");
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/admin/agenda/nueva",new FormUrlEncodedContent(new Dictionary<string,string>{["Dia"]=dia,["Hora"]="21:00"}))).StatusCode);
        var guardada=await cliente.PostAsync("/admin/agenda/nueva",new FormUrlEncodedContent(new Dictionary<string,string>
            {["__RequestVerificationToken"]=token,["Dia"]=dia,["Hora"]="21:00",["Minutos"]="45",["ClienteId"]=ficha.Id.ToString(),["Bloqueo"]="false"}));
        Assert.Equal(HttpStatusCode.Redirect,guardada.StatusCode);
        var agenda=await cliente.GetStringAsync("/admin/agenda?dia="+dia);
        Assert.Contains("Lola"+sufijo,agenda);Assert.Contains("Dada en el salón",agenda);Assert.Contains($"/admin/Visitas/Nueva?clienteId={ficha.Id}",agenda);
    }

    [Fact]
    public async Task Autocompletar_contacto_exige_sesion_y_antifalsificacion_y_reutiliza_ficha()
    {
        using var cliente=fixture.Fabrica.Cliente();Assert.Equal(HttpStatusCode.Redirect,(await EntrarAsync(cliente)).StatusCode);
        var sufijo=Guid.NewGuid().ToString("N");var email=sufijo+"@example.test";var telefono="+346"+Random.Shared.Next(10000000,99999999);
        await using var db=fixture.Bd.Contexto();
        var existente=PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Ana","Prueba",email,telefono,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.UtcNow);db.Add(existente);await db.SaveChangesAsync();
        var datos=new Dictionary<string,string>{["telefono"]=telefono,["email"]=email};
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/admin/Clientes/Reconocer",new FormUrlEncodedContent(datos))).StatusCode);
        datos["__RequestVerificationToken"]=await TokenAsync(cliente,"/admin/Clientes/Nuevo");
        var respuesta=await cliente.PostAsync("/admin/Clientes/Reconocer",new FormUrlEncodedContent(datos));Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        using var json=System.Text.Json.JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());Assert.Equal(existente.Id,json.RootElement.GetProperty("id").GetInt32());Assert.Equal("Prueba",json.RootElement.GetProperty("apellido").GetString());
        var guardado=await cliente.PostAsync("/admin/Clientes/Nuevo",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=datos["__RequestVerificationToken"],["Nombre"]="Ana revisada",["Apellido"]="Prueba",["Telefono"]=telefono,["Email"]=email,["Sexo"]="Mujer"}));
        Assert.Equal(HttpStatusCode.Redirect,guardado.StatusCode);Assert.Contains(existente.Id.ToString(),guardado.Headers.Location!.ToString());
        Assert.Equal(1,await db.Clientes.CountAsync(x=>x.Email==email));Assert.Equal("Ana revisada",(await db.Clientes.AsNoTracking().SingleAsync(x=>x.Id==existente.Id)).Nombre);
    }

    [Fact]
    public async Task Revocar_la_sesion_actual_borra_cookie_expulsa_y_bloquea_su_reutilizacion()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var actual = fabrica.Cliente();
        var entrada = await EntrarAsync(actual);
        var cookie = entrada.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("PeluqueriaLucia.Panel=")).Split(';')[0];
        var id = await SesionAsync(actual);
        var token = await TokenAsync(actual, "/admin/Seguridad");
        var salida = await actual.PostAsync($"/admin/Seguridad/Revocar/{id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.Redirect, salida.StatusCode);
        Assert.Equal("/admin/acceso", salida.Headers.Location!.ToString());
        Assert.Contains(salida.Headers.GetValues("Set-Cookie"), c => c.StartsWith("PeluqueriaLucia.Panel=;") && c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("data-sesiones-cambiadas", await actual.GetStringAsync("/admin/acceso"));
        Assert.Equal(HttpStatusCode.Redirect, (await actual.GetAsync("/admin")).StatusCode);
        using var copia = fabrica.Cliente(cookies: false);
        copia.DefaultRequestHeaders.Add("Cookie", cookie);
        foreach (var ruta in new[] { "/admin", "/admin/Seguridad", "/admin/Visitas/Exportar", "/admin/Visitas/Ticket/1", "/admin/imagenes/11470656-4775-424d-aa59-d4cd442c3268.jpg" })
            Assert.Equal(HttpStatusCode.Redirect, (await copia.GetAsync(ruta)).StatusCode);
        copia.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await copia.GetAsync(Autenticacion.RutaEstado)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await copia.GetAsync("/admin/Clientes/Buscar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await copia.PostAsync("/admin/Clientes/Nuevo", new FormUrlEncodedContent(new Dictionary<string, string> { ["Nombre"] = "No debe guardarse", ["__RequestVerificationToken"] = token }))).StatusCode);
    }

    [Fact]
    public async Task Cerrar_otras_y_todas_expulsa_los_accesos_afectados_y_exige_csrf()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        using var actual = fabrica.Cliente();
        using var otra = fabrica.Cliente();
        await EntrarAsync(actual);
        await EntrarAsync(otra);
        var token = await TokenAsync(actual, "/admin/Seguridad");
        Assert.Equal(HttpStatusCode.BadRequest, (await actual.PostAsync("/admin/Seguridad/CerrarTodas", new StringContent(""))).StatusCode);
        await SesionAsync(otra);
        Assert.Equal(HttpStatusCode.Redirect, (await actual.PostAsync("/admin/Seguridad/CerrarOtras", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))).StatusCode);
        await SesionAsync(actual);
        otra.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await otra.GetAsync(Autenticacion.RutaEstado)).StatusCode);
        otra.DefaultRequestHeaders.Accept.Clear();
        await EntrarAsync(otra);
        token = await TokenAsync(actual, "/admin/Seguridad");
        var cierre = await actual.PostAsync("/admin/Seguridad/CerrarTodas", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
        Assert.Equal("/admin/acceso", cierre.Headers.Location!.ToString());
        Assert.Equal(HttpStatusCode.Redirect, (await actual.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await otra.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Revocar_otra_sesion_no_cierra_la_actual_ni_permite_cerrar_la_de_otro_usuario()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        await using (var scope = fabrica.Services.CreateAsyncScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<AccesoService>().EstablecerAdministradorAsync("ajeno", SeguridadWebFixture.ClavePrueba)).Exito);
        using var actual = fabrica.Cliente();
        using var otra = fabrica.Cliente();
        using var ajena = fabrica.Cliente();
        await EntrarAsync(actual);
        await EntrarAsync(otra);
        await EntrarAsync(ajena, "ajeno");
        var id = await SesionAsync(otra);
        var idAjena = await SesionAsync(ajena);
        var token = await TokenAsync(actual, "/admin/Seguridad");
        Assert.Equal(HttpStatusCode.NotFound, (await actual.PostAsync($"/admin/Seguridad/Revocar/{idAjena}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))).StatusCode);
        await SesionAsync(ajena);
        Assert.Equal(HttpStatusCode.Redirect, (await actual.PostAsync($"/admin/Seguridad/Revocar/{id}", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))).StatusCode);
        await SesionAsync(actual);
        Assert.Equal(HttpStatusCode.Redirect, (await otra.GetAsync("/admin")).StatusCode);
    }

    private sealed class RelojSesion : TimeProvider
    {
        private DateTimeOffset ahora = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => ahora;
        internal void Avanzar(TimeSpan tiempo) => ahora += tiempo;
    }

    [Fact]
    public async Task Sondear_no_renueva_inactividad_y_una_sesion_caducada_no_permite_refresh()
    {
        var reloj = new RelojSesion();
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena, reloj: reloj);
        using var cliente = fabrica.Cliente();
        await EntrarAsync(cliente);
        await SesionAsync(cliente);
        reloj.Avanzar(TimeSpan.FromHours(11));
        await SesionAsync(cliente);
        reloj.Avanzar(TimeSpan.FromHours(1));
        cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(Autenticacion.RutaEstado)).StatusCode);
        cliente.DefaultRequestHeaders.Accept.Clear();
        Assert.Equal(HttpStatusCode.Redirect, (await cliente.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Cookie_cifrada_sin_token_o_con_identidad_incoherente_se_rechaza()
    {
        using var fabrica = new FabricaWeb(fixture.Bd.Cadena);
        await fixture.RecuperacionAsync("prueba");
        await using var scope = fabrica.Services.CreateAsyncScope();
        var servicio = scope.ServiceProvider.GetRequiredService<AccesoService>();
        var sesion = (await servicio.IniciarSesionAsync("prueba", SeguridadWebFixture.ClavePrueba, new Origen("127.0.0.1", "Prueba"), codigo: SeguridadWebFixture.Codigo())).Valor!;
        var opciones = fabrica.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(Autenticacion.Esquema);
        foreach (var campo in new[] { Autenticacion.ClaimToken, Autenticacion.ClaimAdministrador, Autenticacion.ClaimSesion, ClaimTypes.Name })
        {
            var principal = Autenticacion.CrearPrincipal(sesion);
            var identidad = (ClaimsIdentity)principal.Identity!;
            identidad.RemoveClaim(identidad.FindFirst(campo)!);
            if (campo != Autenticacion.ClaimToken) identidad.AddClaim(new Claim(campo, "incoherente"));
            var cookie = opciones.TicketDataFormat.Protect(new AuthenticationTicket(principal, new AuthenticationProperties(), Autenticacion.Esquema));
            using var cliente = fabrica.Cliente(cookies: false);
            cliente.DefaultRequestHeaders.Add("Cookie", "PeluqueriaLucia.Panel=" + cookie);
            cliente.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.GetAsync(Autenticacion.RutaEstado)).StatusCode);
        }
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
            // Las direcciones anteriores a /admin llevan primero a su equivalente, que exige sesión
            if (!ruta.StartsWith("/admin", StringComparison.Ordinal))
            {
                Assert.Equal("/admin" + ruta, respuesta.Headers.Location!.ToString());
                respuesta = await cliente.GetAsync(respuesta.Headers.Location);
                Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
            }
            Assert.Contains("/admin/acceso", respuesta.Headers.Location!.ToString());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("/no-existe/tampoco")).StatusCode);
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
        { ["Actual"] = "actual incorrecta", ["Nueva"] = nueva, ["Confirmacion"] = nueva, ["Codigo"] = SeguridadWebFixture.Codigo(), ["__RequestVerificationToken"] = token }));
        Assert.Equal(HttpStatusCode.OK, rechazo.StatusCode);
        Assert.Contains("La contraseña actual no es correcta.", WebUtility.HtmlDecode(await rechazo.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, (await otra.GetAsync("/admin")).StatusCode);

        var respuesta = await actual.PostAsync("/admin/Seguridad/Contrasena", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Actual"] = SeguridadWebFixture.ClavePrueba, ["Nueva"] = nueva, ["Confirmacion"] = nueva, ["Codigo"] = SeguridadWebFixture.Codigo(), ["__RequestVerificationToken"] = token }));
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
