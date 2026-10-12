using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class AgendaTests
{
    private sealed class Reloj : TimeProvider { public DateTimeOffset Ahora=new(2026,10,10,8,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow()=>Ahora; }
    private static AgendaService Crear(PeluqueriaDbContext db, TimeProvider reloj) => new(new RepositorioAgenda(db),
        new PeluqueriaLucia.Infrastructure.Seguridad.ProtectorSecretos(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()), reloj);
    private static readonly DateOnly Dia=new(2026,10,12);
    private static SolicitarCita Datos(TimeOnly hora,string telefono="612345678", Guid? solicitud=null) => new(solicitud??Guid.NewGuid(),Dia,hora,telefono,"Ana prueba",null,"ip-prueba","ana@example.test",CanalConfirmacion.Correo,"Prueba",PeluqueriaLucia.Domain.Comun.Sexo.Otro);

    [Theory]
    [InlineData("612 345 678","+34612345678")]
    [InlineData("0034671234567","+34671234567")]
    [InlineData("+34 612-345-678","+34612345678")]
    [InlineData("912345678",null)]
    [InlineData("123",null)]
    [InlineData("+34612345678<script>",null)]
    public void Telefono_exige_movil_espanol_y_normaliza(string telefono,string? esperado) => Assert.Equal(esperado,AgendaService.NormalizarTelefono(telefono));

    [Fact]
    public async Task Solicitud_exige_apellidos_y_sexo_valido()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var agenda=Crear(db,new Reloj());
        var datos=Datos(new(10,0));
        Assert.False((await agenda.SolicitarAsync(datos with { Apellido="" },default)).Exito);
        Assert.False((await agenda.SolicitarAsync(datos with { Sexo=null },default)).Exito);
        Assert.False((await agenda.SolicitarAsync(datos with { Sexo=(PeluqueriaLucia.Domain.Comun.Sexo)99 },default)).Exito);
        Assert.Empty(await db.ReservasCitas.ToListAsync());
    }
    [Fact]
    public async Task Dos_peticiones_simultaneas_no_ocupan_el_mismo_hueco()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();
        async Task<bool> Pedir(string telefono){ await using var db=bd.Contexto(); return (await Crear(db,new Reloj()).SolicitarAsync(Datos(new(10,0),telefono),default)).Exito; }
        var resultados=await Task.WhenAll(Pedir("612345678"),Pedir("623456789"));
        Assert.Single(resultados,x=>x);
        Assert.Equal(1,await bd.EscalarAsync<int>("SELECT COUNT(*) FROM ReservasCitas"));
    }

    [Fact]
    public async Task Confirmacion_exige_telefono_no_solapa_y_genera_un_solo_aviso()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto();
        var reloj=new Reloj(); var agenda=Crear(db,reloj);
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
        Assert.Equal(EstadoNotificacion.Pendiente,(await db.MensajesCitas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Reenvio_es_idempotente_y_caducidad_libera_hueco()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto();
        var reloj=new Reloj(); var agenda=Crear(db,reloj); var datos=Datos(new(10,0));
        var a=await agenda.SolicitarAsync(datos,default); var b=await agenda.SolicitarAsync(datos,default);
        Assert.True(a.Exito); Assert.Equal(a.Valor,b.Valor); Assert.Equal(1,await db.ReservasCitas.CountAsync());
        Assert.DoesNotContain(new TimeOnly(10,0),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        reloj.Ahora=reloj.Ahora.AddHours(25);
        Assert.Contains(new TimeOnly(10,0),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
    }

    [Fact]
    public async Task Intervalo_configurable_y_trabajo_de_60_minutos_bloquea_hasta_las_11()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var tramo=await db.TramosAgenda.SingleAsync(x=>x.Dia==DayOfWeek.Monday && x.Desde==new TimeOnly(9,45));
        Assert.False((await agenda.GuardarHorarioAsync(tramo.Id,tramo.Dia,new(10,0),new(12,0),default,0)).Exito);
        Assert.True((await agenda.GuardarHorarioAsync(tramo.Id,tramo.Dia,new(10,0),new(12,0),default,20)).Exito);
        var disponibilidad=await agenda.DisponibilidadAsync(Dia,null,default);
        Assert.Contains(new TimeOnly(10,20),disponibilidad.Horas); Assert.DoesNotContain(new TimeOnly(10,15),disponibilidad.Horas);
        var admin=Administrador.Crear("intervalos","hash-prueba",reloj.Ahora.UtcDateTime);db.Add(admin);await db.SaveChangesAsync();
        var solicitud=await agenda.SolicitarAsync(Datos(new(10,0)),default);Assert.True(solicitud.Exito);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Confirmada,60,true,null,admin.Id),default)).Exito);
        disponibilidad=await agenda.DisponibilidadAsync(Dia,null,default);
        foreach(var hora in new[]{new TimeOnly(10,0),new(10,20),new(10,40)}){Assert.DoesNotContain(hora,disponibilidad.Horas);Assert.Contains(hora,disponibilidad.Ocupadas!);}
        Assert.Contains(new TimeOnly(11,0),disponibilidad.Horas);
        Assert.False((await agenda.SolicitarAsync(Datos(new(10,40),"623456789"),default)).Exito);
        Assert.True((await agenda.SolicitarAsync(Datos(new(11,0),"623456789"),default)).Exito);
    }
    [Fact]
    public async Task Duracion_no_necesita_ser_multiplo_del_intervalo_y_no_adelanta_el_final()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj();var agenda=Crear(db,reloj);
        var tramo=await db.TramosAgenda.SingleAsync(x=>x.Dia==DayOfWeek.Monday && x.Desde==new TimeOnly(9,45));
        Assert.True((await agenda.GuardarHorarioAsync(tramo.Id,tramo.Dia,new(10,0),new(12,0),default,20)).Exito);
        var admin=Administrador.Crear("duracion","hash-prueba",reloj.Ahora.UtcDateTime);db.Add(admin);await db.SaveChangesAsync();
        var solicitud=await agenda.SolicitarAsync(Datos(new(10,0)),default); var r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Confirmada,47,true,null,admin.Id),default)).Exito);
        var disponibilidad=await agenda.DisponibilidadAsync(Dia,null,default);
        Assert.DoesNotContain(new TimeOnly(10,40),disponibilidad.Horas); Assert.Contains(new TimeOnly(11,0),disponibilidad.Horas);
        Assert.Equal(new DateTime(2026,10,12,10,47,0),(await db.ReservasCitas.AsNoTracking().SingleAsync()).Fin);
    }
    [Fact]
    public async Task Dia_cerrado_no_admite_reservas_y_no_se_cierra_con_citas_activas()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var agenda=Crear(db,new Reloj());
        Assert.True((await agenda.CerrarDiaAsync(Dia,"Festivo",default)).Exito);
        Assert.Empty((await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        Assert.False((await agenda.SolicitarAsync(Datos(new(10,0)),default)).Exito);
        Assert.True((await agenda.AbrirDiaAsync((await db.CierresAgenda.SingleAsync()).Id,default)).Exito);
        Assert.True((await agenda.SolicitarAsync(Datos(new(10,0)),default)).Exito);
        Assert.False((await agenda.CerrarDiaAsync(Dia,"Vacaciones",default)).Exito);
    }

    [Fact]
    public async Task Cita_del_salon_nace_confirmada_ocupa_el_hueco_y_no_admite_solapes()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=Administrador.Crear("salon","hash-prueba",reloj.Ahora.UtcDateTime); db.Add(admin);
        var cliente=PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Marta","López","marta@example.test","612 000 111",PeluqueriaLucia.Domain.Comun.Sexo.Mujer,reloj.Ahora.UtcDateTime); db.Add(cliente);
        await db.SaveChangesAsync();
        var cita=await agenda.CrearCitaAsync(new(Dia,new(10,0),60,cliente.Id,null,null,null,"Trae foto",false,admin.Id),default);
        Assert.True(cita.Exito);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync();
        Assert.Equal((EstadoCita.Confirmada,OrigenCita.Panel,cliente.Id,"+34612000111"),(r.Estado,r.Origen,r.ClienteId!.Value,r.Telefono));
        Assert.Empty(await db.MensajesCitas.ToListAsync());
        Assert.DoesNotContain(new TimeOnly(10,30),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        Assert.False((await agenda.SolicitarAsync(Datos(new(10,30)),default)).Exito);
        var solape=await agenda.CrearCitaAsync(new(Dia,new(10,45),30,null,"Otra","600000000",null,null,false,admin.Id),default);
        Assert.False(solape.Exito); Assert.Contains("10:00",solape.Error!.Mensaje);
        // Fuera del horario público también se puede anotar (horas extra), pero no en un día cerrado.
        Assert.True((await agenda.CrearCitaAsync(new(Dia,new(20,0),30,null,"Vecina",null,null,null,false,admin.Id),default)).Exito);
        Assert.True((await agenda.CerrarDiaAsync(Dia.AddDays(1),"Festivo",default)).Exito);
        Assert.False((await agenda.CrearCitaAsync(new(Dia.AddDays(1),new(10,0),30,null,"Ana",null,null,null,false,admin.Id),default)).Exito);
    }

    [Fact]
    public async Task Bloqueo_quita_horas_de_la_web_y_al_quitarlo_se_liberan()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=Administrador.Crear("bloqueos","hash-prueba",reloj.Ahora.UtcDateTime); db.Add(admin); await db.SaveChangesAsync();
        Assert.True((await agenda.CrearCitaAsync(new(Dia,new(11,0),45,null,"Médico",null,null,null,true,admin.Id),default)).Exito);
        Assert.DoesNotContain(new TimeOnly(11,15),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync();
        Assert.Equal((OrigenCita.Bloqueo,"Médico"),(r.Origen,r.Nombre));
        Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Cancelada,45,false,null,admin.Id),default)).Exito);
        Assert.Contains(new TimeOnly(11,15),(await agenda.DisponibilidadAsync(Dia,null,default)).Horas);
        Assert.Empty(await db.MensajesCitas.ToListAsync());
    }

    [Fact]
    public async Task Confirmar_directo_salta_el_enlace_crea_la_ficha_y_solo_informa()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=Administrador.Crear("directo","hash-prueba",reloj.Ahora.UtcDateTime); db.Add(admin); await db.SaveChangesAsync();
        var solicitud=await agenda.SolicitarAsync(Datos(new(10,0)),default);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.False((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Confirmada,30,false,null,admin.Id,true),default)).Exito);
        Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Confirmada,30,true,null,admin.Id,true),default)).Exito);
        r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.Equal(EstadoCita.Confirmada,r.Estado); Assert.Null(r.ConfirmacionHash); Assert.NotNull(r.ClienteId);
        Assert.True((await db.Clientes.AsNoTracking().SingleAsync(x=>x.Id==r.ClienteId)).PendienteCompletar);
        var aviso=await db.MensajesCitas.AsNoTracking().SingleAsync();
        Assert.Null(aviso.TokenProtegido); Assert.Contains("queda confirmada",aviso.Texto);
    }
}
