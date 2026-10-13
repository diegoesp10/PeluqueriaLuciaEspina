using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class TratamientosController(ContenidoService contenido, PeluqueriaLucia.Application.Catalogo.CatalogoService catalogo) : AdminController
{
    [HttpGet] public async Task<IActionResult> Index(string? q, CancellationToken ct) =>
        View(new TratamientosIndice(await contenido.CategoriasAsync(ct), await contenido.BuscarTratamientosAsync(q, ct), q?.Trim()));

    /// <summary>Flechas de la lista: sube (-1) o baja (+1) el servicio dentro de su categoría en la web.</summary>
    [HttpPost] public async Task<IActionResult> MoverTratamiento(int id, int desplazamiento, CancellationToken ct)
    {
        var r = await contenido.MoverTratamientoAsync(id, desplazamiento, ct);
        if (!r.Exito) this.AvisarError(r.Error!.Mensaje);
        return Redirect(Url.Action(nameof(Index)) + "#servicio-" + id);
    }

    /// <summary>Flechas de la lista de categorías: sube (-1) o baja (+1) la categoría en la web.</summary>
    [HttpPost] public async Task<IActionResult> MoverCategoria(int id, int desplazamiento, CancellationToken ct)
    {
        var r = await contenido.MoverCategoriaAsync(id, desplazamiento, ct);
        if (!r.Exito) this.AvisarError(r.Error!.Mensaje);
        return Redirect(Url.Action(nameof(Categorias)) + "#categoria-" + id);
    }
    [HttpGet] public async Task<IActionResult> Nuevo(CancellationToken ct) => View("Edicion", new TratamientoEdicion(null, new(), await contenido.CategoriasAsync(ct), await catalogo.ListarAsync(null, false, ct)));
    [HttpGet] public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var t = await contenido.TratamientoAsync(id, ct); if (t is null) return NotFound();
        return RedirectToAction("Editar", "Catalogo", new { id });
    }
    [HttpPost] public Task<IActionResult> Nuevo(TratamientoFormulario formulario, CancellationToken ct) => GuardarAsync(null, formulario, ct);
    [HttpPost] public Task<IActionResult> Editar(int id, TratamientoFormulario formulario, CancellationToken ct) => GuardarAsync(id, formulario, ct);
    private async Task<IActionResult> GuardarAsync(int? id, TratamientoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid) { var resultado = await contenido.GuardarTratamientoAsync(id, formulario.Datos(), ct); if (resultado.Exito) { this.AvisarExito("Tratamiento guardado. La web ya muestra los cambios."); return RedirectToAction(nameof(Index)); } ModelState.AddModelError("", resultado.Error!.Mensaje); }
        return View("Edicion", new TratamientoEdicion(id, formulario, await contenido.CategoriasAsync(ct), await catalogo.ListarAsync(null, false, ct)));
    }
    [HttpPost] public async Task<IActionResult> Eliminar(int id, CancellationToken ct) { var r = await contenido.EliminarTratamientoAsync(id, ct); if (r.Exito) this.AvisarExito("Servicio retirado de la web. Sigue disponible en el catálogo."); else this.AvisarError(r.Error!.Mensaje); return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Categorias(CancellationToken ct) => View(await contenido.CategoriasAsync(ct));
    [HttpGet] public async Task<IActionResult> Categoria(int? id, CancellationToken ct)
    {
        var c = id is { } clave ? await contenido.CategoriaAsync(clave, ct) : null;
        if (id is not null && c is null) return NotFound();
        return View(new CategoriaEdicion(id, c is null ? new() : new() { Nombre = c.Nombre, NombreEn = c.NombreEn }));
    }
    [HttpPost] public async Task<IActionResult> Categoria(int? id, CategoriaFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid) { var r = await contenido.GuardarCategoriaAsync(id, formulario.Nombre, formulario.NombreEn, ct); if (r.Exito) return RedirectToAction(nameof(Categorias)); ModelState.AddModelError("", r.Error!.Mensaje); }
        return View(new CategoriaEdicion(id, formulario));
    }
    [HttpPost] public async Task<IActionResult> EliminarCategoria(int id, CancellationToken ct) { var r = await contenido.EliminarCategoriaAsync(id, ct); if (!r.Exito) this.AvisarError(r.Error!.Mensaje); return RedirectToAction(nameof(Categorias)); }
}
