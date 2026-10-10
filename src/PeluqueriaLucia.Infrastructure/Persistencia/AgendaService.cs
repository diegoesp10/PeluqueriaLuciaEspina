using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class AgendaService(PeluqueriaDbContext db, TimeProvider reloj) : IAgendaService
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
    private DateTime Utc => reloj.GetUtcNow().UtcDateTime;
    private DateTime Ahora => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona).DateTime;
    public DateOnly Hoy => DateOnly.FromDateTime(Ahora);
    public async Task<IReadOnlyList<ServicioAgenda>> ServiciosAsync(CancellationToken ct) =>
        await db.Servicios.AsNoTracking().Where(x => x.Activo && x.Publicado).OrderBy(x => x.Nombre)
            .Select(x => new ServicioAgenda(x.Id, x.Nombre, x.DuracionMinutos)).ToListAsync(ct);
    private async Task<int> DuracionAsync(int? servicio, CancellationToken ct) => servicio is null ? 30 :
        await db.Servicios.Where(x => x.Id == servicio && x.Activo && x.Publicado).Select(x => x.DuracionMinutos).SingleOrDefaultAsync(ct);
    private IQueryable<ReservaCita> Ocupadas() => db.ReservasCitas.Where(x => x.Estado == EstadoCita.Confirmada || (x.Estado == EstadoCita.Pendiente && x.CaducaUtc > Utc));

    public async Task<DisponibilidadAgenda> DisponibilidadAsync(DateOnly dia, int? servicioId, CancellationToken ct)
    {
        var minutos = await DuracionAsync(servicioId, ct);
        if (dia < Hoy || dia > Hoy.AddDays(90) || minutos is < 5 or > 600 || await db.CierresAgenda.AnyAsync(x => x.Dia == dia, ct))
            return new(dia, minutos, []);
        var tramos = await db.TramosAgenda.AsNoTracking().Where(x => x.Dia == dia.DayOfWeek).OrderBy(x => x.Desde).ToListAsync(ct);
        var desde = dia.ToDateTime(TimeOnly.MinValue); var hasta = desde.AddDays(1);
        var ocupadas = await Ocupadas().Where(x => x.Inicio < hasta && x.Fin > desde).Select(x => new { x.Inicio, x.Fin }).ToListAsync(ct);
        var horas = new List<TimeOnly>();
        foreach (var tramo in tramos)
            for (var t = dia.ToDateTime(tramo.Desde); t.AddMinutes(minutos) <= dia.ToDateTime(tramo.Hasta); t = t.AddMinutes(15))
                if (t >= Ahora.AddMinutes(30) && !Zona.IsInvalidTime(t) && !Zona.IsAmbiguousTime(t) &&
                    !Zona.IsInvalidTime(t.AddMinutes(minutos)) && !Zona.IsAmbiguousTime(t.AddMinutes(minutos)) &&
                    !ocupadas.Any(x => x.Inicio < t.AddMinutes(minutos) && x.Fin > t)) horas.Add(TimeOnly.FromDateTime(t));
        return new(dia, minutos, horas.Distinct().Order().ToList());
    }

    public static string? NormalizarTelefono(string? valor)
    {
        if (valor is null || valor.Length > 30 || valor.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')'))) return null;
        var numero = Regex.Replace(valor, "[ ()-]", "");
        if (numero.StartsWith("0034")) numero = "+34" + numero[4..];
        if (Regex.IsMatch(numero, "^[67][0-9]{8}$")) numero = "+34" + numero;
        return Regex.IsMatch(numero, "^\\+34[67][0-9]{8}$") ? numero : null;
    }

    /// <summary>Bloqueo SQL transaccional compartido por todas las instancias. Disponibilidad y escritura atómicas.</summary>
    private async Task<T> ExclusivoAsync<T>(Func<Task<T>> accion, CancellationToken ct) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'LuciaAgenda', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Agenda ocupada temporalmente', 1;", ct);
            var resultado = await accion();
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return resultado;
        });

    public Task<Resultado<Guid>> SolicitarAsync(SolicitarCita d, CancellationToken ct) => ExclusivoAsync<Resultado<Guid>>(async () =>
    {
        var telefono = NormalizarTelefono(d.Telefono);
        if (telefono is null) return new Error("Introduce un móvil español válido: nueve cifras, empezando por 6 o 7.", "Telefono");
        if (d.SolicitudId == Guid.Empty || d.Nombre?.Length > 80) return new Error("Revisa los datos de la solicitud.");
        var previa = await db.ReservasCitas.SingleOrDefaultAsync(x => x.SolicitudId == d.SolicitudId, ct);
        if (previa is not null) return previa.Telefono == telefono ? Resultado.Ok(previa.Id) : new Error("La solicitud no es válida. Recarga el formulario.");
        var ip = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(d.Ip)));
        if (await db.ReservasCitas.CountAsync(x => x.IpHash == ip && x.CreadaUtc > Utc.AddHours(-1), ct) >= 5 ||
            await Ocupadas().CountAsync(x => x.Telefono == telefono && x.Fin > Ahora, ct) >= 2)
            return new Error("Ya hay varias solicitudes desde tus datos. Contacta con el salón para revisarlas.");
        var disponibles = await DisponibilidadAsync(d.Dia, d.ServicioId, ct);
        if (!disponibles.Horas.Contains(d.Hora)) return new Error("Ese horario acaba de ocuparse o no está disponible. Elige otro.", "Hora");
        var nombreServicio = d.ServicioId is null ? "Consulta con Lucía" : await db.Servicios.Where(x => x.Id == d.ServicioId).Select(x => x.Nombre).SingleAsync(ct);
        var cita = new ReservaCita { Id = Guid.NewGuid(), SolicitudId = d.SolicitudId, Inicio = d.Dia.ToDateTime(d.Hora),
            Fin = d.Dia.ToDateTime(d.Hora).AddMinutes(disponibles.Minutos), Telefono = telefono, Nombre = d.Nombre?.Trim() ?? "",
            ServicioId = d.ServicioId, ServicioNombre = nombreServicio, Estado = EstadoCita.Pendiente,
            CreadaUtc = Utc, ActualizadaUtc = Utc, CaducaUtc = Utc.AddHours(24), IpHash = ip };
        db.ReservasCitas.Add(cita);
        return Resultado.Ok(cita.Id);
    }, ct);

    public Task<Resultado> GestionarAsync(GestionarCita d, CancellationToken ct) => ExclusivoAsync<Resultado>(async () =>
    {
        var r = await db.ReservasCitas.SingleOrDefaultAsync(x => x.Id == d.Id, ct);
        if (r is null) return new Error("La reserva no existe.");
        var finAnterior = r.Fin;
        if (Convert.ToBase64String(r.Version) != d.Version) return new Error("La reserva ha cambiado en otra pestaña. Actualiza la agenda.");
        if (d.Nota?.Length > 500 || !Enum.IsDefined(d.Estado) || d.Estado == EstadoCita.Pendiente) return new Error("Revisa los datos de la gestión.");
        if (r.Estado is EstadoCita.Rechazada or EstadoCita.Cancelada or EstadoCita.Completada) return new Error("Esta reserva ya está cerrada.");
        if (await db.MensajesCitas.AnyAsync(x => x.ReservaId == r.Id && x.Estado == EstadoSms.Enviando, ct)) return new Error("Se está enviando el aviso de esta cita. Espera unos segundos y actualiza antes de cambiarla.");
        if (d.Estado == EstadoCita.Confirmada)
        {
            if (!d.TelefonoValidado) return new Error("Confirma que has validado la reserva contactando con su teléfono.");
            if (r.Inicio <= Ahora || (r.Estado == EstadoCita.Pendiente && r.CaducaUtc <= Utc)) return new Error("La solicitud ha caducado. Acuerda otra hora con el cliente.");
            if (d.Minutos is < 15 or > 600 || d.Minutos % 15 != 0) return new Error("La duración debe ser de 15 a 600 minutos, en tramos de 15.");
            var fin = r.Inicio.AddMinutes(d.Minutos); var dia = DateOnly.FromDateTime(r.Inicio);
            if (Zona.IsInvalidTime(r.Inicio) || Zona.IsAmbiguousTime(r.Inicio) || Zona.IsInvalidTime(fin) || Zona.IsAmbiguousTime(fin)) return new Error("El intervalo coincide con un cambio de hora. Acuerda otro horario.");
            if (DateOnly.FromDateTime(fin) != dia || await db.CierresAgenda.AnyAsync(x => x.Dia == dia, ct) ||
                !await db.TramosAgenda.AnyAsync(x => x.Dia == dia.DayOfWeek && x.Desde <= TimeOnly.FromDateTime(r.Inicio) && x.Hasta >= TimeOnly.FromDateTime(fin), ct))
                return new Error("La duración queda fuera del horario de apertura.");
            if (await Ocupadas().AnyAsync(x => x.Id != r.Id && x.Inicio < fin && x.Fin > r.Inicio, ct))
                return new Error("La duración se solapa con otra reserva. Ajusta el tiempo o rechaza la solicitud.");
            r.Fin = fin; r.TelefonoValidado = true;
        }
        else if (d.Estado == EstadoCita.Completada && (r.Estado != EstadoCita.Confirmada || r.Inicio > Ahora)) return new Error("Solo puedes completar una cita confirmada que ya haya comenzado.");
        else if (d.Estado == EstadoCita.Rechazada && r.Estado != EstadoCita.Pendiente) return new Error("Una cita confirmada se cancela, no se rechaza.");
        var cambio = r.Estado != d.Estado || finAnterior != r.Fin;
        r.Estado = d.Estado; r.NotaInterna = d.Nota?.Trim() ?? ""; r.AdministradorId = d.AdministradorId; r.ActualizadaUtc = Utc;
        if (cambio && d.Estado is EstadoCita.Confirmada or EstadoCita.Rechazada or EstadoCita.Cancelada)
        {
            var texto = d.Estado == EstadoCita.Confirmada
                ? $"Lucía Peluquería Ecológica: cita confirmada el {r.Inicio:dd/MM/yyyy} a las {r.Inicio:HH:mm}. Duración {d.Minutos} min. Para cambios llama al 659306659."
                : $"Lucía Peluquería Ecológica: tu solicitud del {r.Inicio:dd/MM/yyyy HH:mm} no está confirmada o se ha cancelado. Llámanos al 659306659 para acordar otro momento.";
            // Una actualización invalida mensajes todavía no enviados para evitar confirmaciones antiguas.
            await db.MensajesCitas.Where(x => x.ReservaId == r.Id && x.Estado == EstadoSms.Pendiente).ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoSms.Obsoleto), ct);
            db.MensajesCitas.Add(new MensajeCita { Id = Guid.NewGuid(), ReservaId = r.Id, Telefono = r.Telefono, Texto = texto, CreadoUtc = Utc, Estado = EstadoSms.Pendiente });
        }
        return Resultado.Ok();
    }, ct);

    public async Task<IReadOnlyList<ReservaCita>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        if (hasta < desde || hasta > desde.AddDays(31)) hasta = desde.AddDays(31);
        var a = desde.ToDateTime(TimeOnly.MinValue); var b = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await db.ReservasCitas.AsNoTracking().Where(x => x.Inicio >= a && x.Inicio < b).OrderBy(x => x.Inicio).Take(500).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<MensajeCita>> MensajesAsync(CancellationToken ct) => await db.MensajesCitas.AsNoTracking().OrderByDescending(x => x.CreadoUtc).Take(100).ToListAsync(ct);
    public async Task<AvisoAgenda> AvisosAsync(CancellationToken ct) => new(
        await db.ReservasCitas.CountAsync(x => x.Estado == EstadoCita.Pendiente && x.CaducaUtc > Utc && x.Inicio > Ahora, ct),
        await db.ReservasCitas.Select(x => (DateTime?)x.CreadaUtc).MaxAsync(ct));
    public async Task<IReadOnlyList<ReservaCita>> PendientesAsync(CancellationToken ct) => await db.ReservasCitas.AsNoTracking()
        .Where(x => x.Estado == EstadoCita.Pendiente && x.CaducaUtc > Utc && x.Inicio > Ahora).OrderBy(x => x.Inicio).Take(500).ToListAsync(ct);
    public async Task<IReadOnlyList<TramoAgenda>> HorariosAsync(CancellationToken ct) => await db.TramosAgenda.AsNoTracking().OrderBy(x => x.Dia).ThenBy(x => x.Desde).ToListAsync(ct);
    public async Task<IReadOnlyList<CierreAgenda>> CierresAsync(CancellationToken ct) => await db.CierresAgenda.AsNoTracking().Where(x => x.Dia >= Hoy).OrderBy(x => x.Dia).ToListAsync(ct);
    public Task<Resultado> GuardarHorarioAsync(int id, DayOfWeek dia, TimeOnly desde, TimeOnly hasta, CancellationToken ct) => ExclusivoAsync<Resultado>(async () =>
    {
        if (!Enum.IsDefined(dia) || desde >= hasta || desde.Second != 0 || hasta.Second != 0) return new Error("Revisa el día y las horas.");
        if (await db.TramosAgenda.AnyAsync(x => x.Id != id && x.Dia == dia && x.Desde < hasta && x.Hasta > desde, ct)) return new Error("Este tramo se solapa con otro horario.");
        var tramo = id == 0 ? new TramoAgenda() : await db.TramosAgenda.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (tramo is null) return new Error("El tramo no existe.");
        tramo.Dia = dia; tramo.Desde = desde; tramo.Hasta = hasta; if (id == 0) db.TramosAgenda.Add(tramo);
        return Resultado.Ok();
    }, ct);
    public Task<Resultado> EliminarHorarioAsync(int id, CancellationToken ct) => ExclusivoAsync<Resultado>(async () =>
    { var tramo = await db.TramosAgenda.FindAsync([id], ct); if (tramo is not null) db.TramosAgenda.Remove(tramo); return Resultado.Ok(); }, ct);
    public Task<Resultado> CerrarDiaAsync(DateOnly dia, string motivo, CancellationToken ct) => ExclusivoAsync<Resultado>(async () =>
    {
        if (dia < Hoy || dia > Hoy.AddDays(730) || motivo.Length > 100) return new Error("Revisa el día y el motivo.");
        var inicio = dia.ToDateTime(TimeOnly.MinValue); var fin = inicio.AddDays(1);
        if (await Ocupadas().AnyAsync(x => x.Inicio < fin && x.Fin > inicio, ct)) return new Error("Primero cancela o rechaza las reservas de ese día y avisa a sus clientes.");
        if (!await db.CierresAgenda.AnyAsync(x => x.Dia == dia, ct)) db.CierresAgenda.Add(new CierreAgenda { Dia = dia, Motivo = motivo.Trim() });
        return Resultado.Ok();
    }, ct);
    public Task<Resultado> AbrirDiaAsync(int id, CancellationToken ct) => ExclusivoAsync<Resultado>(async () =>
    { var cierre = await db.CierresAgenda.FindAsync([id], ct); if (cierre is not null) db.CierresAgenda.Remove(cierre); return Resultado.Ok(); }, ct);
    public async Task<Resultado> MarcarSmsManualAsync(Guid id, CancellationToken ct)
    {
        var n = await db.MensajesCitas.Where(x => x.Id == id && (x.Estado == EstadoSms.Pendiente || x.Estado == EstadoSms.Error || x.Estado == EstadoSms.Incierto))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Estado, EstadoSms.Manual).SetProperty(x => x.ProcesadoUtc, Utc), ct);
        return n == 1 ? Resultado.Ok() : new Error("El aviso ya se ha gestionado o se está enviando.");
    }
}
