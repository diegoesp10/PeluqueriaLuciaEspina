using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Application.Sorteos;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Sorteos;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class SorteoConfiguracion : IEntityTypeConfiguration<Sorteo>
{
    public void Configure(EntityTypeBuilder<Sorteo> builder)
    {
        builder.ToTable("Sorteos", t =>
        {
            t.HasCheckConstraint("CK_Sorteos_Estado", "[Estado] IN (0, 1, 2)");
            // El premio es un servicio o un producto, nunca los dos
            t.HasCheckConstraint("CK_Sorteos_Premio", "([TipoPremio] = 0 AND [ProductoId] IS NULL) OR ([TipoPremio] = 1 AND [ServicioId] IS NULL)");
        });
        builder.Property(s => s.Titulo).IsRequired().HasMaxLength(Sorteo.LongitudTitulo);
        builder.Property(s => s.TituloEn).HasMaxLength(Sorteo.LongitudTitulo);
        builder.Property(s => s.Descripcion).HasMaxLength(Sorteo.LongitudDescripcion);
        builder.Property(s => s.DescripcionEn).HasMaxLength(Sorteo.LongitudDescripcion);
        builder.Property(s => s.Premio).IsRequired().HasMaxLength(Producto.LongitudNombre);
        builder.Property(s => s.PremioEn).HasMaxLength(Producto.LongitudNombre);
        builder.Property(s => s.MotivoCancelacion).HasMaxLength(Sorteo.LongitudMotivo);
        builder.Property(s => s.Version).IsRowVersion();
        builder.Ignore(s => s.Ganador);
        builder.Ignore(s => s.Abierto);

        // Si el premio desaparece del catálogo o de la tienda, el sorteo conserva su nombre
        builder.HasOne<Servicio>().WithMany().HasForeignKey(s => s.ServicioId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Producto>().WithMany().HasForeignKey(s => s.ProductoId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Participantes).WithOne().HasForeignKey(p => p.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Participantes).HasField("_participantes").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.Extracciones).WithOne(e => e.Sorteo).HasForeignKey(e => e.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Extracciones).HasField("_extracciones").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.Anuncios).WithOne(a => a.Sorteo).HasForeignKey(a => a.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Anuncios).HasField("_anuncios").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.Estado, s.FechaSorteo });
    }
}

internal sealed class ParticipanteSorteoConfiguracion : IEntityTypeConfiguration<ParticipanteSorteo>
{
    public void Configure(EntityTypeBuilder<ParticipanteSorteo> builder)
    {
        builder.ToTable("ParticipantesSorteos");
        // Borrar la ficha de un cliente lo saca de los sorteos (si ganó alguno, la extracción guarda su nombre)
        builder.HasOne(p => p.Cliente).WithMany().HasForeignKey(p => p.ClienteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.SorteoId, p.ClienteId }).IsUnique();
    }
}

internal sealed class ExtraccionSorteoConfiguracion : IEntityTypeConfiguration<ExtraccionSorteo>
{
    public void Configure(EntityTypeBuilder<ExtraccionSorteo> builder)
    {
        builder.ToTable("ExtraccionesSorteos", t => t.HasCheckConstraint("CK_ExtraccionesSorteos_Participantes", "[Participantes] >= 1"));
        builder.Property(e => e.Ganador).IsRequired().HasMaxLength(Cliente.LongitudNombre * 2 + 1);
        builder.Property(e => e.Motivo).HasMaxLength(Sorteo.LongitudMotivo);
        builder.HasOne(e => e.Cliente).WithMany().HasForeignKey(e => e.ClienteId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class AnuncioSorteoConfiguracion : IEntityTypeConfiguration<AnuncioSorteo>
{
    public void Configure(EntityTypeBuilder<AnuncioSorteo> builder)
    {
        builder.ToTable("AnunciosSorteos", t => t.HasCheckConstraint("CK_AnunciosSorteos_Fechas", "[Hasta] >= [Desde]"));
        builder.Property(a => a.Texto).IsRequired().HasMaxLength(AnuncioSorteo.LongitudTexto);
        builder.Property(a => a.TextoEn).HasMaxLength(AnuncioSorteo.LongitudTexto);
        builder.HasIndex(a => new { a.Desde, a.Hasta });
    }
}

internal sealed class SorteoRepositorio(PeluqueriaDbContext db) : ISorteoRepositorio
{
    private IQueryable<Sorteo> Completos => db.Sorteos
        .Include(s => s.Participantes).ThenInclude(p => p.Cliente)
        .Include(s => s.Extracciones)
        .Include(s => s.Anuncios)
        .AsSplitQuery();

    public Task<Sorteo?> ObtenerAsync(int id, CancellationToken ct) => Completos.SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Sorteo>> ListarAsync(bool historial, CancellationToken ct) =>
        historial
            ? await Completos.AsNoTracking().Where(s => s.Estado != EstadoSorteo.Abierto).OrderByDescending(s => s.ActualizadoEn).ThenByDescending(s => s.Id).ToListAsync(ct)
            : await Completos.AsNoTracking().Where(s => s.Estado == EstadoSorteo.Abierto).OrderBy(s => s.FechaSorteo).ThenBy(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AnuncioPublico>> AnunciosVisiblesAsync(DateOnly dia, CancellationToken ct) =>
        await db.AnunciosSorteos.AsNoTracking()
            .Where(a => a.Desde <= dia && a.Hasta >= dia && a.Sorteo!.Estado != EstadoSorteo.Cancelado)
            .OrderBy(a => a.Sorteo!.FechaSorteo).ThenBy(a => a.Id)
            .Select(a => new AnuncioPublico(a.Id, a.SorteoId, a.Sorteo!.Titulo, a.Sorteo.TituloEn, a.Sorteo.Descripcion, a.Sorteo.DescripcionEn,
                a.Sorteo.Premio, a.Sorteo.PremioEn, a.Sorteo.TipoPremio, a.Sorteo.FechaSorteo, a.Sorteo.Estado == EstadoSorteo.Realizado,
                a.Texto, a.TextoEn, a.Hasta))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> ClientesAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Clientes.Where(c => ids.Contains(c.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> ClientesConVisitasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return await db.Clientes
            .Where(c => db.Visitas.Any(v => v.ClienteId == c.Id && v.Fecha >= inicio && v.Fecha < fin))
            .OrderBy(c => c.Nombre).ThenBy(c => c.Apellido)
            .ToListAsync(ct);
    }

    public void Agregar(Sorteo sorteo) => db.Sorteos.Add(sorteo);

    public void Eliminar(Sorteo sorteo) => db.Sorteos.Remove(sorteo);
}

/// <summary>Azar criptográfico: cada participante tiene la misma probabilidad y nadie puede predecir el resultado.</summary>
internal sealed class AzarSeguro : IAzar
{
    public int Siguiente(int maximo) => RandomNumberGenerator.GetInt32(maximo);
}
