using System.Text.RegularExpressions;
using Comunicaciones.Correo;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Infrastructure.Seguridad;

namespace PeluqueriaLucia.Tests.Integracion;
public sealed class VerificacionContactoTests
{
    private sealed class Reloj:TimeProvider {public DateTimeOffset Ahora=new(2026,10,11,10,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Ahora;}
    private sealed class Correo:IEnviadorCorreo
    {
        public bool Configurado {get;set;}=true;
        public ResultadoEnvio Resultado {get;set;}=ResultadoEnvio.Aceptado;
        public List<CorreoSaliente> Enviados {get;}=[];
        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente m,CancellationToken ct=default){Enviados.Add(m);return Task.FromResult(Resultado);}
    }
    private sealed class Caso(PeluqueriaDbContext db)
    {
        public Reloj Reloj {get;}=new();public Correo Correo {get;}=new();
        public ProtectorSecretos Protector {get;}=new(new EphemeralDataProtectionProvider());
        public VerificacionContactoService Servicio=>new(new RepositorioAgenda(db),Protector,Correo,Reloj);
        public async Task<Cliente> AltaAsync()
        {var c=Cliente.Crear("Ana","García","ana@example.test","0034 612 345 678",Sexo.Mujer,Reloj.Ahora.UtcDateTime);db.Add(c);await db.SaveChangesAsync();return c;}
        public string Codigo=>Regex.Match(Correo.Enviados.Last().Texto,@"código es ([0-9]{6})").Groups[1].Value;
    }
    [Fact]
    public async Task Codigo_al_correo_guardado_autocompleta_y_es_de_un_solo_uso()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        var id=await c.Servicio.SolicitarAsync("612345678","ip",default);
        var v=await db.Set<VerificacionContacto>().AsNoTracking().SingleAsync();Assert.NotNull(v.CodigoProtegido);Assert.NotNull(v.SecretoProtegido);
        await c.Servicio.ProcesarColaAsync(default);Assert.Equal("ana@example.test",Assert.Single(c.Correo.Enviados).Destinatario);
        Assert.Equal(6,c.Codigo.Length);Assert.False((await c.Servicio.VerificarAsync(id,"incorrecto",default)).Exito);
        var perfil=await c.Servicio.VerificarAsync(id,c.Codigo,default);Assert.True(perfil.Exito);Assert.Equal("García",perfil.Valor!.Apellido);Assert.Equal(Sexo.Mujer,perfil.Valor.Sexo);
        Assert.False((await c.Servicio.VerificarAsync(id,c.Codigo,default)).Exito);
        v=await db.Set<VerificacionContacto>().AsNoTracking().SingleAsync();Assert.NotNull(v.ConsumidaUtc);Assert.Null(v.SecretoProtegido);Assert.Null(v.CodigoProtegido);
    }
    [Fact]
    public async Task Con_otro_movil_se_reconoce_por_el_correo_y_el_codigo_va_a_la_ficha()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        // Móvil nuevo (no está en la ficha) y el correo de la ficha: se reconoce por el correo
        var id=await c.Servicio.SolicitarAsync("699999999","ip",default,"  ANA@example.test ");await c.Servicio.ProcesarColaAsync(default);
        var enviado=Assert.Single(c.Correo.Enviados);Assert.Equal("ana@example.test",enviado.Destinatario);
        Assert.Contains("Tu código es "+c.Codigo,enviado.Texto);Assert.Contains("Completa tus datos",enviado.Html!);
        Assert.Equal("+34612345678",(await db.Set<VerificacionContacto>().AsNoTracking().SingleAsync()).Telefono);
        var perfil=await c.Servicio.VerificarAsync(id,c.Codigo,default);Assert.True(perfil.Exito);Assert.Equal(("Ana","García"),(perfil.Valor!.Nombre,perfil.Valor.Apellido));
        // Solo con el correo también
        var soloCorreo=await c.Servicio.SolicitarAsync(null,"ip",default,"ana@example.test");await c.Servicio.ProcesarColaAsync(default);
        Assert.Equal(2,c.Correo.Enviados.Count);Assert.True((await c.Servicio.VerificarAsync(soloCorreo,c.Codigo,default)).Exito);
    }
    [Fact]
    public async Task Correo_desconocido_no_envia_nada_y_un_correo_no_recibe_mas_de_tres_codigos_por_hora()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        Assert.NotEqual(Guid.Empty,await c.Servicio.SolicitarAsync("699999999","ip-1",default,"nadie@example.test"));
        await c.Servicio.ProcesarColaAsync(default);Assert.Empty(c.Correo.Enviados);
        for(var i=0;i<4;i++){await c.Servicio.SolicitarAsync(null,"ip-"+i,default,"ana@example.test");await c.Servicio.ProcesarColaAsync(default);}
        Assert.Equal(3,c.Correo.Enviados.Count);
    }
    [Fact]
    public async Task Cinco_intentos_incorrectos_bloquean_un_codigo_correcto()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        var id=await c.Servicio.SolicitarAsync("612345678","ip",default);await c.Servicio.ProcesarColaAsync(default);
        for(int i=0;i<5;i++)Assert.False((await c.Servicio.VerificarAsync(id,"incorrecto",default)).Exito);
        Assert.False((await c.Servicio.VerificarAsync(id,c.Codigo,default)).Exito);Assert.Equal(5,(await db.Set<VerificacionContacto>().AsNoTracking().SingleAsync()).Intentos);
    }
    [Fact]
    public async Task Caducado_no_envia_y_nuevo_codigo_invalida_el_anterior()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        var anterior=await c.Servicio.SolicitarAsync("612345678","ip",default);
        var id=await c.Servicio.SolicitarAsync("612345678","ip",default);await c.Servicio.ProcesarColaAsync(default);Assert.Single(c.Correo.Enviados);
        Assert.False((await c.Servicio.VerificarAsync(anterior,c.Codigo,default)).Exito);
        c.Reloj.Ahora=c.Reloj.Ahora.AddMinutes(11);Assert.False((await c.Servicio.VerificarAsync(id,c.Codigo,default)).Exito);
        var caducado=await c.Servicio.SolicitarAsync("612345678","ip",default);c.Reloj.Ahora=c.Reloj.Ahora.AddMinutes(11);await c.Servicio.ProcesarColaAsync(default);
        Assert.Single(c.Correo.Enviados);Assert.False((await c.Servicio.VerificarAsync(caducado,c.Codigo,default)).Exito);
    }
    [Fact]
    public async Task Telefono_ambiguo_o_nuevo_no_revela_fichas_ni_envia_correo()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();
        db.Add(Cliente.Crear("Otra","Persona","otro@example.test","+34612345678",Sexo.Otro,DateTime.UtcNow));await db.SaveChangesAsync();
        var ambiguo=await c.Servicio.SolicitarAsync("612345678","ip",default);var nuevo=await c.Servicio.SolicitarAsync("623456789","ip",default);
        Assert.NotEqual(Guid.Empty,ambiguo);Assert.NotEqual(Guid.Empty,nuevo);await c.Servicio.ProcesarColaAsync(default);Assert.Empty(c.Correo.Enviados);
        Assert.False((await c.Servicio.VerificarAsync(ambiguo,"123456",default)).Exito);Assert.False((await c.Servicio.VerificarAsync(nuevo,"123456",default)).Exito);
    }
    [Fact]
    public async Task Cambiar_contacto_de_ficha_invalida_verificacion_y_resultado_incierto_no_reenvia()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);var cliente=await c.AltaAsync();var clienteId=cliente.Id;
        var id=await c.Servicio.SolicitarAsync("612345678","ip",default);c.Correo.Resultado=ResultadoEnvio.Incierto;
        await c.Servicio.ProcesarColaAsync(default);await c.Servicio.ProcesarColaAsync(default);Assert.Single(c.Correo.Enviados);
        cliente=await db.Clientes.SingleAsync(x=>x.Id==clienteId);cliente.Actualizar(cliente.Nombre,cliente.Apellido,"nuevo@example.test",cliente.Telefono,cliente.Sexo);await db.SaveChangesAsync();
        Assert.False((await c.Servicio.VerificarAsync(id,c.Codigo,default)).Exito);
    }
    [Fact]
    public async Task Limite_por_telefono_y_smtp_desactivado_no_envian_mensajes()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Caso(db);await c.AltaAsync();c.Correo.Configurado=false;
        for(int i=0;i<4;i++)Assert.NotEqual(Guid.Empty,await c.Servicio.SolicitarAsync("612345678","ip",default));
        Assert.Equal(3,await db.Set<VerificacionContacto>().CountAsync());await c.Servicio.ProcesarColaAsync(default);Assert.Empty(c.Correo.Enviados);Assert.False(c.Servicio.Disponible);
    }
}
