using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class CategoriaTratamientoConfiguracion : IEntityTypeConfiguration<CategoriaTratamiento>
{
    public void Configure(EntityTypeBuilder<CategoriaTratamiento> b) { b.ToTable("CategoriasPublicas"); b.Property(x => x.Nombre).HasMaxLength(CategoriaTratamiento.LongitudNombre); b.Property(x => x.NombreEn).HasMaxLength(CategoriaTratamiento.LongitudNombre); b.Property(x => x.Slug).HasMaxLength(80).IsUnicode(false); b.HasIndex(x => x.Slug).IsUnique(); }
}
internal sealed class PermisoResenaConfiguracion : IEntityTypeConfiguration<PermisoResena>
{
    public void Configure(EntityTypeBuilder<PermisoResena> b) { b.ToTable("PermisosResenas"); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.CodigoHash).HasMaxLength(64).IsUnicode(false); b.Property(x => x.CodigoProtegido).HasMaxLength(1000); b.HasIndex(x => x.CodigoHash).IsUnique(); b.HasIndex(x => x.VisitaId).IsUnique(); b.HasOne<Visita>().WithMany().HasForeignKey(x => x.VisitaId).OnDelete(DeleteBehavior.Restrict); b.Property<byte[]>("Version").IsRowVersion(); }
}
internal sealed class ResenaConfiguracion : IEntityTypeConfiguration<Resena>
{
    public void Configure(EntityTypeBuilder<Resena> b) { b.ToTable("Resenas"); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.Alias).HasMaxLength(50); b.Property(x => x.Comentario).HasMaxLength(1500); b.Property(x => x.Respuesta).HasMaxLength(1500); b.Property(x => x.RespuestaEn).HasMaxLength(1500); b.Property(x => x.Foto).HasMaxLength(40).IsUnicode(false); b.HasIndex(x => x.PermisoId).IsUnique(); b.HasIndex(x => new { x.Estado, x.CreadaEn }); b.HasOne<PermisoResena>().WithMany().HasForeignKey(x => x.PermisoId).OnDelete(DeleteBehavior.Restrict); b.Property<byte[]>("Version").IsRowVersion(); }
}
internal sealed class ContenidoRepositorio(PeluqueriaDbContext db) : IContenidoRepositorio
{
    public async Task<IReadOnlyList<CategoriaTratamiento>> CategoriasAsync(CancellationToken ct) => await db.CategoriasPublicas.OrderBy(x => x.Orden).ThenBy(x => x.Id).ToListAsync(ct);
    public Task<CategoriaTratamiento?> CategoriaAsync(int id, CancellationToken ct) => db.CategoriasPublicas.FirstOrDefaultAsync(x => x.Id == id, ct);
    public void Agregar(CategoriaTratamiento x) => db.CategoriasPublicas.Add(x);
    public void Eliminar(CategoriaTratamiento x) => db.CategoriasPublicas.Remove(x);
    public Task<PermisoResena?> PermisoVisitaAsync(int id, CancellationToken ct) => db.PermisosResenas.FirstOrDefaultAsync(x => x.VisitaId == id, ct);
    public Task<PermisoResena?> PermisoCodigoAsync(string hash, CancellationToken ct) => db.PermisosResenas.FirstOrDefaultAsync(x => x.CodigoHash == hash, ct);
    public async Task<PermisoResena> GuardarPermisoTicketAsync(PermisoResena permiso, CancellationToken ct)
    {
        db.PermisosResenas.Add(permiso);
        try { await db.SaveChangesAsync(ct); return permiso; }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            // Dos impresiones simultáneas deben recibir el mismo código, nunca crear dos permisos.
            db.Entry(permiso).State = EntityState.Detached;
            return await db.PermisosResenas.SingleAsync(x => x.VisitaId == permiso.VisitaId, ct);
        }
    }
    public void Agregar(Resena x) => db.Resenas.Add(x);
    public Task<Resena?> ResenaAsync(Guid id, CancellationToken ct) => db.Resenas.FirstOrDefaultAsync(x => x.Id == id, ct);
    public async Task<Pagina<Resena>> ResenasAsync(EstadoResena? estado, int pagina, string? texto, CancellationToken ct)
    {
        var q = db.Resenas.AsNoTracking().Where(x => estado == null || x.Estado == estado);
        foreach (var palabra in BusquedaTexto.Palabras(texto))
        {
            var patron = BusquedaTexto.Patron(palabra);
            q = q.Where(x => EF.Functions.Like(x.Alias, patron, "\\") || EF.Functions.Like(x.Comentario, patron, "\\")
                || (x.Respuesta != null && EF.Functions.Like(x.Respuesta, patron, "\\")));
        }
        var total = await q.CountAsync(ct); pagina = Math.Clamp(pagina, 1, Math.Max(1, (total + 11) / 12));
        return new(await q.OrderByDescending(x => x.CreadaEn).ThenBy(x => x.Id).Skip((pagina - 1) * 12).Take(12).ToListAsync(ct), pagina, 12, total);
    }
    public async Task<(int Total, double Media)> ValoracionAsync(CancellationToken ct)
    {
        var q = db.Resenas.Where(x => x.Estado == EstadoResena.Publicada);
        var total = await q.CountAsync(ct);
        return (total, total == 0 ? 0 : await q.AverageAsync(x => (double)x.Estrellas, ct));
    }
}
