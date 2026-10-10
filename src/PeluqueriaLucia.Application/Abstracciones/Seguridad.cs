using PeluqueriaLucia.Domain.Seguridad;

namespace PeluqueriaLucia.Application.Abstracciones;

public interface IAdministradorRepositorio
{
    Task<Administrador?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Busca por nombre de usuario ya normalizado.</summary>
    Task<Administrador?> ObtenerPorUsuarioAsync(string usuario, CancellationToken ct = default);

    Task<bool> HayAlgunoAsync(CancellationToken ct = default);

    void Agregar(Administrador administrador);
}

public interface ISesionRepositorio
{
    Task<SesionAdministrador?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<SesionAdministrador>> ListarAbiertasAsync(int administradorId, DateTime ahora, CancellationToken ct = default);

    void Agregar(SesionAdministrador sesion);
}

public interface IIntentoAccesoRepositorio
{
    Task<IReadOnlyList<IntentoAcceso>> UltimosAsync(int cantidad, CancellationToken ct = default);

    void Agregar(IntentoAcceso intento);
}

/// <summary>Hash de contraseñas lento y con sal (PBKDF2). La implementación vive en Infraestructura.</summary>
public interface IHasherContrasenas
{
    string HashSenuelo { get; }
    string Calcular(string contrasena);

    ResultadoVerificacion Verificar(string hash, string contrasena);
}

public enum ResultadoVerificacion
{
    Incorrecta,
    Correcta,

    /// <summary>Correcta, pero el hash usa parámetros antiguos y conviene recalcularlo.</summary>
    CorrectaRecalcular,
}
