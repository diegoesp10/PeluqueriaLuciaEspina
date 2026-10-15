using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
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

    public void Agregar(Servicio servicio) => db.Servicios.Add(servicio);

    public void Eliminar(Servicio servicio) => db.Servicios.Remove(servicio);
}

internal sealed class VisitaRepositorio(PeluqueriaDbContext db) : IVisitaRepositorio
{
    public Task<Visita?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Visitas.Include(v => v.Cliente).Include(v => v.Lineas).FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<Pagina<Visita>> ListarAsync(FiltroVisitas filtro, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = Filtrar(filtro);
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Include(v => v.Lineas)
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
            .OrderBy(v => v.Fecha).ThenBy(v => v.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<ResumenVisitas> ResumirAsync(FiltroVisitas filtro, CancellationToken ct)
    {
        var visitas = Filtrar(filtro);
        var cobradas = visitas.Where(v => v.Estado == EstadoVisita.Cobrada);

        // Tres consultas agregadas pequeñas: ninguna trae filas a memoria.
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

        return new ResumenVisitas(porVisita.Visitas, servicios, facturado, cobrado, porVisita.Efectivo, porVisita.Tarjeta);
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
