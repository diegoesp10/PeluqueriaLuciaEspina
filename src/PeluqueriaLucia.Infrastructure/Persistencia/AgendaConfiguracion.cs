using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Seguridad;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class ReservaCitaConfiguracion : IEntityTypeConfiguration<ReservaCita>
{
    public void Configure(EntityTypeBuilder<ReservaCita> b)
    {
        b.ToTable("ReservasCitas"); b.Property(x => x.Id).ValueGeneratedNever();
        b.HasIndex(x => x.SolicitudId).IsUnique(); b.HasIndex(x => new { x.Inicio, x.Estado });
        b.HasIndex(x => new { x.Telefono, x.CreadaUtc }); b.HasIndex(x => new { x.IpHash, x.CreadaUtc });
        b.Property(x => x.Nombre).HasMaxLength(80); b.Property(x => x.Telefono).HasMaxLength(16).IsUnicode(false);
        b.Property(x => x.ServicioNombre).HasMaxLength(100); b.Property(x => x.IpHash).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.NotaInterna).HasMaxLength(500); b.Property(x => x.Version).IsRowVersion();
        b.HasOne<Servicio>().WithMany().HasForeignKey(x => x.ServicioId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Administrador>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_ReservasCitas_Intervalo", "[Fin] > [Inicio]"));
    }
}
internal sealed class TramoAgendaConfiguracion : IEntityTypeConfiguration<TramoAgenda>
{
    public void Configure(EntityTypeBuilder<TramoAgenda> b)
    {
        b.ToTable("TramosAgenda"); b.HasIndex(x => new { x.Dia, x.Desde }).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_TramosAgenda_Intervalo", "[Hasta] > [Desde]"));
    }
}
internal sealed class CierreAgendaConfiguracion : IEntityTypeConfiguration<CierreAgenda>
{
    public void Configure(EntityTypeBuilder<CierreAgenda> b) { b.ToTable("CierresAgenda"); b.HasIndex(x => x.Dia).IsUnique(); b.Property(x => x.Motivo).HasMaxLength(100); }
}
internal sealed class MensajeCitaConfiguracion : IEntityTypeConfiguration<MensajeCita>
{
    public void Configure(EntityTypeBuilder<MensajeCita> b)
    {
        b.ToTable("MensajesCitas"); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Telefono).HasMaxLength(16).IsUnicode(false); b.Property(x => x.Texto).HasMaxLength(500);
        b.Property(x => x.ProveedorId).HasMaxLength(64).IsUnicode(false); b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.Estado, x.CreadoUtc });
        b.HasOne<ReservaCita>().WithMany().HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class SmsEntranteConfiguracion : IEntityTypeConfiguration<SmsEntrante>
{
    public void Configure(EntityTypeBuilder<SmsEntrante> b)
    {
        b.ToTable("SmsEntrantes"); b.Property(x => x.Id).ValueGeneratedNever(); b.HasIndex(x => x.ProveedorId).IsUnique();
        b.Property(x => x.Telefono).HasMaxLength(16).IsUnicode(false); b.Property(x => x.Texto).HasMaxLength(500);
        b.Property(x => x.ProveedorId).HasMaxLength(64).IsUnicode(false);
    }
}
