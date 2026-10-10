namespace PeluqueriaLucia.Domain.Seguridad;

/// <summary>
/// Una sesión abierta en el panel. El navegador guarda un token aleatorio; aquí solo se guarda su hash, así que ni
/// quien lea la base de datos puede suplantar la sesión. Cada llamada al panel la valida, y cerrarla la anula al momento.
/// </summary>
public sealed class SesionAdministrador
{
    public const int LongitudAgente = 300;

    /// <summary>Tiempo sin usar el panel tras el que la sesión caduca.</summary>
    public static readonly TimeSpan Inactividad = TimeSpan.FromHours(12);

    /// <summary>Duración máxima de una sesión aunque se use todos los días.</summary>
    public static readonly TimeSpan DuracionMaxima = TimeSpan.FromDays(14);

    private SesionAdministrador()
    {
    }

    public Guid Id { get; private set; }

    public int AdministradorId { get; private set; }

    /// <summary>SHA-256 del token, en hexadecimal.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreadaEn { get; private set; }

    public DateTime UltimoUsoEn { get; private set; }

    public DateTime ExpiraEn { get; private set; }

    public string Ip { get; private set; } = string.Empty;

    public string? Agente { get; private set; }

    public DateTime? CerradaEn { get; private set; }

    public static SesionAdministrador Abrir(int administradorId, string tokenHash, string ip, string? agente, DateTime ahora) => new()
    {
        Id = Guid.NewGuid(),
        AdministradorId = administradorId,
        TokenHash = tokenHash,
        CreadaEn = ahora,
        UltimoUsoEn = ahora,
        ExpiraEn = ahora + DuracionMaxima,
        Ip = ip,
        Agente = agente is { Length: > LongitudAgente } ? agente[..LongitudAgente] : agente,
    };

    public bool EstaActiva(DateTime ahora) => CerradaEn is null && ahora < ExpiraEn && ahora - UltimoUsoEn < Inactividad;

    /// <summary>Anota el uso como mucho una vez por minuto, para no escribir en la base de datos en cada petición.</summary>
    public bool RegistrarUso(DateTime ahora, string ip)
    {
        if (ahora - UltimoUsoEn < TimeSpan.FromMinutes(1) && ip == Ip)
            return false;
        UltimoUsoEn = ahora;
        Ip = ip;
        return true;
    }

    public void Cerrar(DateTime ahora) => CerradaEn ??= ahora;
}
