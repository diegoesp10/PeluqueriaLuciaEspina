namespace PeluqueriaLucia.Domain.Clientes;

/// <summary>Registro interno, conservado incluso cuando el administrador corrige una falta.</summary>
public sealed class FaltaAsistencia
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Guid ReservaId { get; set; }
    public DateTime InicioCita { get; set; }
    public DateTime RegistradaUtc { get; set; }
    public int AdministradorId { get; set; }
    public string Nota { get; set; } = "";
    public DateTime? AnuladaUtc { get; set; }
    public int? AnuladaPorId { get; set; }
    public string? MotivoAnulacion { get; set; }
}
