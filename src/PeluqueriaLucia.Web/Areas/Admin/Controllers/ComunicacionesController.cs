using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Avisos a clientes sobre sus citas: el salón los revisa, retoca el texto si quiere y los envía (correo) o los abre en
/// WhatsApp. Las citas se gestionan en Agenda; aquí solo viven los mensajes.
/// </summary>
public sealed class ComunicacionesController(IComunicacionesCitas comunicaciones) : AdminController
{
    private static readonly EstadoNotificacion[] PendientesDeRevisar = [EstadoNotificacion.Pendiente, EstadoNotificacion.Error, EstadoNotificacion.Incierto];

    [HttpGet("/admin/comunicaciones")]
    public async Task<IActionResult> Index(string? filtro, string? q, CancellationToken ct)
    {
        var mensajes = await comunicaciones.ListarAsync(ct);
        var pendientes = mensajes.Count(m => PendientesDeRevisar.Contains(m.Estado));
        var elegido = filtro is "enviados" or "todos" ? filtro : "pendientes";
        IReadOnlyList<ComunicacionCita> visibles = elegido switch
        {
            "enviados" => mensajes.Where(m => m.Estado is EstadoNotificacion.AceptadoServidor or EstadoNotificacion.Manual).ToList(),
            "todos" => mensajes,
            _ => mensajes.Where(m => PendientesDeRevisar.Contains(m.Estado)).ToList(),
        };
        var texto = q?.Trim();
        if (!string.IsNullOrEmpty(texto))
            visibles = visibles.Where(m => Coincide(m.Destinatario, texto) || Coincide(m.Texto, texto)).ToList();
        return View(new ComunicacionesPagina(visibles, elegido, pendientes, comunicaciones.CorreoConfigurado, comunicaciones.EnlaceConfigurado, comunicaciones.EnvioAutomatico, texto));
    }

    /// <summary>Busca sin distinguir mayúsculas ni acentos («sofia» encuentra «Sofía»).</summary>
    private static bool Coincide(string valor, string texto) =>
        System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(valor, texto,
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;

    [HttpPost("/admin/comunicaciones/texto")]
    public async Task<IActionResult> Texto(Guid id, string texto, CancellationToken ct)
    {
        Avisar(await comunicaciones.ActualizarTextoAsync(id, texto, ct), "Texto del aviso guardado.");
        return Volver(id);
    }

    [HttpPost("/admin/comunicaciones/enviar")]
    public async Task<IActionResult> Enviar(Guid id, string? texto, CancellationToken ct)
    {
        Avisar(await comunicaciones.EnviarCorreoAsync(id, texto, ct), "Correo enviado: el servidor de correo lo ha aceptado.");
        return Volver(id);
    }

    [HttpPost("/admin/comunicaciones/whatsapp-enviado")]
    public async Task<IActionResult> WhatsAppEnviado(Guid id, CancellationToken ct)
    {
        Avisar(await comunicaciones.MarcarWhatsAppAsync(id, ct), "Mensaje marcado como enviado. La cita sigue pendiente de que el cliente acepte.");
        return Volver(id);
    }

    private IActionResult Volver(Guid id) => Redirect(Url.Action(nameof(Index)) + "#aviso-" + id);

    private void Avisar(Resultado r, string mensaje)
    {
        if (r.Exito) this.AvisarExito(mensaje);
        else this.AvisarError(r.Error!.Mensaje);
    }
}
