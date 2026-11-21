using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Reseñas de las clientas: pendientes de revisar, publicadas (con la fecha de publicación), ocultas (estuvieron en la web y
/// se quitaron) y rechazadas. Tras cada botón se vuelve a la misma pestaña, con los contadores al día.
/// </summary>
public sealed class ResenasController(ResenasService resenas, IFotosResenas fotos) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(EstadoResena? estado = EstadoResena.Pendiente, int pagina = 1, string? q = null, CancellationToken ct = default) =>
        View(new ResenasIndice(await resenas.AdministrarAsync(estado, pagina, q, ct), estado, q?.Trim(), await resenas.ConteoAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Moderar(Guid id, ModeracionFormulario formulario, CancellationToken ct)
    {
        var volver = new { estado = formulario.Desde ?? formulario.Estado, pagina = formulario.Pagina > 1 ? formulario.Pagina : (int?)null, q = formulario.Q };
        if (!ModelState.IsValid)
        {
            this.AvisarError("La respuesta no puede superar 1500 caracteres.");
            return RedirectToAction(nameof(Index), volver);
        }

        if (await resenas.ObtenerAsync(id, ct) is not { } resena)
            return NotFound();
        var (alias, antes) = (resena.Alias, resena.Estado);
        try
        {
            var r = await resenas.ModerarAsync(id, formulario.Estado, formulario.Respuesta, formulario.RespuestaEn, ct);
            if (r.Exito)
                this.AvisarExito(ResenasIndice.Aviso(alias, antes, formulario.Estado));
            else
                this.AvisarError(r.Error!.Mensaje);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            this.AvisarError("La reseña ha cambiado en otra pestaña. Revisa su estado actual.");
        }

        return RedirectToAction(nameof(Index), volver);
    }

    [HttpGet]
    public async Task<IActionResult> Foto(Guid id, CancellationToken ct)
    {
        var r = await resenas.ObtenerAsync(id, ct);
        if (r?.Foto is null || fotos.RutaFisica(r.Foto) is not { } ruta || !System.IO.File.Exists(ruta))
            return NotFound();
        return PhysicalFile(ruta, "image/jpeg");
    }
}
