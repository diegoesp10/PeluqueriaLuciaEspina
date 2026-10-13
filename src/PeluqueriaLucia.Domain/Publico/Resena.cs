using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Publico;

public enum EstadoResena { Pendiente, Publicada, Rechazada }
public sealed class PermisoResena
{
    private PermisoResena() { }
    public Guid Id { get; private set; }
    public int VisitaId { get; private set; }
    public string CodigoHash { get; private set; } = "";
    public string CodigoProtegido { get; private set; } = "";
    public DateTime ExpiraEn { get; private set; }
    public DateTime? UsadoEn { get; private set; }
    public static PermisoResena Crear(int visita, string hash, string protegido, DateTime ahora) => new() {
        Id = Guid.NewGuid(), VisitaId = visita, CodigoHash = hash, CodigoProtegido = protegido, ExpiraEn = ahora.AddMonths(6) };
    public bool Activo(DateTime ahora) => UsadoEn is null && ahora < ExpiraEn;
    public void Consumir(DateTime ahora) => UsadoEn = ahora;
}
public sealed class Resena
{
    private Resena() { }
    public Guid Id { get; private set; }
    public Guid PermisoId { get; private set; }
    public string Alias { get; private set; } = "";
    public string Comentario { get; private set; } = "";
    public int Estrellas { get; private set; }
    public string? Foto { get; private set; }
    public DateTime CreadaEn { get; private set; }
    public EstadoResena Estado { get; private set; }
    public string? Respuesta { get; private set; }
    /// <summary>Respuesta del salón en inglés. null en las respuestas anteriores al inglés: se muestra la española.</summary>
    public string? RespuestaEn { get; private set; }
    public string? RespuestaEnIdioma(bool ingles) => ingles && RespuestaEn is { Length: > 0 } ? RespuestaEn : Respuesta;
    public static Resena Crear(Guid permiso, string alias, string comentario, int estrellas, string? foto, DateTime ahora)
    {
        if (alias.Trim().Length is < 2 or > 50 || comentario.Trim().Length is < 10 or > 1500 || estrellas is < 1 or > 5)
            throw new ReglaDeNegocioException("Escribe un nombre de 2–50 caracteres, un comentario de 10–1500 y una valoración de 1 a 5.");
        return new() { Id = Guid.NewGuid(), PermisoId = permiso, Alias = alias.Trim(), Comentario = comentario.Trim(),
            Estrellas = estrellas, Foto = foto, CreadaEn = ahora, Estado = EstadoResena.Pendiente };
    }
    public void Moderar(EstadoResena estado, string? respuesta, string? respuestaEn = null)
    {
        if (!Enum.IsDefined(estado) || respuesta?.Length > 1500 || respuestaEn?.Length > 1500) throw new ReglaDeNegocioException("Revisa la respuesta y el estado.");
        Estado = estado; Respuesta = string.IsNullOrWhiteSpace(respuesta) ? null : respuesta.Trim();
        RespuestaEn = Respuesta is null || string.IsNullOrWhiteSpace(respuestaEn) ? null : respuestaEn.Trim();
    }
}
