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
        // Las citas solo van en horas libres del horario de apertura: fuera de él ya no se anotan (el tiempo bloqueado sí)
        var fuera=await agenda.CrearCitaAsync(new(Dia,new(20,0),30,null,"Vecina",null,null,null,false,admin.Id),default);
        Assert.False(fuera.Exito); Assert.Equal(("Elige una de las horas libres de ese día.","Hora"),(fuera.Error!.Mensaje,fuera.Error.Campo));
        Assert.True((await agenda.CrearCitaAsync(new(Dia,new(11,0),30,null,"Vecina",null,null,null,false,admin.Id),default)).Exito);
        Assert.True((await agenda.CrearCitaAsync(new(Dia,new(20,0),30,null,"Formación",null,null,null,true,admin.Id),default)).Exito);
        Assert.True((await agenda.CerrarDiaAsync(Dia.AddDays(1),"Festivo",default)).Exito);
        Assert.False((await agenda.CrearCitaAsync(new(Dia.AddDays(1),new(10,0),30,null,"Ana",null,null,null,false,admin.Id),default)).Exito);
    }

    [Fact]
    public async Task Huecos_del_panel_son_las_horas_libres_del_horario_para_esa_duracion()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=Administrador.Crear("huecos","hash-prueba",reloj.Ahora.UtcDateTime); db.Add(admin); await db.SaveChangesAsync();
        Assert.True((await agenda.CrearCitaAsync(new(Dia,new(10,0),60,null,"Marta",null,null,null,false,admin.Id),default)).Exito);
        var media=await agenda.HuecosPanelAsync(Dia,30,default);
        Assert.Contains(new TimeOnly(11,0),media.Horas);
        // 9:45 durante media hora pisaría la cita de las 10:00; de 10:00 a 10:45 están ocupadas
        foreach(var h in new[]{new TimeOnly(9,45),new TimeOnly(10,0),new TimeOnly(10,45)}) { Assert.DoesNotContain(h,media.Horas); Assert.Contains(h,media.Ocupadas!); }
        Assert.DoesNotContain(new TimeOnly(20,0),media.Horas);
        // Con dos horas solo cabe empezar a las 11:00 (el tramo de la mañana acaba a las 13:00)
        var larga=await agenda.HuecosPanelAsync(Dia,120,default);
        Assert.Contains(new TimeOnly(11,0),larga.Horas); Assert.DoesNotContain(new TimeOnly(11,15),larga.Horas);
        Assert.Empty((await agenda.HuecosPanelAsync(Dia.AddDays(-5),30,default)).Horas);
        Assert.Empty((await agenda.HuecosPanelAsync(Dia,0,default)).Horas);
        var pasada=await agenda.CrearCitaAsync(new(Dia.AddDays(-5),new(10,0),30,null,"Ana",null,null,null,false,admin.Id),default);
        Assert.Equal(("Las citas se dan de hoy en adelante.","Dia"),(pasada.Error!.Mensaje,pasada.Error.Campo));
    }

    [Fact]
    public async Task Dualidad_permite_dar_citas_a_la_misma_hora_salvo_en_el_tiempo_bloqueado()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=Administrador.Crear("dualidad","hash-prueba",reloj.Ahora.UtcDateTime); db.Add(admin);
        var tinte=PeluqueriaLucia.Domain.Catalogo.Servicio.Crear("Tinte",30m,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.Now); tinte.CambiarDualidad(true);
        var corte=PeluqueriaLucia.Domain.Catalogo.Servicio.Crear("Corte",18m,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.Now);
        db.AddRange(tinte,corte); await db.SaveChangesAsync();
        CitaManual Cita(TimeOnly hora,int minutos,int? servicio,bool bloqueo=false) => new(Dia,hora,minutos,null,bloqueo?"Médico":"Clienta",null,servicio,null,bloqueo,admin.Id);

        Assert.True((await agenda.CrearCitaAsync(Cita(new(10,0),60,corte.Id),default)).Exito);
        // Un servicio sin dualidad no puede pisar otra cita; uno con dualidad sí
        Assert.False((await agenda.CrearCitaAsync(Cita(new(10,30),30,corte.Id),default)).Exito);
        Assert.DoesNotContain(new TimeOnly(10,30),(await agenda.HuecosPanelAsync(Dia,30,default)).Horas);
        Assert.Contains(new TimeOnly(10,30),(await agenda.HuecosPanelAsync(Dia,30,default,tinte.Id)).Horas);
        var dual=await agenda.CrearCitaAsync(Cita(new(10,30),30,tinte.Id),default);
        Assert.True(dual.Exito,dual.Error?.Mensaje);
        Assert.True((await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==dual.Valor)).Dualidad);
        // Y una cita normal puede coincidir con una que tiene dualidad
        Assert.True((await agenda.CrearCitaAsync(Cita(new(12,0),30,tinte.Id),default)).Exito);
        Assert.True((await agenda.CrearCitaAsync(Cita(new(12,0),30,corte.Id),default)).Exito);
        // El tiempo bloqueado choca con todo, también con las citas con dualidad
        Assert.False((await agenda.CrearCitaAsync(Cita(new(12,0),30,null,bloqueo:true),default)).Exito);
        Assert.True((await agenda.CrearCitaAsync(Cita(new(12,30),30,null,bloqueo:true),default)).Exito);
        Assert.False((await agenda.CrearCitaAsync(Cita(new(12,30),30,tinte.Id),default)).Exito);
        Assert.DoesNotContain(new TimeOnly(12,30),(await agenda.HuecosPanelAsync(Dia,30,default,tinte.Id)).Horas);
    }

    [Fact]
    public async Task Cambiar_la_dualidad_del_servicio_cambia_sus_citas_por_venir_y_no_las_pasadas()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto();
        var tinte=PeluqueriaLucia.Domain.Catalogo.Servicio.Crear("Tinte",30m,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.Now);
        db.Add(tinte); await db.SaveChangesAsync();
        ReservaCita Cita(DateTime inicio,EstadoCita estado) => new() { Id=Guid.NewGuid(),SolicitudId=Guid.NewGuid(),Inicio=inicio,Fin=inicio.AddMinutes(30),Nombre="Ana",
            ServicioId=tinte.Id,ServicioNombre="Tinte",Estado=estado,Origen=OrigenCita.Panel,CreadaUtc=DateTime.UtcNow,ActualizadaUtc=DateTime.UtcNow,CaducaUtc=DateTime.UtcNow };
        var pasada=Cita(DateTime.Now.AddDays(-2),EstadoCita.Completada); var futura=Cita(DateTime.Now.AddDays(2),EstadoCita.Confirmada); var cancelada=Cita(DateTime.Now.AddDays(3),EstadoCita.Cancelada);
        db.AddRange(pasada,futura,cancelada); await db.SaveChangesAsync();
        var imagenes=new PeluqueriaLucia.Infrastructure.Almacenamiento.AlmacenImagenesLocal(
            Microsoft.Extensions.Options.Options.Create(new PeluqueriaLucia.Infrastructure.Almacenamiento.OpcionesImagenes { Carpeta=Path.Combine(Path.GetTempPath(),"peluqueria-pruebas") }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PeluqueriaLucia.Infrastructure.Almacenamiento.AlmacenImagenesLocal>.Instance);
        var catalogo=new PeluqueriaLucia.Application.Catalogo.CatalogoService(new ServicioRepositorio(db),imagenes,db,TimeProvider.System);

        var resultado=await catalogo.ActualizarAsync(tinte.Id,new("Tinte",30m,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,Dualidad:true),null,false);

        Assert.True(resultado.Exito,resultado.Error?.Mensaje);
        await using var otra=bd.Contexto();
        Assert.True((await otra.Servicios.AsNoTracking().SingleAsync(x=>x.Id==tinte.Id)).Dualidad);
        var citas=await otra.ReservasCitas.AsNoTracking().ToDictionaryAsync(x=>x.Id,x=>x.Dualidad);
        Assert.Equal((false,true,false),(citas[pasada.Id],citas[futura.Id],citas[cancelada.Id]));
    }

    [Fact]
    public async Task Solicitud_sin_servicio_de_la_lista_guarda_lo_que_escribe_el_cliente()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var agenda=Crear(db,new Reloj());
        var larga=await agenda.SolicitarAsync(Datos(new(10,0)) with { ServicioOtro=new string('a',AgendaService.LongitudServicioOtro+1) },default);
        Assert.False(larga.Exito); Assert.Equal("ServicioOtro",larga.Error!.Campo);
        Assert.True((await agenda.SolicitarAsync(Datos(new(10,0)) with { ServicioOtro="  ¿Hacéis alisado\n de   keratina?  " },default)).Exito);
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync();
        Assert.Equal(("Consulta: ¿Hacéis alisado de keratina?",(int?)null),(r.ServicioNombre,r.ServicioId));
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

    [Fact]
    public async Task Eliminar_solo_quita_citas_cerradas_y_se_lleva_sus_avisos()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync(); await using var db=bd.Contexto(); var reloj=new Reloj(); var agenda=Crear(db,reloj);
        var admin=PeluqueriaLucia.Domain.Seguridad.Administrador.Crear("agenda","hash",reloj.Ahora.UtcDateTime); db.Add(admin); await db.SaveChangesAsync();
        var solicitud=await agenda.SolicitarAsync(Datos(new(10,0)),default); Assert.True(solicitud.Exito,solicitud.Error?.Mensaje);
        Assert.False((await agenda.EliminarCitaAsync(solicitud.Valor,default)).Exito,"una solicitud abierta no se elimina");
        var r=await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.True((await agenda.GestionarAsync(new(r.Id,Convert.ToBase64String(r.Version),EstadoCita.Rechazada,30,true,null,admin.Id),default)).Exito);
        Assert.Single(await db.MensajesCitas.AsNoTracking().Where(x=>x.ReservaId==r.Id).ToListAsync());
        Assert.True((await agenda.EliminarCitaAsync(r.Id,default)).Exito);
        Assert.Empty(await db.ReservasCitas.AsNoTracking().ToListAsync()); Assert.Empty(await db.MensajesCitas.AsNoTracking().ToListAsync());
        Assert.False((await agenda.EliminarCitaAsync(r.Id,default)).Exito,"ya no existe");
    }
}
