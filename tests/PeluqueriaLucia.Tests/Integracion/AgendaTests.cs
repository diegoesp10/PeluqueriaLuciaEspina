using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class AgendaTests
{
    private sealed class Reloj : TimeProvider { public DateTimeOffset Ahora=new(2026,10,10,8,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Ahora; }
    private static readonly DateOnly Dia=new(2026,10,12);
    private static SolicitarCita Datos(TimeOnly hora,string telefono="612345678", Guid? solicitud=null) => new(solicitud??Guid.NewGuid(),Dia,hora,telefono,"Ana prueba",null,"ip-prueba");

    [Theory]
    [InlineData("612 345 678","+34612345678")]
    [InlineData("0034671234567","+34671234567")]
    [InlineData("+34 612-345-678","+34612345678")]
    [InlineData("912345678",null)]
    [InlineData("123",null)]
    [InlineData("+34612345678<script>",null)]
    public void Telefono_exige_movil_espanol_y_normaliza(string telefono,string? esperado) => Assert.Equal(esperado,AgendaService.NormalizarTelefono(telefono));

    [Fact]
    public async Task Dos_peticiones_simultaneas_no_ocupan_el_mismo_hueco()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();
        async Task<bool> Pedir(string telefono){ await using var db=bd.Contexto(); return (await new AgendaService(db,new Reloj()).SolicitarAsync(Datos(new(10,0),telefono),default)).Exito; }
        var resultados=await Task.WhenAll(Pedir("612345678"),Pedir("623456789"));
        Assert.Single(resultados,x=>x);
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas"));
    }

    [Fact]
    public async Task Confirmacion_exige_telefono_no_solapa_y_genera_un_solo_aviso()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto();
        var reloj=new Reloj(); var agenda=new AgendaService(db,reloj);
        var admin=Administrador.Crear("prueba","hash-prueba",reloj.Ahora.UtcDateTime); db.Administradores.Add(admin); await db.SaveChangesAsync();
        var primera=await agenda.SolicitarAsync(Datos(new(10,0)),default); Assert.True(primera.Exito);
        var segunda=await agenda.SolicitarAsync(Datos(new(10,30),"623456789"),default); Assert.True(segunda.Exito);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==primera.Valor);
        var version=Convert.ToBase64String(r.Version);
        Assert.False((await agenda.GestionarAsync(new(r.Id,version,EstadoCita.Confirmada,30,false,null,admin.Id),default)).Exito);
        Assert.False((await agenda.GestionarAsync(new(r.Id,version,EstadoCita.Confirmada,60,true,null,admin.Id),default)).Exito);
        Assert.True((await agenda.GestionarAsync(new(r.Id,version,EstadoCita.Confirmada,30,true,null,admin.Id),default)).Exito);
        Assert.False((await agenda.GestionarAsync(new(r.Id,version,EstadoCita.Confirmada,30,true,null,admin.Id),default)).Exito);
        Assert.Equal(1,await db.MensajesCitas.CountAsync());
        Assert.Equal(EstadoSms.Pendiente,(await db.MensajesCitas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Reenvio_es_idempotente_y_caducidad_libera_hueco()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto();
        var reloj=new Reloj(); var agenda=new AgendaService(db,reloj); var datos=Datos(new(10,0));
        var a=await agenda.SolicitarAsync(datos,default); var b=await agenda.SolicitarAsync(datos,default);
        Assert.True(a.Exito); Assert.Equal(a.Valor,b.Valor); Assert.Equal(1,await db.ReservasCitas.CountAsync());
        Assert.DoesNotContain(new TimeOnly(10,0),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        reloj.Ahora=reloj.Ahora.AddHours(25);
        Assert.Contains(new TimeOnly(10,0),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
    }

    [Fact]
    public async Task Dia_cerrado_no_admite_reservas_y_no_se_cierra_con_citas_activas()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var agenda=new AgendaService(db,new Reloj());
        Assert.True((await agenda.CerrarDiaAsync(Dia,"Festivo",default)).Exito);
        Assert.Empty((await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        Assert.False((await agenda.SolicitarAsync(Datos(new(10,0)),default)).Exito);
        Assert.True((await agenda.AbrirDiaAsync((await db.CierresAgenda.SingleAsync()).Id,default)).Exito);
        Assert.True((await agenda.SolicitarAsync(Datos(new(10,0)),default)).Exito);
        Assert.False((await agenda.CerrarDiaAsync(Dia,"Vacaciones",default)).Exito);
    }
}
