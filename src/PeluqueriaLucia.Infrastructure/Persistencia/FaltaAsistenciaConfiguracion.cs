using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Seguridad;
namespace PeluqueriaLucia.Infrastructure.Persistencia;
internal sealed class FaltaAsistenciaConfiguracion : IEntityTypeConfiguration<FaltaAsistencia>
{
    public void Configure(EntityTypeBuilder<FaltaAsistencia> b)
    {
        b.ToTable("FaltasAsistencia");
        b.Property(x => x.Nota).HasMaxLength(500);
        b.Property(x => x.MotivoAnulacion).HasMaxLength(500);
        b.HasIndex(x => x.ReservaId).IsUnique().HasFilter("[AnuladaUtc] IS NULL");
        b.HasOne<Cliente>().WithMany(x => x.Faltas).HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ReservaCita>().WithMany().HasForeignKey(x => x.ReservaId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Administrador>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Administrador>().WithMany().HasForeignKey(x => x.AnuladaPorId).OnDelete(DeleteBehavior.Restrict);
    }
}
