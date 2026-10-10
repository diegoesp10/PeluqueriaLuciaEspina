using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Seguridad;

/// <summary>Persona que puede entrar al panel de administración.</summary>
public sealed class Administrador
{
    public const int LongitudUsuario = 50;

    /// <summary>Intentos fallidos seguidos que bloquean la cuenta.</summary>
    public const int MaximoIntentosFallidos = 5;

    public static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    private Administrador()
    {
    }

    public int Id { get; private set; }

    /// <summary>Nombre de usuario, siempre en minúsculas y sin espacios alrededor.</summary>
    public string Usuario { get; private set; } = string.Empty;

    /// <summary>Hash de la contraseña (PBKDF2 con sal). La contraseña nunca se guarda.</summary>
    public string HashContrasena { get; private set; } = string.Empty;

    public DateTime CreadoEn { get; private set; }

    public DateTime? UltimoAccesoEn { get; private set; }

    public DateTime ContrasenaCambiadaEn { get; private set; }

    public int IntentosFallidos { get; private set; }

    public DateTime? BloqueadoHasta { get; private set; }

    public static string NormalizarUsuario(string? usuario) => (usuario ?? string.Empty).Trim().ToLowerInvariant();

    public static Administrador Crear(string usuario, string hashContrasena, DateTime ahora)
    {
        var normalizado = NormalizarUsuario(usuario);
        if (normalizado.Length is < 3 or > LongitudUsuario || normalizado.Any(char.IsWhiteSpace))
            throw new ReglaDeNegocioException($"El usuario debe tener entre 3 y {LongitudUsuario} caracteres y no llevar espacios.", nameof(Usuario));

        return new Administrador
        {
            Usuario = normalizado,
            HashContrasena = hashContrasena,
            CreadoEn = ahora,
            ContrasenaCambiadaEn = ahora,
        };
    }

    public bool EstaBloqueado(DateTime ahora) => BloqueadoHasta is { } hasta && hasta > ahora;

    /// <summary>Cuenta un fallo; al llegar al máximo, la cuenta queda bloqueada un tiempo.</summary>
    public void RegistrarFallo(DateTime ahora)
    {
        IntentosFallidos++;
        if (IntentosFallidos >= MaximoIntentosFallidos)
        {
            BloqueadoHasta = ahora + DuracionBloqueo;
            IntentosFallidos = 0;
        }
    }

    public void RegistrarAcceso(DateTime ahora)
    {
        IntentosFallidos = 0;
        BloqueadoHasta = null;
        UltimoAccesoEn = ahora;
    }

    public void CambiarContrasena(string hashContrasena, DateTime ahora)
    {
        HashContrasena = hashContrasena;
        ContrasenaCambiadaEn = ahora;
        IntentosFallidos = 0;
        BloqueadoHasta = null;
    }

    /// <summary>Actualiza el hash al algoritmo vigente sin cambiar la contraseña.</summary>
    public void ActualizarHash(string hashContrasena) => HashContrasena = hashContrasena;
}
