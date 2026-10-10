using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using PeluqueriaLucia.Application.Abstracciones;

namespace PeluqueriaLucia.Infrastructure.Seguridad;

internal sealed class ProtectorSecretos(IDataProtectionProvider proveedor) : IProtectorSecretos
{
    public string Proteger(string valor, string finalidad) => proveedor.CreateProtector("Lucia.Secretos.v1", finalidad).Protect(valor);
    public string? Leer(string valor, string finalidad)
    {
        try { return proveedor.CreateProtector("Lucia.Secretos.v1", finalidad).Unprotect(valor); }
        catch (CryptographicException) { return null; }
    }
    public string GenerarSecretoMfa() => Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
    public long? VerificarTotp(string secreto, string codigo, DateTime ahora)
    {
        codigo = codigo.Replace(" ", "");
        if (codigo.Length != 6 || codigo.Any(c => !char.IsAsciiDigit(c))) return null;
        var totp = new Totp(Base32Encoding.ToBytes(secreto), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(ahora, codigo, out var paso, new VerificationWindow(previous: 1, future: 1)) ? paso : null;
    }
}

internal sealed class MfaRepositorio(Persistencia.PeluqueriaDbContext db) : IMfaRepositorio
{
    public Task<Domain.Seguridad.DesafioMfa?> ObtenerAsync(Guid id, CancellationToken ct) =>
        Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(db.DesafiosMfa, x => x.Id == id, ct);
    public void Agregar(Domain.Seguridad.DesafioMfa desafio) => db.DesafiosMfa.Add(desafio);
    public async Task<IReadOnlyList<Domain.Seguridad.RecuperacionMfa>> CodigosAsync(int id, CancellationToken ct) =>
        await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.RecuperacionesMfa.Where(x => x.AdministradorId == id && x.UsadoEn == null), ct);
    public void Agregar(Domain.Seguridad.RecuperacionMfa codigo) => db.RecuperacionesMfa.Add(codigo);
}
