using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class ClienteRepositorio(PeluqueriaDbContext db) : IClienteRepositorio
{
    public Task<Cliente?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Clientes.Include(c => c.Faltas).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Pagina<Cliente>> BuscarAsync(string? texto, Sexo? sexo, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.Clientes.AsNoTracking().Include(c => c.Faltas).AsQueryable();
        if (sexo is { } s)
            consulta = consulta.Where(c => c.Sexo == s);

        // Cada palabra tiene que aparecer en algún campo: «ana gar» encuentra a Ana García
        foreach (var palabra in (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var patron = $"%{EscaparLike(palabra)}%";
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Nombre, patron, "\\")
                || EF.Functions.Like(c.Apellido, patron, "\\")
                || (c.Apodo != null && EF.Functions.Like(c.Apodo, patron, "\\"))
                || (c.Telefono != null && EF.Functions.Like(c.Telefono, patron, "\\"))
                || (c.Email != null && EF.Functions.Like(c.Email, patron, "\\")));
        }

        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .OrderByDescending(c => c.PendienteCompletar).ThenBy(c => c.Nombre).ThenBy(c => c.Apellido).ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);
        return new Pagina<Cliente>(elementos, pagina, tamano, total);
    }

    public Task<bool> ExisteEmailAsync(string email, int? excluirId, CancellationToken ct) =>
        db.Clientes.AnyAsync(c => c.Email == email && (excluirId == null || c.Id != excluirId), ct);

    public Task<bool> ExisteTelefonoAsync(string telefono, int? excluirId, CancellationToken ct) =>
        db.Clientes.AnyAsync(c => c.Telefono == telefono && (excluirId == null || c.Id != excluirId), ct);

    public Task<bool> TieneVisitasAsync(int id, CancellationToken ct) => db.Visitas.AnyAsync(v => v.ClienteId == id, ct);
    public Task<List<Cliente>> BuscarPorContactoAsync(string telefono, string correo, CancellationToken ct) => ConsultaContactos.Candidatos(db.Clientes.AsNoTracking(),telefono,correo).ToListAsync(ct);

    public Task<List<Cliente>> PorTelefonoAsync(string telefono, CancellationToken ct)
    {
        var numero = new string(telefono.Where(c => char.IsAsciiDigit(c) || c == '+').ToArray());
        var local = numero.StartsWith("+34", StringComparison.Ordinal) ? numero[3..] : numero.StartsWith("0034", StringComparison.Ordinal) ? numero[4..] : numero;
        return db.Clientes
            .Where(c => c.Telefono != null)
            .Where(c => c.Telefono!.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == local
                || c.Telefono!.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == "+34" + local
                || c.Telefono!.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == "0034" + local)
            .ToListAsync(ct);
    }
    public Task<bool> TieneCitasAsync(int id, CancellationToken ct) => db.ReservasCitas.AnyAsync(v => v.ClienteId == id, ct);

    public void Agregar(Cliente cliente) => db.Clientes.Add(cliente);

    public void Eliminar(Cliente cliente) => db.Clientes.Remove(cliente);

    private static string EscaparLike(string texto) =>
        texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");
}

internal sealed class ServicioRepositorio(PeluqueriaDbContext db) : IServicioRepositorio
{
    public Task<Servicio?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Servicios.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<IReadOnlyList<Servicio>> ListarAsync(Sexo? sexo, bool incluirArchivados, CancellationToken ct) =>
        BuscarAsync(null, sexo, incluirArchivados, ct);

    public async Task<IReadOnlyList<Servicio>> BuscarAsync(string? texto, Sexo? sexo, bool incluirArchivados, CancellationToken ct)
    {
        var consulta = db.Servicios.AsNoTracking();
        foreach (var palabra in BusquedaTexto.Palabras(texto))
        {
            var patron = BusquedaTexto.Patron(palabra);
            consulta = consulta.Where(s => EF.Functions.Like(s.Nombre, patron, "\\")
                || (s.DescripcionPublica != null && EF.Functions.Like(s.DescripcionPublica, patron, "\\")));
        }
        if (!incluirArchivados)
            consulta = consulta.Where(s => s.Activo);
        if (sexo is { } s)
            consulta = consulta.Where(x => x.Sexo == s);
        return await consulta.OrderByDescending(x => x.Activo).ThenBy(x => x.Nombre).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Servicio>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Servicios.Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public Task<bool> TieneVisitasAsync(int id, CancellationToken ct) => db.LineasVisita.AnyAsync(l => l.ServicioId == id, ct);

    public async Task CambiarDualidadCitasAsync(int servicioId, bool dualidad, DateTime desde, CancellationToken ct)
    {
        var citas = await db.ReservasCitas
            .Where(c => c.ServicioId == servicioId && c.Fin > desde && c.Origen != PeluqueriaLucia.Domain.Agenda.OrigenCita.Bloqueo
                && (c.Estado == PeluqueriaLucia.Domain.Agenda.EstadoCita.Confirmada || c.Estado == PeluqueriaLucia.Domain.Agenda.EstadoCita.Pendiente
                    || c.Estado == PeluqueriaLucia.Domain.Agenda.EstadoCita.PendienteAceptacion))
            .ToListAsync(ct);
        citas.ForEach(c => c.Dualidad = dualidad);
    }

    public void Agregar(Servicio servicio) => db.Servicios.Add(servicio);

    public void Eliminar(Servicio servicio) => db.Servicios.Remove(servicio);
}

internal sealed class VisitaRepositorio(PeluqueriaDbContext db) : IVisitaRepositorio
{
    public Task<Visita?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Visitas.Include(v => v.Cliente).Include(v => v.Lineas).Include(v => v.Productos).AsSplitQuery().SingleOrDefaultAsync(v => v.Id == id, ct);

    public async Task<Pagina<Visita>> ListarAsync(FiltroVisitas filtro, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = Filtrar(filtro);
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Lineas)
            .Include(v => v.Productos)
            .OrderByDescending(v => v.Fecha).ThenByDescending(v => v.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .AsSplitQuery()
            .ToListAsync(ct);
        return new Pagina<Visita>(elementos, pagina, tamano, total);
    }

    public async Task<IReadOnlyList<Visita>> ListarTodasAsync(FiltroVisitas filtro, CancellationToken ct) =>
        await Filtrar(filtro)
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Lineas)
            .Include(v => v.Productos)
            .OrderBy(v => v.Fecha).ThenBy(v => v.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<ResumenVisitas> ResumirAsync(FiltroVisitas filtro, CancellationToken ct)
    {
        var visitas = Filtrar(filtro);
        var cobradas = visitas.Where(v => v.Estado == EstadoVisita.Cobrada);

        // Consultas agregadas pequeñas: ninguna trae filas a memoria.
        // Agrupar por una constante da cero o un grupo: SingleOrDefault lo dice tal cual y EF no avisa (First sin OrderBy sí).
        var porVisita = await visitas
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Visitas = g.Count(),
                Efectivo = g.Where(v => v.Estado == EstadoVisita.Cobrada).Sum(v => v.TotalEfectivo ?? 0),
                Tarjeta = g.Where(v => v.Estado == EstadoVisita.Cobrada).Sum(v => v.TotalTarjeta ?? 0),
            })
            .SingleOrDefaultAsync(ct);
        if (porVisita is null)
            return ResumenVisitas.Vacio;

        var lineas = visitas.SelectMany(v => v.Lineas);
        var servicios = await lineas.CountAsync(ct);
        var facturado = await lineas.SumAsync(l => (decimal?)l.Precio, ct) ?? 0;
        var cobrado = await cobradas.SelectMany(v => v.Lineas).SumAsync(l => (decimal?)l.Precio, ct) ?? 0;

        // Lo usado en el servicio no se vende (va incluido, a 0,00 €): no cuenta como producto vendido
        var productos = await visitas
            .SelectMany(v => v.Productos.Where(p => p.Uso == UsoProducto.Venta).Select(p => new { p.Cantidad, p.Importe, Cobrada = v.Estado == EstadoVisita.Cobrada }))
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Unidades = g.Sum(p => p.Cantidad),
                Facturado = g.Sum(p => p.Importe),
                Cobrado = g.Where(p => p.Cobrada).Sum(p => p.Importe),
            })
            .SingleOrDefaultAsync(ct);

        return new ResumenVisitas(porVisita.Visitas, servicios, facturado + (productos?.Facturado ?? 0), cobrado + (productos?.Cobrado ?? 0),
            porVisita.Efectivo, porVisita.Tarjeta, productos?.Unidades ?? 0);
    }

    public Task<ReservaCita?> CitaAsync(Guid id, CancellationToken ct) => db.ReservasCitas.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<Visita?> DeCitaAsync(Guid reservaId, CancellationToken ct) =>
        db.Visitas.Include(v => v.Cliente).Include(v => v.Lineas).Include(v => v.Productos).AsSplitQuery()
            .SingleOrDefaultAsync(v => v.ReservaId == reservaId, ct);

    public async Task<Visita?> SinCitaDelDiaAsync(int clienteId, DateTime hora, CancellationToken ct)
    {
        var inicio = hora.Date;
        var fin = inicio.AddDays(1);
        var delDia = await db.Visitas
            .Where(v => v.ClienteId == clienteId && v.ReservaId == null && v.Fecha >= inicio && v.Fecha < fin)
            .ToListAsync(ct);
        return delDia.OrderBy(v => Math.Abs((v.Fecha - hora).Ticks)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<ReservaCita>> CitasPorCobrarAsync(DateOnly dia, CancellationToken ct)
    {
        var inicio = dia.ToDateTime(TimeOnly.MinValue);
        var fin = inicio.AddDays(1);
        return await db.ReservasCitas
            .AsNoTracking()
            .Include(c => c.Cliente)
            .Where(c => c.Inicio >= inicio && c.Inicio < fin && c.Origen != OrigenCita.Bloqueo
                && (c.Estado == EstadoCita.Confirmada || c.Estado == EstadoCita.Completada)
                && !db.Visitas.Any(v => v.ReservaId == c.Id
                    || (c.ClienteId != null && v.ReservaId == null && v.ClienteId == c.ClienteId && v.Fecha >= inicio && v.Fecha < fin)))
            .OrderBy(c => c.Inicio)
            .ToListAsync(ct);
    }

    public void Agregar(Visita visita) => db.Visitas.Add(visita);

    public void Eliminar(Visita visita) => db.Visitas.Remove(visita);

    /// <summary>Filtro por rango de fechas que aprovecha el índice de Fecha (sin funciones sobre la columna).</summary>
    private IQueryable<Visita> Filtrar(FiltroVisitas filtro)
    {
        var consulta = db.Visitas.AsQueryable();
        if (filtro.Inicio is { } inicio)
            consulta = consulta.Where(v => v.Fecha >= inicio);
        if (filtro.FinExcluido is { } fin)
            consulta = consulta.Where(v => v.Fecha < fin);
        if (filtro.ClienteId is { } clienteId)
            consulta = consulta.Where(v => v.ClienteId == clienteId);
        return consulta;
    }
}
