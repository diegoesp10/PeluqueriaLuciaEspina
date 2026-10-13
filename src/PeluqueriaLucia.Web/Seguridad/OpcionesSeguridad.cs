namespace PeluqueriaLucia.Web.Seguridad;

/// <summary>Sección «Seguridad» de appsettings.</summary>
public sealed class OpcionesSeguridad
{
    public const string Seccion = "Seguridad";

    /// <summary>
    /// Redirige a HTTPS, activa HSTS y exige que la cookie del panel viaje cifrada. Imprescindible si la web se publica en
    /// internet; en un equipo local sin certificado se deja a false.
    /// </summary>
    public bool RequerirHttps { get; set; } = true;

    public string[] ProxiesConfiables { get; set; } = [];

    /// <summary>
    /// Cabecera con la IP real del visitante que añade un proxy de confianza delante de la web, p. ej. «CF-Connecting-IP»
    /// de Cloudflare. Vacío: se usa la IP de la conexión. Solo debe configurarse si la web no es accesible sin pasar por
    /// ese proxy (en Azure, restricciones de acceso a las IPs de Cloudflare); si no, cualquiera podría falsearla.
    /// </summary>
    public string? CabeceraIpCliente { get; set; }

    /// <summary>
    /// IPs o redes (CIDR, p. ej. «192.168.1.0/24») desde las que se puede abrir el panel. Vacío: desde cualquiera.
    /// Las demás reciben un 404, como si el panel no existiera.
    /// </summary>
    public string[] RedesPermitidasPanel { get; set; } = [];

    public LimitesPeticiones Limites { get; set; } = new();
}

/// <summary>Número máximo de peticiones por IP y minuto. Al superarlo se responde 429 y se indica cuándo reintentar.</summary>
public sealed class LimitesPeticiones
{
    /// <summary>Páginas públicas (inicio, servicios).</summary>
    public int PublicoPorMinuto { get; set; } = 120;

    /// <summary>Pantallas y llamadas del panel (incluye el buscador de clientes mientras se escribe).</summary>
    public int PanelPorMinuto { get; set; } = 400;

    /// <summary>Envíos del formulario de acceso. Además, la cuenta se bloquea tras 5 contraseñas incorrectas.</summary>
    public int AccesoPorCuartoDeHora { get; set; } = 10;

    /// <summary>Tope global de cualquier petición (también ficheros), como freno ante abusos.</summary>
    public int TotalPorMinuto { get; set; } = 1500;
}
