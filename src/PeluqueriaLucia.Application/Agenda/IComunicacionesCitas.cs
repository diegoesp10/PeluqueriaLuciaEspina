using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public interface IUrlPublica { Uri? Obtener(); }
/// <summary>
/// Cómo salen los correos de citas: revisados y enviados a mano desde Comunicaciones (lo normal) o automáticamente
/// por la cola en cuanto se crean (<c>PELUQUERIA_CORREO_AUTOMATICO=true</c>).
/// </summary>
public interface IAjustesComunicaciones { bool EnvioAutomatico { get; } }
public sealed record PropuestaCita(DateTime Inicio, DateTime Fin, string Servicio, DateTime CaducaUtc);
/// <summary>Un aviso de la cola. <see cref="Texto"/> ya lleva el enlace de aceptación cuando lo hay; <see cref="Editable"/> si aún se puede retocar.</summary>
public sealed record ComunicacionCita(Guid Id, Guid ReservaId, string Destinatario, CanalConfirmacion Canal, EstadoNotificacion Estado,
    string Texto, Uri? WhatsApp, DateTime CreadoUtc, bool Editable = false, string? TextoBase = null);
public interface IComunicacionesCitas
{
    bool CorreoConfigurado { get; }
    bool EnlaceConfigurado { get; }
    bool EnvioAutomatico { get; }
    Task<IReadOnlyList<ComunicacionCita>> ListarAsync(CancellationToken ct);
    Task<PropuestaCita?> ConsultarPropuestaAsync(string token, CancellationToken ct);
    Task<Resultado> AceptarAsync(string token, CancellationToken ct);
    Task<Resultado> MarcarWhatsAppAsync(Guid id, CancellationToken ct);
    Task<Resultado> ReintentarCorreoAsync(Guid id, CancellationToken ct);
    /// <summary>Cambia el texto de un aviso aún no enviado (correo o WhatsApp). El enlace de aceptación se añade aparte y no se edita.</summary>
    Task<Resultado> ActualizarTextoAsync(Guid id, string texto, CancellationToken ct);
    /// <summary>Envía ahora un correo pendiente o rechazado, con el texto indicado si se ha retocado. Devuelve el resultado del servidor de correo.</summary>
    Task<Resultado> EnviarCorreoAsync(Guid id, string? texto, CancellationToken ct);
    Task ProcesarColaAsync(CancellationToken ct);
}
