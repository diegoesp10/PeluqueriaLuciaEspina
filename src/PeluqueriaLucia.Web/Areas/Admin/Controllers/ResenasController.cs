using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class ResenasController(ResenasService resenas, IFotosResenas fotos) : AdminController
{
    [HttpGet] public async Task<IActionResult> Index(EstadoResena? estado = EstadoResena.Pendiente, int pagina = 1, string? q = null, CancellationToken ct = default) =>
        View(new ResenasIndice(await resenas.AdministrarAsync(estado, pagina, q, ct), estado, q?.Trim()));
    [HttpPost] public async Task<IActionResult> Moderar(Guid id, ModeracionFormulario formulario, CancellationToken ct)
    {
        if (!ModelState.IsValid) { this.AvisarError("La respuesta no puede superar 1500 caracteres."); return RedirectToAction(nameof(Index)); }
        try { var r = await resenas.ModerarAsync(id, formulario.Estado, formulario.Respuesta, ct); if (r.Exito) this.AvisarExito("Reseña actualizada."); else this.AvisarError(r.Error!.Mensaje); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) { this.AvisarError("La reseña ha cambiado en otra pestaña. Revisa su estado actual."); }
        return RedirectToAction(nameof(Index), new { estado = formulario.Estado });
    }
    [HttpGet] public async Task<IActionResult> Foto(Guid id, CancellationToken ct)
    { var r = await resenas.ObtenerAsync(id, ct); if (r?.Foto is null || fotos.RutaFisica(r.Foto) is not { } ruta || !System.IO.File.Exists(ruta)) return NotFound(); return PhysicalFile(ruta, "image/jpeg"); }
}
