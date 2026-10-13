namespace PeluqueriaLucia.Domain.Agenda;

/// <summary>Desafío de un solo uso. El código y su clave se conservan cifrados; nunca se devuelve la existencia de una ficha.</summary>
public sealed class VerificacionContacto
{
    public Guid Id { get; set; }
    public int? ClienteId { get; set; }
    public string Telefono { get; set; } = "";
    public string? DestinoCorreo { get; set; }
    public string? CodigoProtegido { get; set; }
    public string? SecretoProtegido { get; set; }
    public string CodigoHash { get; set; } = "";
    public string IpHash { get; set; } = "";
    public DateTime CreadaUtc { get; set; }
    public DateTime CaducaUtc { get; set; }
    public DateTime? ConsumidaUtc { get; set; }
    public int Intentos { get; set; }
    public EstadoNotificacion EstadoEnvio { get; set; }
    public DateTime? ProcesadoUtc { get; set; }
    public byte[] Version { get; set; } = [];
}
