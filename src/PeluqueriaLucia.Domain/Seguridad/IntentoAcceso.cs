namespace PeluqueriaLucia.Domain.Seguridad;

/// <summary>Registro de cada intento de entrar al panel, con éxito o sin él, para poder revisar accesos sospechosos.</summary>
public sealed class IntentoAcceso
{
    private IntentoAcceso()
    {
    }

    public long Id { get; private set; }

    public string Usuario { get; private set; } = string.Empty;

    public string Ip { get; private set; } = string.Empty;

    public DateTime Fecha { get; private set; }

    public bool Exito { get; private set; }

    public string Motivo { get; private set; } = string.Empty;

    public static IntentoAcceso Registrar(string usuario, string ip, bool exito, string motivo, DateTime ahora) => new()
    {
        Usuario = usuario.Length > Administrador.LongitudUsuario ? usuario[..Administrador.LongitudUsuario] : usuario,
        Ip = ip,
        Exito = exito,
        Motivo = motivo,
        Fecha = ahora,
    };
}
