using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class ProductoRepositorio(PeluqueriaDbContext db) : IProductoRepositorio
{
    public Task<Producto?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Productos.Include(p => p.Fotos).SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Producto>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Productos.Include(p => p.Fotos).Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<Pagina<Producto>> BuscarAsync(FiltroProductos filtro, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = Filtrar(filtro);
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .AsNoTracking()
            .Include(p => p.Fotos)
            .OrderBy(p => p.Nombre).ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .AsSplitQuery()
            .ToListAsync(ct);
        return new Pagina<Producto>(elementos, pagina, tamano, total);
    }

    public async Task<IReadOnlyList<Producto>> ListarActivosAsync(CancellationToken ct) =>
        await db.Productos.AsNoTracking().Include(p => p.Fotos)
            .Where(p => p.Activo)
            .OrderBy(p => p.Nombre).ThenBy(p => p.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Producto>> PorReponerAsync(int maximo, CancellationToken ct) =>
        await db.Productos.AsNoTracking()
            .Where(p => p.Activo && p.Stock <= p.StockMinimo)
            .OrderBy(p => p.Stock > 0).ThenBy(p => p.Stock - p.StockMinimo).ThenBy(p => p.Nombre)
            .Take(maximo)
            .ToListAsync(ct);

    public async Task<ResumenInventario> ResumirAsync(CancellationToken ct)
    {
        // Una sola consulta agregada sobre los activos; agrupar por una constante da cero o un grupo
        var cifras = await db.Productos
            .Where(p => p.Activo)
            .GroupBy(_ => 1)
            .Select(g => new ResumenInventario(
                g.Count(),
                g.Sum(p => p.Stock > 0 ? p.Stock : 0),
                g.Count(p => p.Stock > 0 && p.Stock <= p.StockMinimo),
                g.Count(p => p.Stock <= 0),
                g.Sum(p => p.Stock > 0 ? p.Stock * p.PrecioVenta : 0),
                g.Sum(p => p.Stock > 0 ? p.Stock * (p.PrecioCoste ?? 0) : 0)))
            .SingleOrDefaultAsync(ct);
        return cifras ?? ResumenInventario.Vacio;
    }

    public async Task<(IReadOnlyList<string> Categorias, IReadOnlyList<string> Marcas)> SugerenciasAsync(CancellationToken ct)
    {
        var categorias = await db.Productos.Where(p => p.Categoria != null).Select(p => p.Categoria!).Distinct().OrderBy(c => c).ToListAsync(ct);
        var marcas = await db.Productos.Where(p => p.Marca != null).Select(p => p.Marca!).Distinct().OrderBy(m => m).ToListAsync(ct);
        return (categorias, marcas);
    }

    public Task<bool> ExisteReferenciaAsync(string referencia, int? excluirId, CancellationToken ct) =>
        db.Productos.AnyAsync(p => p.Referencia == referencia && (excluirId == null || p.Id != excluirId), ct);

    public Task<bool> ExisteCodigoBarrasAsync(string codigo, int? excluirId, CancellationToken ct) =>
        db.Productos.AnyAsync(p => p.CodigoBarras == codigo && (excluirId == null || p.Id != excluirId), ct);

    public Task<bool> TieneVentasAsync(int id, CancellationToken ct) => db.ProductosVisitas.AnyAsync(l => l.ProductoId == id, ct);

    public async Task<IReadOnlyList<MovimientoStock>> UsadosEnVisitaAsync(int visitaId, CancellationToken ct) =>
        await db.MovimientosStock.AsNoTracking().Include(m => m.Producto)
            .Where(m => m.VisitaId == visitaId && m.Tipo == TipoMovimientoStock.UsoInterno)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

    public async Task<Pagina<MovimientoStock>> MovimientosAsync(int productoId, int pagina, int tamano, CancellationToken ct)
    {
        var consulta = db.MovimientosStock.AsNoTracking().Where(m => m.ProductoId == productoId);
        var total = await consulta.CountAsync(ct);
        var elementos = await consulta
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.Id)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync(ct);
        return new Pagina<MovimientoStock>(elementos, pagina, tamano, total);
    }

    public void Agregar(Producto producto) => db.Productos.Add(producto);

    public void AgregarMovimientos(IEnumerable<MovimientoStock> movimientos) => db.MovimientosStock.AddRange(movimientos);

    public void Eliminar(Producto producto) => db.Productos.Remove(producto);

    private IQueryable<Producto> Filtrar(FiltroProductos filtro)
    {
        var consulta = filtro.Estado switch
        {
            EstadoInventario.Activos => db.Productos.Where(p => p.Activo),
            EstadoInventario.PorReponer => db.Productos.Where(p => p.Activo && p.Stock <= p.StockMinimo),
            EstadoInventario.Archivados => db.Productos.Where(p => !p.Activo),
            _ => db.Productos.AsQueryable(),
        };
        if (!string.IsNullOrWhiteSpace(filtro.Categoria))
        {
            var categoria = filtro.Categoria.Trim();
            consulta = consulta.Where(p => p.Categoria == categoria);
        }

        // Cada palabra en algún campo: «champú arg» encuentra «Champú de argán», y el código del lector, su producto
        foreach (var palabra in BusquedaTexto.Palabras(filtro.Texto))
        {
            var patron = BusquedaTexto.Patron(palabra);
            consulta = consulta.Where(p =>
                EF.Functions.Like(p.Referencia, patron, "\\")
                || EF.Functions.Like(p.Nombre, patron, "\\")
                || (p.CodigoBarras != null && EF.Functions.Like(p.CodigoBarras, patron, "\\"))
                || (p.Marca != null && EF.Functions.Like(p.Marca, patron, "\\"))
                || (p.Categoria != null && EF.Functions.Like(p.Categoria, patron, "\\")));
        }

        return consulta;
    }
}
