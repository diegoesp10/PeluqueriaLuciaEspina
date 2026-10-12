using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public interface IUrlPublica { Uri? Obtener(); }
public sealed record PropuestaCita(DateTime Inicio, DateTime Fin, string Servicio, DateTime CaducaUtc);
public sealed record ComunicacionCita(Guid Id, string Destinatario, CanalConfirmacion Canal, EstadoNotificacion Estado,
    string Texto, Uri? WhatsApp, DateTime CreadoUtc);
public interface IComunicacionesCitas
{
    bool CorreoConfigurado { get; }
    bool EnlaceConfigurado { get; }
    Task<IReadOnlyList<ComunicacionCita>> ListarAsync(CancellationToken ct);
    Task<PropuestaCita?> ConsultarPropuestaAsync(string token, CancellationToken ct);
    Task<Resultado> AceptarAsync(string token, CancellationToken ct);
    Task<Resultado> MarcarWhatsAppAsync(Guid id, CancellationToken ct);
    Task<Resultado> ReintentarCorreoAsync(Guid id, CancellationToken ct);
    Task ProcesarColaAsync(CancellationToken ct);
}
