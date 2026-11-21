using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Application.Gastos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Gastos;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class GastoConfiguracion : IEntityTypeConfiguration<Gasto>
{
    public void Configure(EntityTypeBuilder<Gasto> builder)
    {
        builder.ToTable("Gastos", t =>
        {
            t.HasCheckConstraint("CK_Gastos_Importe", "[Importe] > 0");
            t.HasCheckConstraint("CK_Gastos_MedioPago", "[MedioPago] BETWEEN 1 AND 5");
        });
        builder.Property(g => g.Concepto).IsRequired().HasMaxLength(Gasto.LongitudConcepto);
        builder.Property(g => g.Nota).HasMaxLength(Gasto.LongitudNota);
        builder.Property(g => g.Importe).HasColumnType("decimal(18,2)");
        builder.Ignore(g => g.NombreTipo);
        builder.HasIndex(g => g.Fecha);
        // Un tipo con gastos no se puede borrar (la aplicación lo impide y la base de datos también)
        builder.HasOne(g => g.Tipo).WithMany().HasForeignKey(g => g.TipoId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Tipos de gasto que edita el salón. La migración que crea la tabla la rellena con los de antes (1 a 14 y 99, «Otros»),
/// con los mismos números, así que los gastos ya anotados conservan su tipo.
/// </summary>
internal sealed class TipoGastoConfiguracion : IEntityTypeConfiguration<TipoGasto>
{
    public void Configure(EntityTypeBuilder<TipoGasto> builder)
    {
        builder.ToTable("TiposGasto");
        builder.Property(t => t.Nombre).IsRequired().HasMaxLength(TipoGasto.LongitudNombre);
        builder.HasIndex(t => t.Nombre).IsUnique();
    }
}

internal sealed class GastoRepositorio(PeluqueriaDbContext db) : IGastoRepositorio
{
    public Task<Gasto?> ObtenerAsync(int id, CancellationToken ct) => db.Gastos.Include(g => g.Tipo).SingleOrDefaultAsync(g => g.Id == id, ct);

    public async Task<IReadOnlyList<Gasto>> ListarAsync(FiltroVisitas filtro, CancellationToken ct) =>
        await Filtrar(db, filtro).AsNoTracking().Include(g => g.Tipo).OrderBy(g => g.Fecha).ThenBy(g => g.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<TotalPorTipo>> TotalesPorTipoAsync(FiltroVisitas filtro, CancellationToken ct)
    {
        var totales = await Filtrar(db, filtro)
            .GroupBy(g => g.Tipo!.Nombre)
            .Select(g => new { Tipo = g.Key, Gastos = g.Count(), Total = g.Sum(x => x.Importe) })
            .OrderByDescending(t => t.Total).ThenBy(t => t.Tipo)
            .ToListAsync(ct);
        return [.. totales.Select(t => new TotalPorTipo(t.Tipo, t.Gastos, t.Total))];
    }

    public async Task<IReadOnlyList<TipoGasto>> TiposAsync(CancellationToken ct) =>
        await db.TiposGasto.OrderBy(t => t.Orden).ThenBy(t => t.Nombre).ToListAsync(ct);

    public Task<TipoGasto?> TipoAsync(int id, CancellationToken ct) => db.TiposGasto.SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyDictionary<int, int>> GastosPorTipoAsync(CancellationToken ct) =>
        await db.Gastos.GroupBy(g => g.TipoId).Select(g => new { g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);

    public void AgregarTipo(TipoGasto tipo) => db.TiposGasto.Add(tipo);

    public void EliminarTipo(TipoGasto tipo) => db.TiposGasto.Remove(tipo);

    public async Task<ResumenGastos> ResumirAsync(FiltroVisitas filtro, CancellationToken ct)
    {
        // Agrupar por una constante da cero o un grupo: SingleOrDefault lo dice tal cual
        var resumen = await Filtrar(db, filtro)
            .GroupBy(_ => 1)
            .Select(g => new ResumenGastos(g.Count(), g.Sum(x => x.Importe), g.Where(x => x.MedioPago == MedioPagoGasto.Efectivo).Sum(x => x.Importe)))
            .SingleOrDefaultAsync(ct);
        return resumen ?? ResumenGastos.Vacio;
    }

    public void Agregar(Gasto gasto) => db.Gastos.Add(gasto);

    public void Eliminar(Gasto gasto) => db.Gastos.Remove(gasto);

    /// <summary>Los gastos del periodo; con filtro de cliente, ninguno (no son de un cliente).</summary>
    internal static IQueryable<Gasto> Filtrar(PeluqueriaDbContext db, FiltroVisitas filtro)
    {
        var consulta = db.Gastos.AsQueryable();
        if (filtro.ClienteId is not null)
            return consulta.Where(_ => false);
        if (filtro.Inicio is { } inicio)
            consulta = consulta.Where(g => g.Fecha >= inicio);
        if (filtro.FinExcluido is { } fin)
            consulta = consulta.Where(g => g.Fecha < fin);
        return consulta;
    }
}
