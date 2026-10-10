using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Seguridad;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Infrastructure.Seguridad;

internal sealed class AdministradorConfiguracion : IEntityTypeConfiguration<Administrador>
{
    public void Configure(EntityTypeBuilder<Administrador> builder)
    {
        builder.ToTable("Administradores");
        builder.Property(a => a.Usuario).IsRequired().HasMaxLength(Administrador.LongitudUsuario);
        builder.HasIndex(a => a.Usuario).IsUnique();
        builder.Property(a => a.SecretoMfaProtegido).HasMaxLength(1000);
        builder.Ignore(a => a.MfaActivo);
        builder.Property(a => a.HashContrasena).IsRequired().HasMaxLength(200);
        builder.Property<byte[]>("Version").IsRowVersion();
    }
}

internal sealed class DesafioMfaConfiguracion : IEntityTypeConfiguration<DesafioMfa>
{
    public void Configure(EntityTypeBuilder<DesafioMfa> b)
    {
        b.ToTable("DesafiosMfa"); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsUnicode(false);
        b.Property(x => x.Ip).HasMaxLength(45).IsUnicode(false);
        b.Property(x => x.NuevoSecretoProtegido).HasMaxLength(1000);
        b.Property<byte[]>("Version").IsRowVersion();
        b.HasOne<Administrador>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.ExpiraEn);
    }
}

internal sealed class RecuperacionMfaConfiguracion : IEntityTypeConfiguration<RecuperacionMfa>
{
    public void Configure(EntityTypeBuilder<RecuperacionMfa> b)
    {
        b.ToTable("RecuperacionesMfa"); b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Hash).HasMaxLength(64).IsUnicode(false); b.HasIndex(x => x.Hash).IsUnique();
        b.Property<byte[]>("Version").IsRowVersion();
        b.HasOne<Administrador>().WithMany().HasForeignKey(x => x.AdministradorId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SesionAdministradorConfiguracion : IEntityTypeConfiguration<SesionAdministrador>
{
    public void Configure(EntityTypeBuilder<SesionAdministrador> builder)
    {
        builder.ToTable("SesionesAdministrador");
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).IsRequired().HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.Property(s => s.Ip).IsRequired().HasMaxLength(45).IsUnicode(false);
        builder.Property(s => s.Agente).HasMaxLength(SesionAdministrador.LongitudAgente);
        builder.HasOne<Administrador>().WithMany().HasForeignKey(s => s.AdministradorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.AdministradorId, s.CerradaEn });
    }
}

internal sealed class IntentoAccesoConfiguracion : IEntityTypeConfiguration<IntentoAcceso>
{
    public void Configure(EntityTypeBuilder<IntentoAcceso> builder)
    {
        builder.ToTable("IntentosAcceso");
        builder.Property(i => i.Usuario).IsRequired().HasMaxLength(Administrador.LongitudUsuario);
        builder.Property(i => i.Ip).IsRequired().HasMaxLength(45).IsUnicode(false);
        builder.Property(i => i.Motivo).IsRequired().HasMaxLength(100);
        builder.HasIndex(i => i.Fecha);
    }
}

internal sealed class AdministradorRepositorio(PeluqueriaDbContext db) : IAdministradorRepositorio
{
    public Task<Administrador?> ObtenerAsync(int id, CancellationToken ct) =>
        db.Administradores.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Administrador?> ObtenerPorUsuarioAsync(string usuario, CancellationToken ct) =>
        db.Administradores.FirstOrDefaultAsync(a => a.Usuario == usuario, ct);

    public Task<bool> HayAlgunoAsync(CancellationToken ct) => db.Administradores.AnyAsync(ct);

    public void Agregar(Administrador administrador) => db.Administradores.Add(administrador);
}

internal sealed class SesionRepositorio(PeluqueriaDbContext db) : ISesionRepositorio
{
    public Task<SesionAdministrador?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.SesionesAdministrador.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<SesionAdministrador>> ListarAbiertasAsync(int administradorId, DateTime ahora, CancellationToken ct)
    {
        var limiteInactividad = ahora - SesionAdministrador.Inactividad;
        return await db.SesionesAdministrador
            .Where(s => s.AdministradorId == administradorId && s.CerradaEn == null && s.ExpiraEn > ahora && s.UltimoUsoEn > limiteInactividad)
            .OrderByDescending(s => s.UltimoUsoEn)
            .ToListAsync(ct);
    }

    public void Agregar(SesionAdministrador sesion) => db.SesionesAdministrador.Add(sesion);
}

internal sealed class IntentoAccesoRepositorio(PeluqueriaDbContext db) : IIntentoAccesoRepositorio
{
    public async Task<IReadOnlyList<IntentoAcceso>> UltimosAsync(int cantidad, CancellationToken ct) =>
        await db.IntentosAcceso.AsNoTracking().OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id).Take(cantidad).ToListAsync(ct);

    public void Agregar(IntentoAcceso intento) => db.IntentosAcceso.Add(intento);
}

/// <summary>
/// Hash de contraseñas de ASP.NET Core Identity: PBKDF2 con HMAC-SHA512, sal aleatoria y 210.000 iteraciones.
/// Si en el futuro sube el número de iteraciones, los hashes antiguos se recalculan solos al entrar.
/// </summary>
internal sealed class HasherContrasenas : IHasherContrasenas
{
    private static readonly PasswordHasher<object> Hasher = new(Microsoft.Extensions.Options.Options.Create(
        new PasswordHasherOptions { IterationCount = 210_000 }));
    private static readonly object Usuario = new();
    private static readonly Lazy<string> Senuelo = new(() => Hasher.HashPassword(Usuario,
        Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));

    public string HashSenuelo => Senuelo.Value;

    public string Calcular(string contrasena) => Hasher.HashPassword(Usuario, contrasena);

    public ResultadoVerificacion Verificar(string hash, string contrasena) => Hasher.VerifyHashedPassword(Usuario, hash, contrasena) switch
    {
        PasswordVerificationResult.Success => ResultadoVerificacion.Correcta,
        PasswordVerificationResult.SuccessRehashNeeded => ResultadoVerificacion.CorrectaRecalcular,
        _ => ResultadoVerificacion.Incorrecta,
    };
}
