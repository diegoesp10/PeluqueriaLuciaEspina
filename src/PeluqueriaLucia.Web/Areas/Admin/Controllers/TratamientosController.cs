using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class TratamientosController(ContenidoService contenido) : AdminController
{
    [HttpGet] public async Task<IActionResult> Index(CancellationToken ct) => View(new TratamientosIndice(await contenido.CategoriasAsync(ct), await contenido.TratamientosAsync(false, ct)));
    [HttpGet] public async Task<IActionResult> Nuevo(CancellationToken ct) => View("Edicion", new TratamientoEdicion(null, new(), await contenido.CategoriasAsync(ct)));
    [HttpGet] public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var t = await contenido.TratamientoAsync(id, ct); if (t is null) return NotFound();
        return View("Edicion", new TratamientoEdicion(id, new() { CategoriaId = t.CategoriaId, Nombre = t.Nombre, Descripcion = t.Descripcion, Precio = t.Precio,
            TipoPrecio = t.TipoPrecio, DuracionMinutos = t.DuracionMinutos, Orden = t.Orden, Publicado = t.Publicado, Destacado = t.Destacado }, await contenido.CategoriasAsync(ct)));
    }
    [HttpPost] public Task<IActionResult> Nuevo(TratamientoFormulario formulario, CancellationToken ct) => GuardarAsync(null, formulario, ct);
    [HttpPost] public Task<IActionResult> Editar(int id, TratamientoFormulario formulario, CancellationToken ct) => GuardarAsync(id, formulario, ct);
    private async Task<IActionResult> GuardarAsync(int? id, TratamientoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid) { var resultado = await contenido.GuardarTratamientoAsync(id, formulario.Datos(), ct); if (resultado.Exito) { this.AvisarExito("Tratamiento guardado. La web ya muestra los cambios."); return RedirectToAction(nameof(Index)); } ModelState.AddModelError("", resultado.Error!.Mensaje); }
        return View("Edicion", new TratamientoEdicion(id, formulario, await contenido.CategoriasAsync(ct)));
    }
    [HttpPost] public async Task<IActionResult> Eliminar(int id, CancellationToken ct) { var r = await contenido.EliminarTratamientoAsync(id, ct); if (r.Exito) this.AvisarExito("Tratamiento eliminado de la web."); else this.AvisarError(r.Error!.Mensaje); return RedirectToAction(nameof(Index)); }
    [HttpGet] public async Task<IActionResult> Categorias(CancellationToken ct) => View(await contenido.CategoriasAsync(ct));
    [HttpGet] public async Task<IActionResult> Categoria(int? id, CancellationToken ct)
    {
        var c = id is { } clave ? await contenido.CategoriaAsync(clave, ct) : null;
        if (id is not null && c is null) return NotFound();
        return View(new CategoriaEdicion(id, c is null ? new() : new() { Nombre = c.Nombre, Orden = c.Orden }));
    }
    [HttpPost] public async Task<IActionResult> Categoria(int? id, CategoriaFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid) { var r = await contenido.GuardarCategoriaAsync(id, formulario.Nombre, formulario.Orden, ct); if (r.Exito) return RedirectToAction(nameof(Categorias)); ModelState.AddModelError("", r.Error!.Mensaje); }
        return View(new CategoriaEdicion(id, formulario));
    }
    [HttpPost] public async Task<IActionResult> EliminarCategoria(int id, CancellationToken ct) { var r = await contenido.EliminarCategoriaAsync(id, ct); if (!r.Exito) this.AvisarError(r.Error!.Mensaje); return RedirectToAction(nameof(Categorias)); }
}
