using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Infrastructure.Seguridad;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class ComunicacionesCitasTests
{
    private sealed class Reloj : TimeProvider
    { public DateTimeOffset Ahora = new(2026,10,10,8,0,0,TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Ahora; }
    private sealed class Url : IUrlPublica { public Uri? Obtener() => new("https://salon.example.test/"); }
    private sealed class Correo : IEnviadorCorreo
    {
        public bool Configurado { get; set; } = true;
        public ResultadoEnvio Resultado { get; set; } = ResultadoEnvio.Aceptado;
        public List<CorreoSaliente> Enviados { get; } = [];
        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct = default)
        { Enviados.Add(correo); return Task.FromResult(Resultado); }
    }
    private sealed class Contexto(PeluqueriaDbContext db)
    {
        public Reloj Reloj { get; } = new();
        public Correo Correo { get; } = new();
        public ProtectorSecretos Protector { get; } = new(new EphemeralDataProtectionProvider());
        public AgendaService Agenda => new(new RepositorioAgenda(db), Protector, Reloj);
        public ComunicacionesCitas Comunicaciones => new(new RepositorioAgenda(db), Protector, Correo, new WhatsAppManual(), new Url(), Reloj);
        public async Task<(Guid Id, string Token)> ProponerAsync(CanalConfirmacion canal)
        {
            var admin = Administrador.Crear("comunicaciones", "hash-prueba", Reloj.Ahora.UtcDateTime);
            db.Add(admin); await db.SaveChangesAsync();
            var reserva = await Agenda.SolicitarAsync(new(Guid.NewGuid(), new(2026,10,12), new(10,0), "612345678", "Ana", null, "ip", "ana@example.test", canal), default);
            Assert.True(reserva.Exito, reserva.Error?.Mensaje);
            var r = await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == reserva.Valor);
            Assert.True((await Agenda.GestionarAsync(new(r.Id, Convert.ToBase64String(r.Version), EstadoCita.Confirmada, 30, true, null, admin.Id), default)).Exito);
            var m = await db.MensajesCitas.AsNoTracking().SingleAsync();
            return (r.Id, Protector.Leer(m.TokenProtegido!, "confirmacion-cita")!);
        }
    }
    [Theory]
    [InlineData(CanalConfirmacion.Correo)]
    [InlineData(CanalConfirmacion.WhatsApp)]
    public async Task Consultar_no_acepta_y_aceptar_solo_funciona_una_vez(CanalConfirmacion canal)
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (id, token) = await c.ProponerAsync(canal);
        Assert.NotNull(await c.Comunicaciones.ConsultarPropuestaAsync(token, default));
        Assert.Empty(await db.Clientes.ToListAsync());
        Assert.Equal(EstadoCita.PendienteAceptacion, (await db.ReservasCitas.AsNoTracking().SingleAsync()).Estado);
        Assert.True((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        Assert.False((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        var r = await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(EstadoCita.Confirmada, r.Estado); Assert.NotNull(r.AceptadaUtc); Assert.Null(r.ConfirmacionHash);
        var cliente = Assert.Single(await db.Clientes.ToListAsync());
        Assert.Equal(cliente.Id, r.ClienteId); Assert.Equal("Ana", cliente.Apodo); Assert.True(cliente.PendienteCompletar);
    }
    [Fact]
    public async Task Reutiliza_solo_coincidencia_de_ambos_contactos_sin_sobrescribir_ficha()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var existente = PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Nombre real", "Apellidos", "ANA@example.test", "0034 612 345 678", PeluqueriaLucia.Domain.Comun.Sexo.Mujer, DateTime.UtcNow);
        db.Add(existente); await db.SaveChangesAsync(); var clienteId = existente.Id;
        var (_, token) = await c.ProponerAsync(CanalConfirmacion.Correo);
        Assert.True((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        var cliente = Assert.Single(await db.Clientes.AsNoTracking().ToListAsync());
        Assert.Equal("Nombre real", cliente.Nombre); Assert.False(cliente.PendienteCompletar);
        Assert.Equal(clienteId, (await db.ReservasCitas.AsNoTracking().SingleAsync()).ClienteId);
    }
    [Fact]
    public async Task Reconocimiento_administrativo_exige_ambos_contactos_y_devuelve_datos_de_ficha()
    {
        await using var bd=await BaseDeDatosPrueba.CrearAsync();await using var db=bd.Contexto();var c=new Contexto(db);
        var cliente=PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Ana","García","ANA@example.test","0034 612 345 678",PeluqueriaLucia.Domain.Comun.Sexo.Mujer,DateTime.UtcNow);
        db.Add(cliente);await db.SaveChangesAsync();
        var servicio=new PeluqueriaLucia.Application.Clientes.ClientesService(new ClienteRepositorio(db),db,c.Reloj);
        Assert.Null(await servicio.ReconocerAsync("612345678","otro@example.test"));
        Assert.Null(await servicio.ReconocerAsync("623456789","ana@example.test"));
        var ficha=await servicio.ReconocerAsync("+34 612345678","ana@example.test");
        Assert.NotNull(ficha);Assert.Equal(cliente.Id,ficha.Id);Assert.Equal("García",ficha.Apellido);Assert.Equal(PeluqueriaLucia.Domain.Comun.Sexo.Mujer,ficha.Sexo);
    }
    [Fact]
    public async Task Completar_ficha_conserva_apodo_y_una_cita_impide_eliminar_cliente()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (_,token) = await c.ProponerAsync(CanalConfirmacion.Correo); Assert.True((await c.Comunicaciones.AceptarAsync(token,default)).Exito);
        var cliente = await db.Clientes.AsNoTracking().SingleAsync();
        var servicio = new PeluqueriaLucia.Application.Clientes.ClientesService(new ClienteRepositorio(db),db,c.Reloj);
        Assert.False((await servicio.EliminarAsync(cliente.Id)).Exito);
        Assert.True((await servicio.ActualizarAsync(cliente.Id,new("Ana","García",cliente.Email,cliente.Telefono,PeluqueriaLucia.Domain.Comun.Sexo.Mujer,cliente.Apodo))).Exito);
        cliente = await db.Clientes.AsNoTracking().SingleAsync(); Assert.False(cliente.PendienteCompletar); Assert.Equal("Ana",cliente.Apodo); Assert.Equal("García",cliente.Apellido);
    }
    [Fact]
    public async Task Dos_aceptaciones_concurrentes_del_mismo_contacto_crean_una_sola_ficha()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (_, token1) = await c.ProponerAsync(CanalConfirmacion.Correo);
        var admin = await db.Administradores.AsNoTracking().SingleAsync();
        var solicitud = await c.Agenda.SolicitarAsync(new(Guid.NewGuid(),new(2026,10,12),new(10,30),"612345678","Otro apodo",null,"otra-ip","ana@example.test",CanalConfirmacion.WhatsApp),default);
        Assert.True(solicitud.Exito,solicitud.Error?.Mensaje);
        var segunda = await db.ReservasCitas.AsNoTracking().SingleAsync(x=>x.Id==solicitud.Valor);
        Assert.True((await c.Agenda.GestionarAsync(new(segunda.Id,Convert.ToBase64String(segunda.Version),EstadoCita.Confirmada,30,true,null,admin.Id),default)).Exito);
        var m = await db.MensajesCitas.AsNoTracking().SingleAsync(x=>x.ReservaId==segunda.Id);
        var token2 = c.Protector.Leer(m.TokenProtegido!,"confirmacion-cita")!;
        await using var otroDb = bd.Contexto();
        var otraComunicacion = new ComunicacionesCitas(new RepositorioAgenda(otroDb),c.Protector,c.Correo,new WhatsAppManual(),new Url(),c.Reloj);
        var resultados = await Task.WhenAll(c.Comunicaciones.AceptarAsync(token1,default),otraComunicacion.AceptarAsync(token2,default));
        Assert.All(resultados,x=>Assert.True(x.Exito,x.Error?.Mensaje));
        Assert.Single(await db.Clientes.AsNoTracking().ToListAsync());
        var ids = await db.ReservasCitas.AsNoTracking().Select(x=>x.ClienteId).ToListAsync(); Assert.NotNull(ids[0]); Assert.Equal(ids[0],ids[1]);
    }
    [Fact]
    public async Task Coincidencia_parcial_confirma_sin_duplicar_y_requiere_revision_administrativa()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var existente = PeluqueriaLucia.Domain.Clientes.Cliente.Crear("Otra", "Persona", "otro@example.test", "612345678", PeluqueriaLucia.Domain.Comun.Sexo.Otro, DateTime.UtcNow);
        db.Add(existente); await db.SaveChangesAsync(); var clienteId = existente.Id;
        var (id, token) = await c.ProponerAsync(CanalConfirmacion.WhatsApp);
        Assert.True((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        var r = await db.ReservasCitas.AsNoTracking().SingleAsync();
        Assert.Equal(EstadoCita.Confirmada, r.Estado); Assert.Null(r.ClienteId); Assert.True(r.PendienteVinculacion); Assert.Single(await db.Clientes.ToListAsync());
        var servicio = new ClientesAgendaService(new RepositorioAgenda(db), c.Reloj);
        var admin = await db.Administradores.AsNoTracking().SingleAsync();
        Assert.False((await servicio.ResolverAsync(id,Convert.ToBase64String(r.Version),clienteId,false,admin.Id,default)).Exito);
        Assert.True((await servicio.ResolverAsync(id,Convert.ToBase64String(r.Version),clienteId,true,admin.Id,default)).Exito);
        Assert.Equal(clienteId,(await db.ReservasCitas.AsNoTracking().SingleAsync()).ClienteId);
    }
    [Fact]
    public async Task Falta_solo_despues_de_cita_sin_duplicados_y_correccion_con_auditoria()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (id, token) = await c.ProponerAsync(CanalConfirmacion.Correo); Assert.True((await c.Comunicaciones.AceptarAsync(token,default)).Exito);
        var servicio = new ClientesAgendaService(new RepositorioAgenda(db), c.Reloj);
        var adminId = (await db.Administradores.AsNoTracking().SingleAsync()).Id;
        var r = await db.ReservasCitas.AsNoTracking().SingleAsync(); var version=Convert.ToBase64String(r.Version);
        Assert.False((await servicio.RegistrarFaltaAsync(id,version,null,adminId,default)).Exito);
        c.Reloj.Ahora = new DateTimeOffset(2026,10,12,9,0,0,TimeSpan.Zero);
        Assert.True((await servicio.RegistrarFaltaAsync(id,version,"No acudió",adminId,default)).Exito);
        Assert.False((await servicio.RegistrarFaltaAsync(id,version,null,adminId,default)).Exito);
        var cliente = await db.Clientes.AsNoTracking().Include(x=>x.Faltas).SingleAsync(); Assert.Equal(1,cliente.NumeroFaltas);
        var falta = Assert.Single(cliente.Faltas);
        Assert.False((await servicio.CorregirFaltaAsync(falta.Id,"",adminId,default)).Exito);
        Assert.True((await servicio.CorregirFaltaAsync(falta.Id,"Se registró por error",adminId,default)).Exito);
        Assert.False((await servicio.CorregirFaltaAsync(falta.Id,"Otra corrección",adminId,default)).Exito);
        cliente = await db.Clientes.AsNoTracking().Include(x=>x.Faltas).SingleAsync(); Assert.Equal(0,cliente.NumeroFaltas);
        falta = Assert.Single(cliente.Faltas); Assert.NotNull(falta.AnuladaUtc); Assert.Equal(adminId,falta.AnuladaPorId);
        Assert.Equal(EstadoCita.Confirmada,(await db.ReservasCitas.AsNoTracking().SingleAsync()).Estado);
    }
    [Fact]
    public async Task WhatsApp_prepara_enlace_y_marcar_enviado_no_acepta_ni_envia_correo()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (_, token) = await c.ProponerAsync(CanalConfirmacion.WhatsApp);
        var m = Assert.Single(await c.Comunicaciones.ListarAsync(default));
        Assert.Equal("wa.me", m.WhatsApp!.Host); Assert.Contains(token, Uri.UnescapeDataString(m.WhatsApp.Query));
        Assert.True((await c.Comunicaciones.MarcarWhatsAppAsync(m.Id, default)).Exito);
        await c.Comunicaciones.ProcesarColaAsync(default);
        Assert.Empty(c.Correo.Enviados);
        Assert.Equal(EstadoCita.PendienteAceptacion, (await db.ReservasCitas.AsNoTracking().SingleAsync()).Estado);
        Assert.True((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
    }
    [Fact]
    public async Task Sin_smtp_no_envia_y_un_resultado_incierto_no_se_repite()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        await c.ProponerAsync(CanalConfirmacion.Correo); c.Correo.Configurado = false;
        await c.Comunicaciones.ProcesarColaAsync(default); Assert.Empty(c.Correo.Enviados);
        Assert.Equal(EstadoNotificacion.Pendiente, (await db.MensajesCitas.AsNoTracking().SingleAsync()).Estado);
        c.Correo.Configurado = true; c.Correo.Resultado = ResultadoEnvio.Incierto;
        await c.Comunicaciones.ProcesarColaAsync(default); await c.Comunicaciones.ProcesarColaAsync(default);
        Assert.Single(c.Correo.Enviados);
        var m = await db.MensajesCitas.AsNoTracking().SingleAsync(); Assert.Equal(EstadoNotificacion.Incierto, m.Estado);
        Assert.False((await c.Comunicaciones.ReintentarCorreoAsync(m.Id, default)).Exito);
    }
    [Fact]
    public async Task Caducidad_libera_hueco_e_impide_enviar_o_aceptar()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (_, token) = await c.ProponerAsync(CanalConfirmacion.Correo);
        c.Reloj.Ahora = c.Reloj.Ahora.AddHours(25);
        Assert.Null(await c.Comunicaciones.ConsultarPropuestaAsync(token, default));
        Assert.False((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        Assert.Contains(new TimeOnly(10,0), (await c.Agenda.DisponibilidadAsync(new(2026,10,12), null, default)).Horas);
        await c.Comunicaciones.ProcesarColaAsync(default); Assert.Empty(c.Correo.Enviados);
        Assert.Equal(EstadoNotificacion.Obsoleto, (await db.MensajesCitas.AsNoTracking().SingleAsync()).Estado);
    }
    [Fact]
    public async Task Cambio_de_duracion_invalida_el_enlace_anterior()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var (id, token) = await c.ProponerAsync(CanalConfirmacion.Correo);
        var r = await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.True((await c.Agenda.GestionarAsync(new(id, Convert.ToBase64String(r.Version), EstadoCita.Confirmada, 45, true, null, r.AdministradorId!.Value), default)).Exito);
        Assert.False((await c.Comunicaciones.AceptarAsync(token, default)).Exito);
        await c.Comunicaciones.ProcesarColaAsync(default);
        var nuevo = Assert.Single(c.Correo.Enviados); Assert.Contains("45 minutos", nuevo.Texto); Assert.DoesNotContain(token, nuevo.Texto);
    }
    [Fact]
    public async Task Aplicacion_exige_correo_y_canal_incluso_sin_formulario_web()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto(); var c = new Contexto(db);
        var d = new SolicitarCita(Guid.NewGuid(), new(2026,10,12), new(10,0), "612345678", "Ana", null, "ip", "", CanalConfirmacion.WhatsApp);
        Assert.False((await c.Agenda.SolicitarAsync(d, default)).Exito);
        Assert.False((await c.Agenda.SolicitarAsync(d with { Correo = "ana@example.test", Canal = (CanalConfirmacion)99 }, default)).Exito);
        Assert.Empty(await db.ReservasCitas.ToListAsync());
    }
}
