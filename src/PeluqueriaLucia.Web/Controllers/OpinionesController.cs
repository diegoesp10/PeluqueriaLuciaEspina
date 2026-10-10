using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Web.Models;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous, EnableRateLimiting(LimitesDePeticiones.Publico), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OpinionesController(ResenasService resenas, IFotosResenas fotos) : Controller
{
    [HttpGet("/opiniones")] public async Task<IActionResult> Index(int pagina = 1, CancellationToken ct = default) => View(new OpinionesModelo(await resenas.PublicasAsync(pagina, ct), new()));
    [HttpPost("/opiniones"), EnableRateLimiting(LimitesDePeticiones.Resenas), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<IActionResult> Index(OpinionFormulario formulario, CancellationToken ct)
    {
        if (formulario.Foto?.Length > 5 * 1024 * 1024) ModelState.AddModelError("Foto", "La foto no puede superar 5 MB.");
        if (ModelState.IsValid)
        {
            try {
                await using var foto = formulario.Foto?.OpenReadStream();
                var r = await resenas.EnviarAsync(formulario.Codigo, formulario.Alias, formulario.Comentario, formulario.Estrellas, foto, ct);
                if (r.Exito) { TempData["opinion-enviada"] = true; return Redirect(Url.Action(nameof(Index)) + "#dejar-opinion"); }
                ModelState.AddModelError(r.Error!.Campo ?? "", r.Error.Mensaje);
            } catch (Microsoft.EntityFrameworkCore.DbUpdateException) { ModelState.AddModelError("", "Este ticket ya se está usando o la opinión ha cambiado. Vuelve a intentarlo."); }
        }
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(new OpinionesModelo(await resenas.PublicasAsync(1, ct), formulario));
    }
    [HttpGet("/opiniones/foto/{id:guid}")] public async Task<IActionResult> Foto(Guid id, CancellationToken ct)
    {
        var r = await resenas.ObtenerAsync(id, ct);
        if (r?.Estado != EstadoResena.Publicada || r.Foto is null || fotos.RutaFisica(r.Foto) is not { } ruta || !System.IO.File.Exists(ruta)) return NotFound();
        return PhysicalFile(ruta, "image/jpeg");
    }
}
