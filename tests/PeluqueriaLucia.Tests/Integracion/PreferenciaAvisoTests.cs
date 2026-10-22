using System.Security.Cryptography;
using System.Text;
using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Infrastructure.Seguridad;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// Preferencia de aviso de la ficha del cliente: si la tiene, los avisos de sus citas van siempre por ese canal (las pida en
/// la web o se las dé el salón); si no, por el canal elegido en cada cita.
/// </summary>
public sealed class PreferenciaAvisoTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 10, 12);
    private BaseDeDatosPrueba _bd = null!;
    private readonly Reloj _reloj = new();
    private readonly ProtectorSecretos _protector = new(new EphemeralDataProtectionProvider());

    private sealed class Reloj : TimeProvider { public DateTimeOffset Ahora = new(2026, 10, 10, 8, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Ahora; }
    private sealed class Url : IUrlPublica { public Uri? Obtener() => new("https://salon.example.test/"); }
    private sealed class Ajustes : IAjustesComunicaciones { public bool EnvioAutomatico { get; set; } }
    private sealed class SinCorreo : IEnviadorCorreo
    {
        public bool Configurado => false;
        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct = default) => Task.FromResult(ResultadoEnvio.Rechazado);
    }

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync() => await _bd.DisposeAsync();

    private AgendaService Agenda(PeluqueriaDbContext db) => new(new RepositorioAgenda(db), _protector, _reloj);

    private async Task<(Cliente Cliente, int Admin)> FichaAsync(PeluqueriaDbContext db, string? telefono, string? email, CanalConfirmacion? preferencia)
    {
        var cliente = Cliente.Crear("Ana", "Prueba", email, telefono, Sexo.Mujer, DateTime.Now);
        cliente.CambiarPreferenciaAviso(preferencia);
        var admin = Administrador.Crear("avisos-" + Guid.NewGuid().ToString("N")[..6], "hash-prueba", _reloj.Ahora.UtcDateTime);
        db.AddRange(cliente, admin);
        await db.SaveChangesAsync();
        return (cliente, admin.Id);
    }

    private static SolicitarCita Solicitud(TimeOnly hora, string telefono, string correo, CanalConfirmacion canal) =>
        new(Guid.NewGuid(), Dia, hora, telefono, "Ana", null, "ip-prueba", correo, canal, "Prueba", Sexo.Mujer);

    [Fact]
    public async Task La_solicitud_web_de_una_ficha_con_preferencia_avisa_por_su_canal_aunque_se_marque_otro()
    {
        await using var db = _bd.Contexto();
        var (_, admin) = await FichaAsync(db, "612 345 678", "ana@example.test", CanalConfirmacion.WhatsApp);
        var agenda = Agenda(db);

        var solicitud = await agenda.SolicitarAsync(Solicitud(new(10, 0), "612345678", "ANA@example.test ", CanalConfirmacion.Correo), default);

        Assert.True(solicitud.Exito, solicitud.Error?.Mensaje);
        var r = await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == solicitud.Valor);
        Assert.Equal(CanalConfirmacion.WhatsApp, r.Canal);
        Assert.True((await agenda.GestionarAsync(new(r.Id, Convert.ToBase64String(r.Version), EstadoCita.Confirmada, 30, true, null, admin), default)).Exito);
        var aviso = await db.MensajesCitas.AsNoTracking().SingleAsync();
        Assert.Equal((CanalConfirmacion.WhatsApp, "+34612345678"), (aviso.Canal, aviso.Destinatario));
    }

    [Fact]
    public async Task Si_la_ficha_no_coincide_en_movil_y_correo_se_respeta_el_canal_elegido()
    {
        await using var db = _bd.Contexto();
        await FichaAsync(db, "612345678", "otra@example.test", CanalConfirmacion.WhatsApp);

        var solicitud = await Agenda(db).SolicitarAsync(Solicitud(new(10, 0), "612345678", "ana@example.test", CanalConfirmacion.Correo), default);

        Assert.True(solicitud.Exito, solicitud.Error?.Mensaje);
        Assert.Equal(CanalConfirmacion.Correo, (await db.ReservasCitas.AsNoTracking().SingleAsync()).Canal);
    }

    [Fact]
    public async Task La_cita_del_salon_avisa_por_la_preferencia_de_la_ficha_o_por_el_canal_elegido()
    {
        await using var db = _bd.Contexto();
        var (conPreferencia, admin) = await FichaAsync(db, "612345678", "ana@example.test", CanalConfirmacion.Correo);
        var sinPreferencia = Cliente.Crear("Marta", "López", null, "622 333 444", Sexo.Mujer, DateTime.Now);
        db.Add(sinPreferencia);
        await db.SaveChangesAsync();
        var agenda = Agenda(db);
        CitaManual Cita(TimeOnly hora, int? cliente, CanalConfirmacion? aviso, string? nombre = null, string? telefono = null) =>
            new(Dia, hora, 30, cliente, nombre, telefono, null, null, false, admin, aviso);

        // Con preferencia manda la ficha, aunque se elija otro canal en el formulario
        var preferida = await agenda.CrearCitaAsync(Cita(new(10, 0), conPreferencia.Id, CanalConfirmacion.WhatsApp), default);
        // Sin preferencia: sin aviso, o el canal elegido si la ficha tiene ese dato
        var sinAviso = await agenda.CrearCitaAsync(Cita(new(10, 30), sinPreferencia.Id, null), default);
        var sinCorreo = await agenda.CrearCitaAsync(Cita(new(11, 0), sinPreferencia.Id, CanalConfirmacion.Correo), default);
        var porWhatsApp = await agenda.CrearCitaAsync(Cita(new(11, 0), sinPreferencia.Id, CanalConfirmacion.WhatsApp), default);
        // Sin ficha: WhatsApp necesita un móvil
        var fijo = await agenda.CrearCitaAsync(Cita(new(11, 30), null, CanalConfirmacion.WhatsApp, "Luis", "985 111 222"), default);
        var movil = await agenda.CrearCitaAsync(Cita(new(11, 30), null, CanalConfirmacion.WhatsApp, "Luis", "699 111 222"), default);

        Assert.True(preferida.Exito && sinAviso.Exito && porWhatsApp.Exito && movil.Exito);
        Assert.Equal(("Aviso", "Aviso"), (sinCorreo.Error?.Campo, fijo.Error?.Campo));
        var avisos = await db.MensajesCitas.AsNoTracking().ToDictionaryAsync(m => m.ReservaId);
        Assert.Equal(3, avisos.Count);
        Assert.Equal((CanalConfirmacion.Correo, "ana@example.test"), (avisos[preferida.Valor].Canal, avisos[preferida.Valor].Destinatario));
        Assert.Equal((CanalConfirmacion.WhatsApp, "+34622333444"), (avisos[porWhatsApp.Valor].Canal, avisos[porWhatsApp.Valor].Destinatario));
        Assert.Equal((CanalConfirmacion.WhatsApp, "+34699111222"), (avisos[movil.Valor].Canal, avisos[movil.Valor].Destinatario));
        Assert.All(avisos.Values, m => Assert.Contains("queda confirmada", m.Texto));
        Assert.False(avisos.ContainsKey(sinAviso.Valor));
        Assert.Equal(CanalConfirmacion.Correo, (await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == preferida.Valor)).Canal);
    }

    [Fact]
    public async Task Cancelar_una_cita_del_salon_avisada_avisa_de_la_cancelacion_y_anula_la_confirmacion_pendiente()
    {
        await using var db = _bd.Contexto();
        var (cliente, admin) = await FichaAsync(db, "612345678", null, null);
        var agenda = Agenda(db);
        var avisada = (await agenda.CrearCitaAsync(new(Dia, new(10, 0), 30, cliente.Id, null, null, null, null, false, admin, CanalConfirmacion.WhatsApp), default)).Valor;
        var callada = (await agenda.CrearCitaAsync(new(Dia, new(11, 0), 30, cliente.Id, null, null, null, null, false, admin), default)).Valor;

        foreach (var id in new[] { avisada, callada })
        {
            var r = await db.ReservasCitas.AsNoTracking().SingleAsync(x => x.Id == id);
            Assert.True((await agenda.GestionarAsync(new(r.Id, Convert.ToBase64String(r.Version), EstadoCita.Cancelada, 30, true, null, admin), default)).Exito);
        }

        var avisos = await db.MensajesCitas.AsNoTracking().OrderBy(m => m.CreadoUtc).ToListAsync();
        Assert.All(avisos, m => Assert.Equal(avisada, m.ReservaId));
        Assert.Equal([EstadoNotificacion.Obsoleto, EstadoNotificacion.Pendiente], avisos.Select(m => m.Estado).OrderByDescending(e => e == EstadoNotificacion.Obsoleto));
        var cancelacion = avisos.Single(m => m.Estado == EstadoNotificacion.Pendiente);
        Assert.Equal(CanalConfirmacion.WhatsApp, cancelacion.Canal);
        Assert.Contains("se ha cancelado", cancelacion.Texto);
    }

    [Fact]
    public async Task Aceptar_una_propuesta_dual_no_choca_con_otras_citas_pero_una_normal_si()
    {
        await using var db = _bd.Contexto();
        ReservaCita Cita(TimeOnly hora, EstadoCita estado, bool dual, string? token = null) => new()
        {
            Id = Guid.NewGuid(), SolicitudId = Guid.NewGuid(), Inicio = Dia.ToDateTime(hora), Fin = Dia.ToDateTime(hora).AddMinutes(60), Nombre = "Ana",
            Telefono = "+34612345678", Correo = "ana@example.test", Estado = estado, Origen = estado == EstadoCita.Confirmada ? OrigenCita.Panel : OrigenCita.Web,
            Dualidad = dual, ConfirmacionHash = token is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))),
            CreadaUtc = _reloj.Ahora.UtcDateTime, ActualizadaUtc = _reloj.Ahora.UtcDateTime, CaducaUtc = _reloj.Ahora.UtcDateTime.AddHours(24),
        };
        var tokenDual = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenNormal = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.AddRange(Cita(new(10, 0), EstadoCita.Confirmada, false), Cita(new(10, 30), EstadoCita.PendienteAceptacion, true, tokenDual),
            Cita(new(16, 30), EstadoCita.Confirmada, false), Cita(new(17, 0), EstadoCita.PendienteAceptacion, false, tokenNormal));
        await db.SaveChangesAsync();
        var comunicaciones = new ComunicacionesCitas(new RepositorioAgenda(db), _protector, new SinCorreo(), new WhatsAppManual(), new Url(), _reloj, new Ajustes());

        Assert.True((await comunicaciones.AceptarAsync(tokenDual, default)).Exito);
        Assert.False((await comunicaciones.AceptarAsync(tokenNormal, default)).Exito);
    }

    [Fact]
    public async Task La_ficha_guarda_la_preferencia_solo_si_tiene_el_dato_de_ese_canal()
    {
        await using var db = _bd.Contexto();
        var clientes = new ClientesService(new ClienteRepositorio(db), db, _reloj);

        var sinCorreo = await clientes.CrearAsync(new DatosCliente("Rosa", "Díaz", null, "633444555", Sexo.Mujer, CanalPreferido: CanalConfirmacion.Correo));
        var conMovil = await clientes.CrearAsync(new DatosCliente("Rosa", "Díaz", null, "633444555", Sexo.Mujer, CanalPreferido: CanalConfirmacion.WhatsApp));
        var quitarTelefono = await clientes.ActualizarAsync(conMovil.Valor, new DatosCliente("Rosa", "Díaz", "rosa@example.test", null, Sexo.Mujer, CanalPreferido: CanalConfirmacion.WhatsApp));
        var cambiar = await clientes.ActualizarAsync(conMovil.Valor, new DatosCliente("Rosa", "Díaz", "rosa@example.test", "633444555", Sexo.Mujer, CanalPreferido: CanalConfirmacion.Correo));

        Assert.Equal(("CanalPreferido", "CanalPreferido"), (sinCorreo.Error?.Campo, quitarTelefono.Error?.Campo));
        Assert.True(conMovil.Exito && cambiar.Exito);
        await using var otra = _bd.Contexto();
        Assert.Equal(CanalConfirmacion.Correo, (await otra.Clientes.AsNoTracking().SingleAsync(c => c.Id == conMovil.Valor)).CanalPreferido);
    }
}
