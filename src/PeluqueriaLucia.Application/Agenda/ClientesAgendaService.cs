using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;

namespace PeluqueriaLucia.Application.Agenda;

public sealed class ClientesAgendaService(IRepositorioAgenda datos, TimeProvider reloj)
{
    /// <summary>
    /// La ficha de quien pide la cita, si es inequívoca: la única que comparte el móvil o el correo y coincide en los dos.
    /// Si hay varias parecidas o solo coincide uno de los datos, null (lo resuelve el salón). Hay más candidatas en <paramref name="candidatas"/>.
    /// </summary>
    internal static async Task<(Cliente? Ficha, int Candidatas)> FichaDeAsync(IRepositorioAgenda datos, string telefono, string correo, CancellationToken ct)
    {
        var candidatos = await datos.ClientesPorContactoAsync(telefono, correo, ct);
        var exactos = candidatos.Where(c => AgendaService.NormalizarTelefono(c.Telefono) == telefono &&
            string.Equals(c.Email?.Trim(), correo.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return (candidatos.Count == 1 && exactos.Count == 1 ? exactos[0] : null, candidatos.Count);
    }

    internal static async Task VincularAsync(IRepositorioAgenda datos, ReservaCita r, DateTime utc, CancellationToken ct)
    {
        // La identidad ya revisada por administración se conserva al aceptar una nueva duración.
        if (r.ClienteId is not null) return;
        var (ficha, candidatas) = await FichaDeAsync(datos, r.Telefono, r.Correo, ct);
        if (ficha is not null)
        { r.ClienteId = ficha.Id; r.PendienteVinculacion = false; return; }
        if (candidatas > 0) { r.PendienteVinculacion = true; return; }
        var cliente = Cliente.CrearDesdeReserva(string.IsNullOrWhiteSpace(r.Nombre) ? "Cliente de la agenda" : r.Nombre,
            r.Correo, r.Telefono, utc, r.Apellido, r.Sexo);
        datos.Agregar(cliente); r.Cliente = cliente; r.PendienteVinculacion = false;
    }

    public async Task<IReadOnlyList<Cliente>> PendientesAsync(CancellationToken ct) =>
        (await datos.ListarAsync<Cliente>(c => c.PendienteCompletar, ct)).OrderByDescending(c => c.FechaCreacion).ToList();
    public Task<List<Cliente>> CandidatosAsync(ReservaCita cita, CancellationToken ct) => datos.ClientesPorContactoAsync(cita.Telefono, cita.Correo, ct);
    public Task<List<ReservaCita>> ConflictosAsync(CancellationToken ct) => datos.CitasConClientesAsync(x => x.PendienteVinculacion, ct);

    public Task<Resultado> ResolverAsync(Guid id, string version, int clienteId, bool contactoVerificado, int administradorId, CancellationToken ct) =>
        datos.ExclusivoAsync<Resultado>(async () =>
        {
            var r = await datos.BuscarAsync<ReservaCita>(x => x.Id == id, ct);
            var cliente = await datos.BuscarAsync<Cliente>(x => x.Id == clienteId, ct);
            if (r is null || cliente is null || !r.PendienteVinculacion || r.AceptadaUtc is null ||
                Convert.ToBase64String(r.Version) != version || !contactoVerificado)
                return new Error("Revisa la ficha y verifica personalmente ambos datos de contacto antes de vincularla.");
            // El administrador resuelve una coincidencia parcial sin modificar contactos ni fusionar fichas.
            r.ClienteId = cliente.Id; r.PendienteVinculacion = false;
            r.AdministradorId = administradorId; r.ActualizadaUtc = reloj.GetUtcNow().UtcDateTime;
            return Resultado.Ok();
        }, ct);

    public Task<Resultado> RegistrarFaltaAsync(Guid id, string version, string? nota, int administradorId, CancellationToken ct) =>
        datos.ExclusivoAsync<Resultado>(async () =>
        {
            var r = await datos.BuscarAsync<ReservaCita>(x => x.Id == id, ct);
            var ahora = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), AgendaService.Zona).DateTime;
            if (r is null || r.Estado != EstadoCita.Confirmada || r.ClienteId is null || r.Fin > ahora ||
                Convert.ToBase64String(r.Version) != version || nota?.Length > 500)
                return new Error("Solo puedes registrar una falta de una cita confirmada, vinculada a un cliente y ya terminada. Actualiza la agenda si ha cambiado.");
            if (await datos.ExisteAsync<FaltaAsistencia>(x => x.ReservaId == id && x.AnuladaUtc == null, ct))
                return new Error("Esta cita ya tiene una falta registrada.");
            if (await datos.ExisteAsync<PeluqueriaLucia.Domain.Visitas.Visita>(x => x.ReservaId == id, ct))
                return new Error("Esta cita ya tiene su visita abierta para cobrarla. Si el cliente no vino, elimina antes la visita.");
            datos.Agregar(new FaltaAsistencia { ClienteId = r.ClienteId.Value, ReservaId = id, InicioCita = r.Inicio,
                RegistradaUtc = reloj.GetUtcNow().UtcDateTime, AdministradorId = administradorId, Nota = nota?.Trim() ?? "" });
            r.Estado = EstadoCita.NoPresentada; r.AdministradorId = administradorId; r.ActualizadaUtc = reloj.GetUtcNow().UtcDateTime;
            return Resultado.Ok();
        }, ct);

    public Task<Resultado> CorregirFaltaAsync(int id, string? motivo, int administradorId, CancellationToken ct) =>
        datos.ExclusivoAsync<Resultado>(async () =>
        {
            var falta = await datos.BuscarAsync<FaltaAsistencia>(x => x.Id == id, ct);
            if (falta is null || falta.AnuladaUtc is not null || string.IsNullOrWhiteSpace(motivo) || motivo.Length > 500)
                return new Error("Indica el motivo de la corrección (máximo 500 caracteres). La falta debe seguir activa.");
            var r = await datos.BuscarAsync<ReservaCita>(x => x.Id == falta.ReservaId, ct);
            falta.AnuladaUtc = reloj.GetUtcNow().UtcDateTime; falta.AnuladaPorId = administradorId; falta.MotivoAnulacion = motivo.Trim();
            if (r?.Estado == EstadoCita.NoPresentada) { r.Estado = EstadoCita.Confirmada; r.AdministradorId = administradorId; r.ActualizadaUtc = reloj.GetUtcNow().UtcDateTime; }
            return Resultado.Ok();
        }, ct);
}
