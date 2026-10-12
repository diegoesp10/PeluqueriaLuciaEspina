using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
namespace PeluqueriaLucia.Infrastructure.Persistencia;
internal sealed class VerificacionContactoConfiguracion : IEntityTypeConfiguration<VerificacionContacto>
{
    public void Configure(EntityTypeBuilder<VerificacionContacto> b)
    {
        b.ToTable("VerificacionesContacto"); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Telefono).HasMaxLength(16).IsUnicode(false);
        b.Property(x => x.DestinoCorreo).HasMaxLength(254);
        b.Property(x => x.CodigoProtegido).HasMaxLength(2048); b.Property(x => x.SecretoProtegido).HasMaxLength(2048);
        b.Property(x => x.CodigoHash).HasMaxLength(64).IsUnicode(false); b.Property(x => x.IpHash).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.Version).IsRowVersion();
        b.HasIndex(x => new { x.Telefono, x.CreadaUtc }); b.HasIndex(x => new { x.IpHash, x.CreadaUtc });
        b.HasIndex(x => new { x.EstadoEnvio, x.CaducaUtc });
        b.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.SetNull);
        b.ToTable(t => t.HasCheckConstraint("CK_VerificacionesContacto_Intentos", "[Intentos] BETWEEN 0 AND 5"));
    }
}
