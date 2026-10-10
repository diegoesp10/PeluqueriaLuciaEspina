using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

public interface ISmsCitas
{
    bool Activo { get; }
    bool ValidarFirma(string ruta, string firma, IReadOnlyDictionary<string, string> campos);
    Task ProcesarWebhookAsync(IReadOnlyDictionary<string, string> campos, bool entrada, CancellationToken ct);
    Task<IReadOnlyList<SmsEntrante>> EntrantesAsync(CancellationToken ct);
    Task ProcesarColaAsync(CancellationToken ct);
}
