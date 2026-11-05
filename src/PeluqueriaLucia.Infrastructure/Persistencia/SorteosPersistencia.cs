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
            t.HasCheckConstraint("CK_Sorteos_Premio", "([TipoPremio] = 0 AND [ProductoId] IS NULL) OR ([TipoPremio] = 1 AND [ServicioId] IS NULL)");
        });
        builder.Property(s => s.Titulo).IsRequired().HasMaxLength(Sorteo.LongitudTitulo);
        builder.Property(s => s.TituloEn).HasMaxLength(Sorteo.LongitudTitulo);
        builder.Property(s => s.Descripcion).HasMaxLength(Sorteo.LongitudDescripcion);
        builder.Property(s => s.DescripcionEn).HasMaxLength(Sorteo.LongitudDescripcion);
        builder.Property(s => s.MotivoCancelacion).HasMaxLength(Sorteo.LongitudMotivo);
        builder.Property(s => s.Version).IsRowVersion();
        builder.Ignore(s => s.Ganador);
        builder.Ignore(s => s.Abierto);

        // Columnas de cuando un sorteo tenía un solo premio (migración SorteosYCostesDeProductos). Ya no se escriben: los
        // premios están en PremiosSorteos (la migración ProductosDeSorteoYVariosPremios copió ahí los que había). Se
        // conservan para no borrar datos; los sorteos nuevos las dejan a nulo.
        builder.Property<int?>("TipoPremio");
        builder.Property<int?>("ServicioId");
        builder.Property<int?>("ProductoId");
        builder.Property<string?>("Premio").HasMaxLength(PremioSorteo.LongitudNombre);
        builder.Property<string?>("PremioEn").HasMaxLength(PremioSorteo.LongitudNombre);
        builder.HasOne<Servicio>().WithMany().HasForeignKey("ServicioId").OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Producto>().WithMany().HasForeignKey("ProductoId").OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(s => s.Premios).WithOne(p => p.Sorteo).HasForeignKey(p => p.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Premios).HasField("_premios").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.Participantes).WithOne().HasForeignKey(p => p.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Participantes).HasField("_participantes").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.Extracciones).WithOne(e => e.Sorteo).HasForeignKey(e => e.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Extracciones).HasField("_extracciones").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasMany(s => s.Anuncios).WithOne(a => a.Sorteo).HasForeignKey(a => a.SorteoId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(s => s.Anuncios).HasField("_anuncios").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(s => new { s.Estado, s.FechaSorteo });
    }
}

internal sealed class PremioSorteoConfiguracion : IEntityTypeConfiguration<PremioSorteo>
{
    public void Configure(EntityTypeBuilder<PremioSorteo> builder)
    {
        builder.ToTable("PremiosSorteos", t =>
        {
            t.HasCheckConstraint("CK_PremiosSorteos_Tipo", "[Tipo] IN (0, 1, 2)");
            // Cada premio es una sola cosa: el número que no le corresponde va a nulo
            t.HasCheckConstraint("CK_PremiosSorteos_Articulo",
                "([Tipo] = 0 AND [ProductoId] IS NULL AND [ProductoSorteoId] IS NULL) OR ([Tipo] = 1 AND [ServicioId] IS NULL AND [ProductoSorteoId] IS NULL) OR ([Tipo] = 2 AND [ServicioId] IS NULL AND [ProductoId] IS NULL)");
            t.HasCheckConstraint("CK_PremiosSorteos_Cantidad", $"[Cantidad] BETWEEN 1 AND {PremioSorteo.CantidadMaxima}");
        });
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(PremioSorteo.LongitudNombre);
        builder.Property(p => p.NombreEn).HasMaxLength(PremioSorteo.LongitudNombre);
        builder.Property(p => p.Descripcion).HasMaxLength(PremioSorteo.LongitudDescripcion);
        builder.Property(p => p.DescripcionEn).HasMaxLength(PremioSorteo.LongitudDescripcion);
        builder.Ignore(p => p.ArticuloId);

        // Si el servicio o el producto desaparecen, el premio conserva su nombre: el historial dice qué se sorteó
        builder.HasOne<Servicio>().WithMany().HasForeignKey(p => p.ServicioId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Producto>().WithMany().HasForeignKey(p => p.ProductoId).OnDelete(DeleteBehavior.SetNull);
        // Los productos de sorteo no se borran (se eliminan de forma lógica); si alguien los borra a mano, igual que arriba
        builder.HasOne<ProductoSorteo>().WithMany().HasForeignKey(p => p.ProductoSorteoId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProductoSorteoConfiguracion : IEntityTypeConfiguration<ProductoSorteo>
{
    public void Configure(EntityTypeBuilder<ProductoSorteo> builder)
    {
        builder.ToTable("ProductosSorteo");
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(ProductoSorteo.LongitudNombre);
        builder.Property(p => p.NombreEn).HasMaxLength(ProductoSorteo.LongitudNombre);
        builder.Property(p => p.Descripcion).HasMaxLength(ProductoSorteo.LongitudDescripcion);
        builder.Property(p => p.DescripcionEn).HasMaxLength(ProductoSorteo.LongitudDescripcion);
        builder.HasIndex(p => new { p.Activo, p.Nombre });
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
        .Include(s => s.Premios)
        .Include(s => s.Participantes).ThenInclude(p => p.Cliente)
        .Include(s => s.Extracciones)
        .Include(s => s.Anuncios)
        .AsSplitQuery();

    public Task<Sorteo?> ObtenerAsync(int id, CancellationToken ct) => Completos.SingleOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Sorteo>> ListarAsync(bool historial, CancellationToken ct) =>
        historial
            ? await Completos.AsNoTracking().Where(s => s.Estado != EstadoSorteo.Abierto).OrderByDescending(s => s.ActualizadoEn).ThenByDescending(s => s.Id).ToListAsync(ct)
            : await Completos.AsNoTracking().Where(s => s.Estado == EstadoSorteo.Abierto).OrderBy(s => s.FechaSorteo).ThenBy(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Sorteo>> AbiertosConProductoSorteoAsync(int productoSorteoId, CancellationToken ct) =>
        await Completos.Where(s => s.Estado == EstadoSorteo.Abierto && s.Premios.Any(p => p.ProductoSorteoId == productoSorteoId)).ToListAsync(ct);

    public async Task<IReadOnlyList<AnuncioPublico>> AnunciosVisiblesAsync(DateOnly dia, CancellationToken ct) =>
        await db.AnunciosSorteos.AsNoTracking()
            .Where(a => a.Desde <= dia && a.Hasta >= dia && a.Sorteo!.Estado != EstadoSorteo.Cancelado)
            .OrderBy(a => a.Sorteo!.FechaSorteo).ThenBy(a => a.Id)
            .Select(a => new AnuncioPublico(a.Id, a.SorteoId, a.Sorteo!.Titulo, a.Sorteo.TituloEn, a.Sorteo.Descripcion, a.Sorteo.DescripcionEn,
                a.Sorteo.Premios.OrderBy(p => p.Id)
                    .Select(p => new PremioPublico(p.Tipo, p.Nombre, p.NombreEn, p.Descripcion, p.DescripcionEn, p.Cantidad)).ToList(),
                a.Sorteo.FechaSorteo, a.Sorteo.Estado == EstadoSorteo.Realizado, a.Texto, a.TextoEn, a.Hasta))
            .AsSplitQuery()
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

internal sealed class ProductoSorteoRepositorio(PeluqueriaDbContext db) : IProductoSorteoRepositorio
{
    public Task<ProductoSorteo?> ObtenerAsync(int id, CancellationToken ct) => db.ProductosSorteo.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<ResumenProductoSorteo>> ResumenAsync(CancellationToken ct) =>
        await db.ProductosSorteo.AsNoTracking()
            .OrderBy(p => p.Nombre).ThenBy(p => p.Id)
            .Select(p => new ResumenProductoSorteo(p,
                db.PremiosSorteos.Count(x => x.ProductoSorteoId == p.Id && x.Sorteo!.Estado == EstadoSorteo.Abierto),
                db.PremiosSorteos.Count(x => x.ProductoSorteoId == p.Id && x.Sorteo!.Estado == EstadoSorteo.Realizado),
                db.PremiosSorteos.Count(x => x.ProductoSorteoId == p.Id && x.Sorteo!.Estado == EstadoSorteo.Realizado
                    && x.Sorteo.Extracciones.Any(e => e.EntregadoEn != null))))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductoSorteo>> ActivosAsync(CancellationToken ct) =>
        await db.ProductosSorteo.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ThenBy(p => p.Id).ToListAsync(ct);

    public Task<ProductoSorteo?> ActivoPorNombreAsync(string nombre, int? excepto, CancellationToken ct) =>
        // La intercalación de la base de datos no distingue mayúsculas
        db.ProductosSorteo.AsNoTracking().FirstOrDefaultAsync(p => p.Activo && p.Nombre == nombre && p.Id != excepto, ct);

    public async Task<IReadOnlyList<Sorteo>> SorteosAsync(int id, CancellationToken ct) =>
        await db.Sorteos.AsNoTracking()
            .Include(s => s.Premios)
            .Include(s => s.Extracciones)
            .Where(s => s.Premios.Any(p => p.ProductoSorteoId == id))
            .OrderByDescending(s => s.FechaSorteo).ThenByDescending(s => s.Id)
            .AsSplitQuery()
            .ToListAsync(ct);

    public void Agregar(ProductoSorteo producto) => db.ProductosSorteo.Add(producto);
}

/// <summary>Azar criptográfico: cada participante tiene la misma probabilidad y nadie puede predecir el resultado.</summary>
internal sealed class AzarSeguro : IAzar
{
    public int Siguiente(int maximo) => RandomNumberGenerator.GetInt32(maximo);
}
