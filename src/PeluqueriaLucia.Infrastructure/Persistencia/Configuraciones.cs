using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(Cliente.LongitudNombre);
        builder.Property(c => c.Apellido).IsRequired().HasMaxLength(Cliente.LongitudNombre);
        builder.Ignore(c => c.NombreCompleto);
        builder.Ignore(c => c.Iniciales);
        builder.Ignore(c => c.NumeroFaltas);
        builder.Property(c => c.Apodo).HasMaxLength(80);
        builder.HasIndex(c => c.PendienteCompletar);
        builder.HasIndex(c => new { c.Nombre, c.Apellido });
    }
}

internal sealed class ServicioConfiguracion : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> builder)
    {
        builder.ToTable("ServiciosGenericos");
        builder.Property(s => s.Nombre).IsRequired().HasMaxLength(Servicio.LongitudNombre);
        builder.Property(s => s.Precio).HasColumnType("decimal(18,2)");
          builder.Property(s => s.DescripcionPublica).HasMaxLength(1000);
          builder.HasOne<PeluqueriaLucia.Domain.Publico.CategoriaTratamiento>().WithMany().HasForeignKey(s => s.CategoriaPublicaId).OnDelete(DeleteBehavior.Restrict);

        // La migración que añade la columna rellena las filas existentes con 1 (activo)
        builder.Property(s => s.Activo).HasColumnName("Activo");
    }
}

internal sealed class VisitaConfiguracion : IEntityTypeConfiguration<Visita>
{
    public void Configure(EntityTypeBuilder<Visita> builder)
    {
        builder.ToTable("ServiciosClientes");
        builder.Property(v => v.FormaPago).HasColumnName("OpcionPago");
        builder.Property(v => v.TotalEfectivo).HasColumnType("decimal(18,2)");
        builder.Property(v => v.TotalTarjeta).HasColumnType("decimal(18,2)");
        builder.Ignore(v => v.Total);
        builder.Ignore(v => v.EstaCobrada);

        // Un cliente con visitas no se puede borrar: la aplicación lo impide y la base de datos también
        builder.HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Lineas)
            .WithOne()
            .HasForeignKey(l => l.VisitaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(v => v.Lineas).HasField("_lineas").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(v => v.Fecha);
    }
}

internal sealed class LineaVisitaConfiguracion : IEntityTypeConfiguration<LineaVisita>
{
    public void Configure(EntityTypeBuilder<LineaVisita> builder)
    {
        builder.ToTable("ServiciosGenericosRealizados");
        builder.Property(l => l.VisitaId).HasColumnName("ServicioClienteId");
        builder.Property(l => l.ServicioId).HasColumnName("ServicioGenericoId");
        builder.Property(l => l.Nombre).HasColumnName("NombreAplicado").IsRequired().HasMaxLength(Servicio.LongitudNombre);
        builder.Property(l => l.Precio).HasColumnName("PrecioAplicado").HasColumnType("decimal(18,2)");

        // Un servicio del catálogo que aparece en visitas se archiva, nunca se borra en cascada
        builder.HasOne<Servicio>()
            .WithMany()
            .HasForeignKey(l => l.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
