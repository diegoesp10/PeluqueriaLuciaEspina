using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class ProductoConfiguracion : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> builder)
    {
        // El stock puede quedar en negativo (se vendió o usó más de lo que había); el mínimo y el precio, no
        builder.ToTable("Productos", t =>
        {
            t.HasCheckConstraint("CK_Productos_StockMinimo", "[StockMinimo] >= 0");
            t.HasCheckConstraint("CK_Productos_PrecioVenta", "[PrecioVenta] > 0");
        });
        builder.Property(p => p.Referencia).IsRequired().HasMaxLength(Producto.LongitudReferencia);
        builder.Property(p => p.CodigoBarras).HasMaxLength(Producto.LongitudCodigoBarras).IsUnicode(false);
        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(Producto.LongitudNombre);
        builder.Property(p => p.Marca).HasMaxLength(Producto.LongitudMarca);
        builder.Property(p => p.Descripcion).HasMaxLength(Producto.LongitudDescripcion);
        builder.Property(p => p.PrecioVenta).HasColumnType("decimal(18,2)");
        builder.Property(p => p.PrecioCoste).HasColumnType("decimal(18,2)");
        builder.Property(p => p.Version).IsRowVersion();
        builder.Ignore(p => p.FotosEnOrden);
        builder.Ignore(p => p.FotoPrincipal);
        builder.Ignore(p => p.Agotado);
        builder.Ignore(p => p.EnNegativo);
        builder.Ignore(p => p.BajoStock);
        builder.Ignore(p => p.Margen);

        builder.HasIndex(p => p.Referencia).IsUnique();
        builder.HasIndex(p => p.CodigoBarras).IsUnique().HasFilter("[CodigoBarras] IS NOT NULL");
        builder.HasIndex(p => new { p.Activo, p.Nombre });

        // Una categoría con productos no se borra de verdad: se elimina con borrado lógico (archiva sus productos)
        builder.HasOne(p => p.Categoria)
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Fotos)
            .WithOne()
            .HasForeignKey(f => f.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Fotos).HasField("_fotos").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CategoriaProductoConfiguracion : IEntityTypeConfiguration<CategoriaProducto>
{
    public void Configure(EntityTypeBuilder<CategoriaProducto> builder)
    {
        builder.ToTable("CategoriasProductos");
        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(CategoriaProducto.LongitudNombre);

        // Dos categorías activas no pueden llamarse igual; una eliminada no estorba para crear otra con su nombre
        builder.HasIndex(c => c.Nombre).IsUnique().HasFilter("[Activa] = 1");
    }
}

internal sealed class FotoProductoConfiguracion : IEntityTypeConfiguration<FotoProducto>
{
    public void Configure(EntityTypeBuilder<FotoProducto> builder)
    {
        builder.ToTable("FotosProductos");
        // El identificador es también el nombre del fichero: lo pone la aplicación, no la base de datos
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.HasIndex(f => new { f.ProductoId, f.Orden });
    }
}

internal sealed class MovimientoStockConfiguracion : IEntityTypeConfiguration<MovimientoStock>
{
    public void Configure(EntityTypeBuilder<MovimientoStock> builder)
    {
        builder.ToTable("MovimientosStock", t => t.HasCheckConstraint("CK_MovimientosStock_Cantidad", "[Cantidad] <> 0"));
        builder.Property(m => m.Nota).HasMaxLength(MovimientoStock.LongitudNota);

        // Solo se borra el historial de un producto que nunca se ha vendido (si no, se archiva)
        builder.HasOne(m => m.Producto)
            .WithMany()
            .HasForeignKey(m => m.ProductoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Las ventas apuntan a su visita; una visita cobrada no se puede borrar
        builder.HasOne<Visita>()
            .WithMany()
            .HasForeignKey(m => m.VisitaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ProductoId, m.Fecha });
    }
}

internal sealed class LineaProductoVisitaConfiguracion : IEntityTypeConfiguration<LineaProductoVisita>
{
    public void Configure(EntityTypeBuilder<LineaProductoVisita> builder)
    {
        builder.ToTable("ProductosVisitas", t =>
            t.HasCheckConstraint("CK_ProductosVisitas_Cantidad", $"[Cantidad] BETWEEN 1 AND {LineaProductoVisita.CantidadMaxima}"));
        builder.Property(l => l.Referencia).IsRequired().HasMaxLength(Producto.LongitudReferencia);
        builder.Property(l => l.Nombre).IsRequired().HasMaxLength(Producto.LongitudNombre);
        builder.Property(l => l.PrecioUnitario).HasColumnType("decimal(18,2)");
        builder.Property(l => l.Importe).HasColumnType("decimal(18,2)");

        // Un producto vendido se archiva, nunca se borra
        builder.HasOne<Producto>()
            .WithMany()
            .HasForeignKey(l => l.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
