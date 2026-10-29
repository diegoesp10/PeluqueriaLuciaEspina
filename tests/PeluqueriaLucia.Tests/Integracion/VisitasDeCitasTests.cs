using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// Una cita confirmada de la agenda ya es su visita: «Cobrar» la abre (una sola vez) con el servicio de la cita, se edita y
/// al cobrarla la cita queda completada. Contra SQL Server real, con un contexto por petición como en la web.
/// </summary>
public sealed class VisitasDeCitasTests : IAsyncLifetime
{
    private static readonly DateTime Inicio = DateTime.Today.AddHours(16);
    private BaseDeDatosPrueba _bd = null!;

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync() => await _bd.DisposeAsync();

    [Fact]
    public async Task Cobrar_una_cita_abre_su_visita_una_sola_vez_y_al_cobrarla_la_cita_queda_completada()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var cita = await CitaAsync(clienteId, corte);
        Assert.Equal([cita], (await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio))).Select(c => c.Id));

        var abierta = await Visitas().AbrirDesdeCitaAsync(cita);
        var otraVez = await Visitas().AbrirDesdeCitaAsync(cita);

        Assert.True(abierta.Exito, abierta.Error?.Mensaje);
        Assert.Equal(abierta.Valor, otraVez.Valor);
        Assert.Empty(await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio)));
        var visita = (await Visitas().ObtenerAsync(abierta.Valor))!;
        Assert.Equal((cita, Inicio, clienteId), (visita.ReservaId!.Value, visita.Fecha, visita.ClienteId!.Value));
        Assert.Equal([("Corte", 18m)], visita.Lineas.Select(l => (l.Nombre, l.Precio)));

        // El precio se cambia en el momento de cobrar
        var cobro = await Visitas().CobrarAsync(visita.Id, FormaPago.Efectivo, null, null,
            precios: new PreciosVisita(new Dictionary<int, decimal> { [visita.Lineas[0].Id] = 20m }, new Dictionary<int, decimal>()));

        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        Assert.Equal(20m, (await Visitas().ObtenerAsync(visita.Id))!.TotalEfectivo);
        Assert.Equal((int)EstadoCita.Completada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosClientes"));
    }

    [Fact]
    public async Task Si_el_cliente_ya_tenia_una_visita_ese_dia_se_enlaza_esa_y_no_se_crea_otra()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var ayer = (await Visitas().RegistrarAsync(new NuevaVisita(clienteId, Inicio.AddDays(-1), FormaPago.Efectivo, [corte]))).Valor;
        var aparte = (await Visitas().RegistrarAsync(new NuevaVisita(clienteId, Inicio.AddMinutes(-10), FormaPago.Efectivo, [corte]))).Valor;
        await Visitas().CobrarAsync(aparte, FormaPago.Tarjeta, null, null);
        var cita = await CitaAsync(clienteId, corte);
        Assert.Empty(await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio)));

        var abierta = await Visitas().AbrirDesdeCitaAsync(cita);

        Assert.Equal(aparte, abierta.Valor);
        Assert.Equal(cita, (await Visitas().ObtenerAsync(aparte))!.ReservaId);
        Assert.Null((await Visitas().ObtenerAsync(ayer))!.ReservaId);
        Assert.Equal(2, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosClientes"));
        // Ya estaba cobrada: la cita queda completada
        Assert.Equal((int)EstadoCita.Completada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
    }

    [Fact]
    public async Task Una_cita_sin_servicio_del_catalogo_abre_su_visita_vacia_para_anadir_los_servicios()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var cita = await CitaAsync(clienteId, null, servicio: "Consulta en la peluquería");

        var id = (await Visitas().AbrirDesdeCitaAsync(cita)).Valor;
        var vacia = await Visitas().CobrarAsync(id, FormaPago.Efectivo, null, null);
        var anadido = await Visitas().AgregarServicioAsync(id, corte, 25m);
        var cobro = await Visitas().CobrarAsync(id, FormaPago.Efectivo, null, null);

        Assert.False(vacia.Exito);
        Assert.True(anadido.Exito, anadido.Error?.Mensaje);
        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        Assert.Equal(25m, (await Visitas().ObtenerAsync(id))!.TotalEfectivo);
    }

    [Fact]
    public async Task Solo_se_cobran_las_citas_confirmadas()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var pendiente = await CitaAsync(clienteId, corte, EstadoCita.Pendiente);
        var cancelada = await CitaAsync(clienteId, corte, EstadoCita.Cancelada);

        Assert.Contains("confirmadas", (await Visitas().AbrirDesdeCitaAsync(pendiente)).Error?.Mensaje);
        Assert.Contains("confirmadas", (await Visitas().AbrirDesdeCitaAsync(cancelada)).Error?.Mensaje);
        Assert.Contains("no existe", (await Visitas().AbrirDesdeCitaAsync(Guid.NewGuid())).Error?.Mensaje);
        Assert.Empty(await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio)));
        Assert.Equal(0, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ServiciosClientes"));
    }

    [Fact]
    public async Task Una_cita_dada_sin_ficha_recibe_la_de_su_telefono_o_una_nueva_por_completar()
    {
        var (_, corte) = await ClienteYServicioAsync();
        int conTelefono;
        await using (var db = _bd.Contexto())
        {
            var ficha = Cliente.Crear("Marta", "Ruiz", null, "611 222 333", Sexo.Mujer, DateTime.Now);
            db.Add(ficha);
            await db.SaveChangesAsync();
            conTelefono = ficha.Id;
        }
        var deMarta = await CitaAsync(null, corte, nombre: "Marta", telefono: "+34611222333");
        var deDiego = await CitaAsync(null, corte, nombre: "diego", inicio: Inicio.AddHours(1));
        Assert.Equal(2, (await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio))).Count);

        var visitaMarta = (await Visitas().AbrirDesdeCitaAsync(deMarta)).Valor;
        var visitaDiego = (await Visitas().AbrirDesdeCitaAsync(deDiego)).Valor;

        Assert.Equal(conTelefono, (await Visitas().ObtenerAsync(visitaMarta))!.ClienteId);
        Assert.Equal(conTelefono, await _bd.EscalarAsync<int>($"SELECT ClienteId FROM ReservasCitas WHERE Id = '{deMarta}'"));
        var diego = (await Visitas().ObtenerAsync(visitaDiego))!.Cliente!;
        Assert.Equal(("diego", true, null, null), (diego.Nombre, diego.PendienteCompletar, diego.Email, diego.Telefono));
        Assert.Equal(diego.Id, await _bd.EscalarAsync<int>($"SELECT ClienteId FROM ReservasCitas WHERE Id = '{deDiego}'"));
        Assert.Empty(await Visitas().CitasPorCobrarAsync(DateOnly.FromDateTime(Inicio)));
    }

    [Fact]
    public async Task Una_cita_no_se_completa_sin_cobrarla()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var admin = await AdministradorAsync();
        var cita = await CitaAsync(clienteId, corte, inicio: Inicio.AddDays(-1));

        var completar = await Agenda().GestionarAsync(new GestionarCita(cita, await VersionAsync(cita), EstadoCita.Completada, 30, true, null, admin), default);

        Assert.Contains("Completar y cobrar", completar.Error?.Mensaje);
        Assert.Equal((int)EstadoCita.Confirmada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
        var visita = (await Visitas().AbrirDesdeCitaAsync(cita)).Valor;
        Assert.Equal((int)EstadoCita.Confirmada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
        await Visitas().CobrarAsync(visita, FormaPago.Efectivo, null, null);
        Assert.Equal((int)EstadoCita.Completada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
    }

    [Fact]
    public async Task Con_su_visita_abierta_la_cita_no_se_cancela_ni_se_marca_falta_hasta_eliminar_la_visita()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var admin = await AdministradorAsync();
        var cita = await CitaAsync(clienteId, corte, inicio: Inicio.AddDays(-1));
        var visita = (await Visitas().AbrirDesdeCitaAsync(cita)).Valor;

        var cancelar = await Agenda().GestionarAsync(new GestionarCita(cita, await VersionAsync(cita), EstadoCita.Cancelada, 30, true, null, admin), default);
        var falta = await ClientesAgenda().RegistrarFaltaAsync(cita, await VersionAsync(cita), null, admin, default);

        Assert.Contains("elimina antes la visita", cancelar.Error?.Mensaje);
        Assert.Contains("elimina antes la visita", falta.Error?.Mensaje);
        Assert.Equal((int)EstadoCita.Confirmada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));

        Assert.True((await Visitas().EliminarAsync(visita)).Exito);
        var otraVez = await Agenda().GestionarAsync(new GestionarCita(cita, await VersionAsync(cita), EstadoCita.Cancelada, 30, true, null, admin), default);

        Assert.True(otraVez.Exito, otraVez.Error?.Mensaje);
        Assert.Equal((int)EstadoCita.Cancelada, await _bd.EscalarAsync<int>($"SELECT Estado FROM ReservasCitas WHERE Id = '{cita}'"));
    }

    [Fact]
    public async Task Borrar_de_la_agenda_una_cita_cobrada_conserva_su_visita()
    {
        var (clienteId, corte) = await ClienteYServicioAsync();
        var cita = await CitaAsync(clienteId, corte);
        var visita = (await Visitas().AbrirDesdeCitaAsync(cita)).Valor;
        await Visitas().CobrarAsync(visita, FormaPago.Efectivo, null, null);

        // Como haría alguien a mano en la base de datos: la visita cobrada se queda, sin su cita
        await _bd.EjecutarAsync($"DELETE FROM ReservasCitas WHERE Id = '{cita}'");

        var cobrada = (await Visitas().ObtenerAsync(visita))!;
        Assert.True(cobrada.EstaCobrada);
        Assert.Null(cobrada.ReservaId);
    }

    private VisitasService Visitas()
    {
        var db = _bd.Contexto();
        var reloj = TimeProvider.System;
        return new VisitasService(new VisitaRepositorio(db), new ClienteRepositorio(db), new ServicioRepositorio(db), new ProductoRepositorio(db),
            new ExportadorExcelVisitas(reloj), db, reloj);
    }

    private AgendaService Agenda() => new(new RepositorioAgenda(_bd.Contexto()),
        new PeluqueriaLucia.Infrastructure.Seguridad.ProtectorSecretos(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider()), TimeProvider.System);

    private ClientesAgendaService ClientesAgenda() => new(new RepositorioAgenda(_bd.Contexto()), TimeProvider.System);

    private async Task<(int Cliente, int Servicio)> ClienteYServicioAsync()
    {
        await using var db = _bd.Contexto();
        var cliente = Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, DateTime.Now);
        var servicio = Servicio.Crear("Corte", 18m, Sexo.Mujer, DateTime.Now);
        db.AddRange(cliente, servicio);
        await db.SaveChangesAsync();
        return (cliente.Id, servicio.Id);
    }

    private async Task<int> AdministradorAsync()
    {
        await using var db = _bd.Contexto();
        var admin = Administrador.Crear("citas", "hash-prueba", DateTime.UtcNow);
        db.Add(admin);
        await db.SaveChangesAsync();
        return admin.Id;
    }

    /// <summary>Una cita dada en el salón, como las de «Nueva cita».</summary>
    private async Task<Guid> CitaAsync(int? clienteId, int? servicioId, EstadoCita estado = EstadoCita.Confirmada, string servicio = "Corte", DateTime? inicio = null,
        string nombre = "Ana", string telefono = "")
    {
        await using var db = _bd.Contexto();
        var cuando = inicio ?? Inicio;
        var cita = new ReservaCita
        {
            Id = Guid.NewGuid(), SolicitudId = Guid.NewGuid(), Inicio = cuando, Fin = cuando.AddMinutes(30), Nombre = nombre, Apellido = clienteId is null ? "" : "García",
            Telefono = telefono,
            Origen = OrigenCita.Panel, Estado = estado, ClienteId = clienteId, ServicioId = servicioId, ServicioNombre = servicio,
            TelefonoValidado = true, CreadaUtc = DateTime.UtcNow, ActualizadaUtc = DateTime.UtcNow, CaducaUtc = DateTime.UtcNow.AddDays(1), IpHash = "",
        };
        db.ReservasCitas.Add(cita);
        await db.SaveChangesAsync();
        return cita.Id;
    }

    private async Task<string> VersionAsync(Guid cita)
    {
        await using var db = _bd.Contexto();
        return Convert.ToBase64String((await db.ReservasCitas.SingleAsync(c => c.Id == cita)).Version);
    }
}
