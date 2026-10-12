using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

public sealed class AgendaWebTests
{
    private sealed class Reloj : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026,10,10,8,0,0,TimeSpan.Zero); }
    [Fact]
    public async Task Autocompletado_publico_no_revela_ficha_sin_codigo_y_exige_antifalsificacion()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var fabrica=new FabricaWeb(bd.Cadena);using var navegador=fabrica.Cliente();
        await using var db=bd.Contexto();var cliente=PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Ana privada","Apellidos privados","privado@example.test","+34612345678",PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.UtcNow);db.Add(cliente);await db.SaveChangesAsync();
        var html=await navegador.GetStringAsync("/citas");Assert.Contains("Apellidos",html);Assert.Contains("sexo-agenda-Mujer",html);Assert.Contains("sexo-agenda-Hombre",html);Assert.Contains("sexo-agenda-Otro",html);
        Assert.DoesNotContain("Ana privada",html);Assert.DoesNotContain("privado@example.test",html);
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.BadRequest,(await navegador.PostAsync("/citas/contacto/solicitar",new FormUrlEncodedContent(new Dictionary<string,string>{["telefono"]="612345678"}))).StatusCode);
        var respuesta=await navegador.PostAsync("/citas/contacto/solicitar",new FormUrlEncodedContent(new Dictionary<string,string>{["telefono"]="612345678",["__RequestVerificationToken"]=token}));Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        var texto=await respuesta.Content.ReadAsStringAsync();Assert.DoesNotContain("privado@example.test",texto);Assert.DoesNotContain("Ana privada",texto);
        using var json=System.Text.Json.JsonDocument.Parse(texto);Assert.Single(json.RootElement.EnumerateObject());var id=json.RootElement.GetProperty("id").GetGuid();
        var reto=await db.Set<PeluqueriaLucia.Domain.Agenda.VerificacionContacto>().AsNoTracking().SingleAsync(x=>x.Id==id);
        await using var scope=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateAsyncScope(fabrica.Services);
        var protector=Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PeluqueriaLucia.Application.Abstracciones.IProtectorSecretos>(scope.ServiceProvider);
        var codigo=protector.Leer(reto.CodigoProtegido!,"contacto-codigo")!;
        Assert.Equal(HttpStatusCode.BadRequest,(await navegador.PostAsync("/citas/contacto/verificar",new FormUrlEncodedContent(new Dictionary<string,string>{["id"]=id.ToString(),["codigo"]=codigo}))).StatusCode);
        var datos=new Dictionary<string,string>{["id"]=id.ToString(),["codigo"]=codigo,["__RequestVerificationToken"]=token};
        respuesta=await navegador.PostAsync("/citas/contacto/verificar",new FormUrlEncodedContent(datos));Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);Assert.Contains("privado@example.test",await respuesta.Content.ReadAsStringAsync());Assert.Contains("no-store",respuesta.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.BadRequest,(await navegador.PostAsync("/citas/contacto/verificar",new FormUrlEncodedContent(datos))).StatusCode);
    }
    [Fact]
    public async Task Enlace_publico_exige_post_antifalsificacion_y_no_expone_contacto()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var fabrica = new FabricaWeb(bd.Cadena, reloj: new Reloj());
        using var cliente = fabrica.Cliente();
        string enlace;
        await using (var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateAsyncScope(fabrica.Services))
        {
            var db = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PeluqueriaLucia.Infrastructure.Persistencia.PeluqueriaDbContext>(scope.ServiceProvider);
            var agenda = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PeluqueriaLucia.Application.Agenda.IAgendaService>(scope.ServiceProvider);
            var secretos = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<PeluqueriaLucia.Application.Abstracciones.IProtectorSecretos>(scope.ServiceProvider);
            var admin = PeluqueriaLucia.Domain.Seguridad.Administrador.Crear("enlace", "hash-prueba", DateTime.UtcNow); db.Add(admin); await db.SaveChangesAsync();
            var solicitud = await agenda.SolicitarAsync(new(Guid.NewGuid(),new(2026,10,12),new(10,0),"612345678","Ana",null,"ip","ana@example.test",PeluqueriaLucia.Domain.Agenda.CanalConfirmacion.Correo,"Prueba",PeluqueriaLucia.Domain.Comun.Sexo.Mujer),default);
            var r = (await agenda.ListarAsync(new(2026,10,12),new(2026,10,12),default)).Single();
            Assert.True(solicitud.Exito);
            Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),PeluqueriaLucia.Domain.Agenda.EstadoCita.Confirmada,30,true,null,admin.Id),default)).Exito);
            var m = (await agenda.MensajesAsync(default)).Single();
            enlace = "/citas/confirmar/" + secretos.Leer(m.TokenProtegido!,"confirmacion-cita");
        }
        var respuesta = await cliente.GetAsync(enlace); Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        Assert.Equal("no-referrer",respuesta.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("no-store",respuesta.Headers.CacheControl!.ToString());
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("Aceptar cita",html); Assert.DoesNotContain("ana@example.test",html); Assert.DoesNotContain("612345678",html);
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas WHERE Estado=5"));
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync(enlace,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        var token = WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.PostAsync(enlace,new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=token}))).StatusCode);
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas WHERE Estado=1 AND AceptadaUtc IS NOT NULL"));
        Assert.Equal(HttpStatusCode.Gone,(await cliente.GetAsync(enlace)).StatusCode);
    }
    [Fact]
    public async Task Publico_puede_solicitar_sin_ver_telefonos_y_admin_requiere_sesion()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var fabrica=new FabricaWeb(bd.Cadena); using var cliente=fabrica.Cliente();
        var ahora=TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid"));
        var dia=DateOnly.FromDateTime(ahora.DateTime).AddDays(2); while(dia.DayOfWeek==DayOfWeek.Sunday) dia=dia.AddDays(1);
        var html=await cliente.GetStringAsync("/citas?dia="+dia.ToString("yyyy-MM-dd"));
        Assert.DoesNotMatch("(?i)\\bstyle\\s*=", html);
        Assert.Contains("<li hidden>", html);
        var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var id=Regex.Match(html,"name=\"Formulario.SolicitudId\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var hora=Regex.Match(html,"name=\"Formulario.Hora\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token); Assert.NotEmpty(id); Assert.NotEmpty(hora);
        var invalida = await cliente.PostAsync("/citas", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = token, ["Formulario.SolicitudId"] = id,
            ["Formulario.Dia"] = dia.ToString("yyyy-MM-dd"), ["Formulario.Hora"] = hora,
            ["Formulario.Telefono"] = "incorrecto", ["Formulario.Consentimiento"] = "true"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var errores = await invalida.Content.ReadAsStringAsync();
        Assert.Contains("validation-summary-errors", errores);
        Assert.DoesNotMatch("(?i)\\bstyle\\s*=", errores);
        Assert.Contains("style-src 'self'", invalida.Headers.GetValues("Content-Security-Policy").Single());
        var r=await cliente.PostAsync("/citas",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=token,["Formulario.SolicitudId"]=id,["Formulario.Dia"]=dia.ToString("yyyy-MM-dd"),["Formulario.Hora"]=hora,["Formulario.Nombre"]="Ana",["Formulario.Apellido"]="Prueba",["Formulario.Sexo"]="Mujer",["Formulario.Correo"]="ana@example.test",["Formulario.Canal"]="Correo",["Formulario.Telefono"]="612345678",["Formulario.Consentimiento"]="true"}));
        Assert.Equal(HttpStatusCode.Redirect,r.StatusCode); Assert.Equal("/citas/recibida",r.Headers.Location!.ToString());
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas"));
        var disponibilidad=await cliente.GetStringAsync("/citas/disponibilidad?dia="+dia.ToString("yyyy-MM-dd"));
        Assert.DoesNotContain("612345678",disponibilidad);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin/agenda")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await cliente.GetAsync("/admin/agenda/avisos")).StatusCode);
        foreach(var ruta in new[]{"/admin/agenda/falta","/admin/agenda/corregir-falta","/admin/agenda/vincular-cliente","/admin/Clientes/Reconocer"})
            Assert.Equal(HttpStatusCode.Redirect,(await cliente.PostAsync(ruta,new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await cliente.PostAsync("/citas",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
    }
}
