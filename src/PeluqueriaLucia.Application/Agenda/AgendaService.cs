using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;

namespace PeluqueriaLucia.Application.Agenda;

public sealed class AgendaService(IRepositorioAgenda datos, IProtectorSecretos secretos, TimeProvider reloj) : IAgendaService
{
    internal static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private DateTime Utc => reloj.GetUtcNow().UtcDateTime;
    private DateTime Ahora => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime;
    public DateOnly Hoy => DateOnly.FromDateTime(Ahora);
    public async Task<IReadOnlyList<ServicioAgenda>> ServiciosAsync(CancellationToken ct) =>
        (await datos.ListarAsync<Servicio>(x => x.Activo && x.Publicado, ct)).OrderBy(x => x.Nombre)
            .Select(x => new ServicioAgenda(x.Id, x.Nombre, x.DuracionMinutos, x.NombreEn)).ToList();
    private async Task<int> DuracionAsync(int? id, CancellationToken ct) => id is null ? 30 :
        (await datos.BuscarAsync<Servicio>(x => x.Id == id && x.Activo && x.Publicado, ct))?.DuracionMinutos ?? 0;
    private Task<List<ReservaCita>> OcupadasAsync(DateTime inicio, DateTime fin, CancellationToken ct) =>
        datos.ListarAsync<ReservaCita>(x => x.Inicio < fin && x.Fin > inicio &&
            (x.Estado == EstadoCita.Confirmada || ((x.Estado == EstadoCita.Pendiente || x.Estado == EstadoCita.PendienteAceptacion) && x.CaducaUtc > Utc)), ct);

    public async Task<DisponibilidadAgenda> DisponibilidadAsync(DateOnly dia, int? servicioId, CancellationToken ct)
    {
        var minutos = await DuracionAsync(servicioId, ct);
        if (dia < Hoy || dia > Hoy.AddDays(90) || minutos is < 5 or > 600 || await datos.ExisteAsync<CierreAgenda>(x => x.Dia == dia, ct)) return new(dia, minutos, []);
        var tramos = await datos.ListarAsync<TramoAgenda>(x => x.Dia == dia.DayOfWeek, ct);
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        var ocupadas = await OcupadasAsync(inicio, inicio.AddDays(1), ct);
        var horas = new HashSet<TimeOnly>();
        var bloqueadas = new HashSet<TimeOnly>();
        foreach (var tramo in tramos)
            for (var t = dia.ToDateTime(tramo.Desde); t.AddMinutes(minutos) <= dia.ToDateTime(tramo.Hasta); t = t.AddMinutes(tramo.IntervaloMinutos))
            {
                if (t < Ahora.AddMinutes(30) || Zona.IsInvalidTime(t) || Zona.IsAmbiguousTime(t) ||
                    Zona.IsInvalidTime(t.AddMinutes(minutos)) || Zona.IsAmbiguousTime(t.AddMinutes(minutos))) continue;
                var hora = TimeOnly.FromDateTime(t);
                if (ocupadas.Any(x => x.Inicio < t.AddMinutes(minutos) && x.Fin > t)) bloqueadas.Add(hora);
                else horas.Add(hora);
            }
        return new(dia, minutos, horas.Order().ToList(), bloqueadas.Except(horas).Order().ToList());
    }

    public static string? NormalizarTelefono(string? valor)
    {
        if (valor is null || valor.Length > 30 || valor.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')'))) return null;
        var numero = Regex.Replace(valor, "[ ()-]", "");
        if (numero.StartsWith("0034")) numero = "+34" + numero[4..];
        if (Regex.IsMatch(numero, "^[67][0-9]{8}$")) numero = "+34" + numero;
        return Regex.IsMatch(numero, @"^\+34[67][0-9]{8}$") ? numero : null;
    }
    public static string? NormalizarCorreo(string? valor)
    {
        var correo = valor?.Trim();
        return correo is { Length: > 3 and <= 254 } && !correo.Any(char.IsWhiteSpace) &&
            MailAddress.TryCreate(correo, out var m) && m.Address == correo && correo.Contains('@') &&
            correo[(correo.LastIndexOf('@') + 1)..].Contains('.') ? correo : null;
    }

    public Task<Resultado<Guid>> SolicitarAsync(SolicitarCita d, CancellationToken ct) => datos.ExclusivoAsync<Resultado<Guid>>(async () =>
    {
        var telefono = NormalizarTelefono(d.Telefono); var correo = NormalizarCorreo(d.Correo);
        if (telefono is null) return new Error("Introduce un móvil español válido: nueve cifras, empezando por 6 o 7.", "Telefono");
        if (correo is null) return new Error("Introduce un correo electrónico válido.", "Correo");
        if (!Enum.IsDefined(d.Canal)) return new Error("Elige correo electrónico o WhatsApp.", "Canal");
        if (d.SolicitudId == Guid.Empty || string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Length > 100) return new Error("Revisa los datos de la solicitud.");
        if (string.IsNullOrWhiteSpace(d.Apellido) || d.Apellido.Length > 100 || d.Sexo is null || !Enum.IsDefined(d.Sexo.Value)) return new Error("Revisa los apellidos y el sexo.");
        var previa = await datos.BuscarAsync<ReservaCita>(x => x.SolicitudId == d.SolicitudId, ct);
        if (previa is not null) return previa.Telefono == telefono && previa.Correo == correo && previa.Canal == d.Canal ? Resultado.Ok(previa.Id) : new Error("La solicitud no es válida. Recarga el formulario.");
        var ip = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(d.Ip)));
        if (await datos.ContarAsync<ReservaCita>(x => x.IpHash == ip && x.CreadaUtc > Utc.AddHours(-1), ct) >= 5 ||
            await datos.ContarAsync<ReservaCita>(x => x.Telefono == telefono && x.Fin > Ahora && (x.Estado == EstadoCita.Confirmada ||
                ((x.Estado == EstadoCita.Pendiente || x.Estado == EstadoCita.PendienteAceptacion) && x.CaducaUtc > Utc)), ct) >= 2)
            return new Error("Ya hay varias solicitudes desde tus datos. Contacta con el salón para revisarlas.");
        var disponibles = await DisponibilidadAsync(d.Dia, d.ServicioId, ct);
        if (!disponibles.Horas.Contains(d.Hora)) return new Error("Ese horario acaba de ocuparse o no está disponible. Elige otro.", "Hora");
        var servicio = d.ServicioId is null ? null : await datos.BuscarAsync<Servicio>(x => x.Id == d.ServicioId, ct);
        var cita = new ReservaCita { Id = Guid.NewGuid(), SolicitudId = d.SolicitudId, Inicio = d.Dia.ToDateTime(d.Hora),
            Fin = d.Dia.ToDateTime(d.Hora).AddMinutes(disponibles.Minutos), Telefono = telefono, Correo = correo, Canal = d.Canal,
            Nombre = d.Nombre?.Trim() ?? "", Apellido = d.Apellido?.Trim() ?? "", Sexo = d.Sexo, ServicioId = d.ServicioId, ServicioNombre = servicio?.Nombre ?? "Consulta con el equipo",
            Estado = EstadoCita.Pendiente, CreadaUtc = Utc, ActualizadaUtc = Utc, CaducaUtc = Utc.AddHours(24), IpHash = ip };
        datos.Agregar(cita); return Resultado.Ok(cita.Id);
    }, ct);

    public Task<Resultado> GestionarAsync(GestionarCita d, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var r = await datos.BuscarAsync<ReservaCita>(x => x.Id == d.Id, ct);
        if (r is null) return new Error("La reserva no existe.");
        if (Convert.ToBase64String(r.Version) != d.Version) return new Error("La reserva ha cambiado en otra pestaña. Actualiza la agenda.");
        if (d.Nota?.Length > 500 || !Enum.IsDefined(d.Estado) || d.Estado is EstadoCita.Pendiente or EstadoCita.PendienteAceptacion or EstadoCita.NoPresentada) return new Error("Revisa los datos de la gestión.");
        if (r.Estado is EstadoCita.Rechazada or EstadoCita.Cancelada or EstadoCita.Completada or EstadoCita.NoPresentada) return new Error("Esta reserva ya está cerrada.");
        if (await datos.ExisteAsync<MensajeCita>(x => x.ReservaId == r.Id && x.Estado == EstadoNotificacion.Enviando, ct)) return new Error("Se está enviando el aviso. Espera unos segundos antes de cambiar la cita.");
        var fin = r.Fin;
        var web = r.Origen == OrigenCita.Web;
        if (d.Estado == EstadoCita.Confirmada)
        {
            if (web)
            {
                if (!d.TelefonoValidado) return new Error("Confirma que has contactado con el teléfono y validado la solicitud.");
                if (!d.ConfirmarDirecto && NormalizarCorreo(r.Correo) is null) return new Error("Esta reserva antigua no tiene correo. Confírmala directamente si ya la has acordado por teléfono.");
                if (r.Inicio <= Ahora || (r.Estado != EstadoCita.Confirmada && r.CaducaUtc <= Utc)) return new Error("La solicitud ha caducado. Acuerda otra hora con el cliente.");
            }
            if (d.Minutos is < 5 or > 600) return new Error("La duración debe ser de 5 a 600 minutos.");
            fin = r.Inicio.AddMinutes(d.Minutos); var dia = DateOnly.FromDateTime(r.Inicio);
            if (Zona.IsInvalidTime(r.Inicio) || Zona.IsAmbiguousTime(r.Inicio) || Zona.IsInvalidTime(fin) || Zona.IsAmbiguousTime(fin)) return new Error("El intervalo coincide con un cambio de hora. Acuerda otro horario.");
            // Las citas que da el salón pueden salirse del horario público (horas extra), pero nunca del día.
            if (DateOnly.FromDateTime(fin) != dia || (web && (await datos.ExisteAsync<CierreAgenda>(x => x.Dia == dia, ct) ||
                !await datos.ExisteAsync<TramoAgenda>(x => x.Dia == dia.DayOfWeek && x.Desde <= TimeOnly.FromDateTime(r.Inicio) && x.Hasta >= TimeOnly.FromDateTime(fin), ct)))) return new Error("La duración queda fuera del horario de apertura.");
            if ((await OcupadasAsync(r.Inicio, fin, ct)).Any(x => x.Id != r.Id)) return new Error("La duración se solapa con otra reserva. Ajusta el tiempo o rechaza la solicitud.");
        }
        else if (d.Estado == EstadoCita.Completada && (r.Estado != EstadoCita.Confirmada || r.Inicio > Ahora)) return new Error("Solo puedes completar una cita confirmada que ya haya comenzado.");
        else if (d.Estado == EstadoCita.Rechazada && r.Estado != EstadoCita.Pendiente) return new Error("Una cita propuesta o confirmada se cancela, no se rechaza.");

        if (!web)
        {
            // Citas del salón y bloqueos: sin propuestas ni avisos automáticos; se hablan directamente con el cliente.
            if (d.Estado == EstadoCita.Rechazada) return new Error("Una cita del salón se cancela, no se rechaza.");
            r.Fin = fin; r.Estado = d.Estado; r.NotaInterna = d.Nota?.Trim() ?? ""; r.AdministradorId = d.AdministradorId; r.ActualizadaUtc = Utc;
            return Resultado.Ok();
        }
        var directo = d.Estado == EstadoCita.Confirmada && d.ConfirmarDirecto && r.Estado != EstadoCita.Confirmada;
        var cambio = directo || fin != r.Fin || (d.Estado == EstadoCita.Confirmada ? r.Estado == EstadoCita.Pendiente : r.Estado != d.Estado);
        r.Fin = fin; r.TelefonoValidado = d.TelefonoValidado;
        r.NotaInterna = d.Nota?.Trim() ?? ""; r.AdministradorId = d.AdministradorId; r.ActualizadaUtc = Utc;
        if (cambio)
        {
            foreach (var anterior in await datos.ListarAsync<MensajeCita>(x => x.ReservaId == r.Id && x.Estado != EstadoNotificacion.Obsoleto, ct)) anterior.Estado = EstadoNotificacion.Obsoleto;
            r.ConfirmacionHash = null;
            if (directo)
            {
                await ClientesAgendaService.VincularAsync(datos, r, Utc, ct);
                r.Estado = EstadoCita.Confirmada;
                if (NormalizarCorreo(r.Correo) is not null || r.Canal == CanalConfirmacion.WhatsApp)
                    datos.Agregar(new MensajeCita { Id = Guid.NewGuid(), ReservaId = r.Id, Canal = r.Canal,
                        Destinatario = r.Canal == CanalConfirmacion.Correo ? r.Correo : r.Telefono, CreadoUtc = Utc, Estado = EstadoNotificacion.Pendiente,
                        Texto = $"Tu cita del {r.Inicio:dd/MM/yyyy} a las {r.Inicio:HH:mm} para {r.ServicioNombre} queda confirmada, tal como la hemos hablado. Si necesitas cambiarla, contacta con el equipo." });
            }
            else if (d.Estado == EstadoCita.Confirmada)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                r.AceptadaUtc = null;
                r.ConfirmacionHash = ComunicacionesCitas.Hash(token);
                r.Estado = EstadoCita.PendienteAceptacion;
                var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(r.Inicio, DateTimeKind.Unspecified), Zona);
                r.CaducaUtc = inicioUtc < Utc.AddHours(24) ? inicioUtc : Utc.AddHours(24);
                datos.Agregar(new MensajeCita { Id = Guid.NewGuid(), ReservaId = r.Id, Canal = r.Canal,
                    Destinatario = r.Canal == CanalConfirmacion.Correo ? r.Correo : r.Telefono, CreadoUtc = Utc,
                    Texto = $"El equipo del salón te propone una cita el {r.Inicio:dd/MM/yyyy} a las {r.Inicio:HH:mm}, para {r.ServicioNombre}, con una duración de {(int)(r.Fin-r.Inicio).TotalMinutes} minutos. Acepta la cita en el enlace antes del {TimeZoneInfo.ConvertTimeFromUtc(r.CaducaUtc, Zona):dd/MM/yyyy HH:mm}. Si necesitas cambiarla, contacta con el equipo.",
                    TokenProtegido = secretos.Proteger(token, "confirmacion-cita"), Estado = EstadoNotificacion.Pendiente });
            }
            else
            {
                r.Estado = d.Estado;
                if (d.Estado is EstadoCita.Rechazada or EstadoCita.Cancelada && NormalizarCorreo(r.Correo) is not null)
                    datos.Agregar(new MensajeCita { Id = Guid.NewGuid(), ReservaId = r.Id, Canal = r.Canal,
                        Destinatario = r.Canal == CanalConfirmacion.Correo ? r.Correo : r.Telefono, CreadoUtc = Utc, Estado = EstadoNotificacion.Pendiente,
                        Texto = $"El equipo del salón informa de que tu solicitud del {r.Inicio:dd/MM/yyyy HH:mm} no está confirmada o se ha cancelado. Contacta con el equipo para acordar otro momento." });
            }
        }
        return Resultado.Ok();
    }, ct);

    public async Task<IReadOnlyList<ServicioAgenda>> ServiciosPanelAsync(CancellationToken ct) =>
        (await datos.ListarAsync<Servicio>(x => x.Activo, ct)).OrderBy(x => x.Nombre)
            .Select(x => new ServicioAgenda(x.Id, x.Nombre, x.DuracionMinutos, x.NombreEn)).ToList();

    public Task<Resultado<Guid>> CrearCitaAsync(CitaManual d, CancellationToken ct) => datos.ExclusivoAsync<Resultado<Guid>>(async () =>
    {
        if (d.Dia < Hoy.AddDays(-30) || d.Dia > Hoy.AddDays(365)) return new Error("Elige un día entre el último mes y el próximo año.", "Dia");
        if (d.Nota?.Length > 500) return new Error("La nota admite hasta 500 caracteres.", "Nota");
        var servicio = d.Bloqueo || d.ServicioId is null ? null : await datos.BuscarAsync<Servicio>(x => x.Id == d.ServicioId && x.Activo, ct);
        if (!d.Bloqueo && d.ServicioId is not null && servicio is null) return new Error("El servicio ya no está disponible.", "ServicioId");
        var minutos = d.Minutos ?? (servicio is { DuracionMinutos: > 0 } ? servicio.DuracionMinutos : 30);
        if (minutos is < 5 or > 600) return new Error("La duración debe ser de 5 a 600 minutos.", "Minutos");
        var inicio = d.Dia.ToDateTime(d.Hora); var fin = inicio.AddMinutes(minutos);
        if (DateOnly.FromDateTime(fin) != d.Dia) return new Error("La cita debe terminar el mismo día.", "Minutos");
        if (Zona.IsInvalidTime(inicio) || Zona.IsAmbiguousTime(inicio) || Zona.IsInvalidTime(fin) || Zona.IsAmbiguousTime(fin)) return new Error("El intervalo coincide con un cambio de hora. Elige otra hora.", "Hora");
        if (await datos.ExisteAsync<CierreAgenda>(x => x.Dia == d.Dia, ct)) return new Error("Ese día está cerrado. Reábrelo en Horarios si vas a trabajar.", "Dia");
        var solape = (await OcupadasAsync(inicio, fin, ct)).OrderBy(x => x.Inicio).FirstOrDefault();
        if (solape is not null) return new Error($"Se solapa con «{(solape.Nombre + " " + solape.Apellido).Trim()}» de {solape.Inicio:HH:mm} a {solape.Fin:HH:mm}.", "Hora");

        var cita = new ReservaCita { Id = Guid.NewGuid(), SolicitudId = Guid.NewGuid(), Inicio = inicio, Fin = fin, Estado = EstadoCita.Confirmada,
            Origen = d.Bloqueo ? OrigenCita.Bloqueo : OrigenCita.Panel, TelefonoValidado = true, AdministradorId = d.AdministradorId,
            NotaInterna = d.Nota?.Trim() ?? "", CreadaUtc = Utc, ActualizadaUtc = Utc, CaducaUtc = Utc, IpHash = "" };
        if (d.Bloqueo)
        {
            cita.Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? "Tiempo reservado" : d.Nombre.Trim();
            if (cita.Nombre.Length > 100) return new Error("El motivo admite hasta 100 caracteres.", "Nombre");
            cita.ServicioNombre = "Bloqueo";
        }
        else if (d.ClienteId is not null)
        {
            var cliente = await datos.BuscarAsync<PeluqueriaLucia.Domain.Clientes.Cliente>(x => x.Id == d.ClienteId, ct);
            if (cliente is null) return new Error("El cliente no existe.", "ClienteId");
            cita.ClienteId = cliente.Id; cita.Nombre = cliente.Nombre; cita.Apellido = cliente.Apellido; cita.Sexo = cliente.Sexo;
            cita.Telefono = TelefonoDeContacto(cliente.Telefono) ?? ""; cita.Correo = NormalizarCorreo(cliente.Email) ?? "";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(d.Nombre) || d.Nombre.Trim().Length > 100) return new Error("Escribe el nombre del cliente o elige su ficha.", "Nombre");
            var telefono = TelefonoDeContacto(d.Telefono);
            if (!string.IsNullOrWhiteSpace(d.Telefono) && telefono is null) return new Error("Revisa el teléfono.", "Telefono");
            cita.Nombre = d.Nombre.Trim(); cita.Telefono = telefono ?? "";
        }
        if (!d.Bloqueo) cita.ServicioNombre = servicio?.Nombre ?? "Sin servicio indicado";
        cita.ServicioId = servicio?.Id;
        datos.Agregar(cita);
        return Resultado.Ok(cita.Id);
    }, ct);

    /// <summary>Móviles en formato +34; fijos y extranjeros tal cual si son solo cifras (hasta 16).</summary>
    private static string? TelefonoDeContacto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        if (NormalizarTelefono(valor) is { } movil) return movil;
        var limpio = Regex.Replace(valor, "[ ()-]", "");
        return Regex.IsMatch(limpio, @"^\+?[0-9]{6,15}$") ? limpio : null;
    }

    public async Task<IReadOnlyList<ReservaCita>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde || hasta > desde.AddDays(31)) hasta = desde.AddDays(31);
        var a = desde.ToDateTime(TimeOnly.MinValue); var b = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return (await datos.CitasConClientesAsync(x => x.Inicio >= a && x.Inicio < b, ct)).OrderBy(x => x.Inicio).Take(500).ToList();
    }
    public Task<Resultado> EliminarCitaAsync(Guid id, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        var r = await datos.BuscarAsync<ReservaCita>(x => x.Id == id, ct);
        if (r is null) return new Error("La cita no existe.");
        var caducada = r.Estado is EstadoCita.Pendiente or EstadoCita.PendienteAceptacion && r.CaducaUtc <= Utc;
        if (!(r.Estado is EstadoCita.Cancelada or EstadoCita.Rechazada or EstadoCita.NoPresentada || caducada))
            return new Error("Solo se eliminan citas canceladas, rechazadas, no presentadas o caducadas. Cancela antes esta cita.");
        if (await datos.ExisteAsync<MensajeCita>(x => x.ReservaId == r.Id && x.Estado == EstadoNotificacion.Enviando, ct))
            return new Error("Se está enviando un aviso de esta cita. Espera unos segundos.");
        if (await datos.ExisteAsync<PeluqueriaLucia.Domain.Clientes.FaltaAsistencia>(x => x.ReservaId == r.Id, ct))
            return new Error("Esta cita tiene una falta registrada en la ficha del cliente y se conserva como historial.");
        foreach (var m in await datos.ListarAsync<MensajeCita>(x => x.ReservaId == r.Id, ct)) datos.Quitar(m);
        datos.Quitar(r);
        return Resultado.Ok();
    }, ct);
    public Task<IReadOnlyList<MensajeCita>> MensajesAsync(CancellationToken ct) => datos.UltimosMensajesAsync(ct);
    public async Task<AvisoAgenda> AvisosAsync(CancellationToken ct) => new(await datos.ContarAsync<ReservaCita>(x => x.Estado == EstadoCita.Pendiente && x.CaducaUtc > Utc && x.Inicio > Ahora, ct), await datos.UltimaSolicitudAsync(ct));
    public async Task<IReadOnlyList<ReservaCita>> PendientesAsync(CancellationToken ct) => (await datos.CitasConClientesAsync(x => (x.Estado == EstadoCita.Pendiente || x.Estado == EstadoCita.PendienteAceptacion) && x.CaducaUtc > Utc && x.Inicio > Ahora, ct)).OrderBy(x => x.Inicio).Take(500).ToList();
    public async Task<IReadOnlyList<TramoAgenda>> HorariosAsync(CancellationToken ct) => (await datos.ListarAsync<TramoAgenda>(x => true, ct)).OrderBy(x => x.Dia).ThenBy(x => x.Desde).ToList();
    public async Task<IReadOnlyList<CierreAgenda>> CierresAsync(CancellationToken ct) => (await datos.ListarAsync<CierreAgenda>(x => x.Dia >= Hoy, ct)).OrderBy(x => x.Dia).ToList();
    public Task<Resultado> GuardarHorarioAsync(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct, int intervaloMinutos = 15) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        if (intervaloMinutos is < 1 or > 600) return new Error("El intervalo debe ser de 1 a 600 minutos.");
        if (!Enum.IsDefined(dia) || desde >= hasta || desde.Second != 0 || hasta.Second != 0) return new Error("Revisa el día y las horas.");
        if (await datos.ExisteAsync<TramoAgenda>(x => x.Id != id && x.Dia == dia && x.Desde < hasta && x.Hasta > desde, ct)) return new Error("Este tramo se solapa con otro horario.");
        var tramo = id == 0 ? new TramoAgenda() : await datos.BuscarAsync<TramoAgenda>(x => x.Id == id, ct);
        if (tramo is null) return new Error("El tramo no existe.");
        tramo.IntervaloMinutos = intervaloMinutos; tramo.Dia = dia; tramo.Desde = desde; tramo.Hasta = hasta; if (id == 0) datos.Agregar(tramo);
        return Resultado.Ok();
    }, ct);
    public Task<Resultado> EliminarHorarioAsync(int id, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    { var tramo = await datos.BuscarAsync<TramoAgenda>(x => x.Id == id, ct); if (tramo is not null) datos.Quitar(tramo); return Resultado.Ok(); }, ct);
    public Task<Resultado> CerrarDiaAsync(DateOnly dia, string motivo, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    {
        if (dia < Hoy || dia > Hoy.AddDays(730) || motivo.Length > 100) return new Error("Revisa el día y el motivo.");
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        if ((await OcupadasAsync(inicio, inicio.AddDays(1), ct)).Count > 0) return new Error("Primero cancela o rechaza las reservas de ese día y avisa a sus clientes.");
        if (!await datos.ExisteAsync<CierreAgenda>(x => x.Dia == dia, ct)) datos.Agregar(new CierreAgenda { Dia = dia, Motivo = motivo.Trim() });
        return Resultado.Ok();
    }, ct);
    public Task<Resultado> AbrirDiaAsync(int id, CancellationToken ct) => datos.ExclusivoAsync<Resultado>(async () =>
    { var cierre = await datos.BuscarAsync<CierreAgenda>(x => x.Id == id, ct); if (cierre is not null) datos.Quitar(cierre); return Resultado.Ok(); }, ct);
}
