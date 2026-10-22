using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class CatalogoController(CatalogoService catalogo, PeluqueriaLucia.Application.Publico.ContenidoService contenido) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(Sexo? sexo, string? q, CancellationToken ct)
    {
        var todos = await catalogo.BuscarAsync(q, sexo, incluirArchivados: true, ct);
        return View(new CatalogoIndiceModelo(todos.Where(s => s.Activo).ToList(), todos.Where(s => !s.Activo).ToList(), sexo, q?.Trim()));
    }

    [HttpGet]
    public async Task<IActionResult> Nuevo(CancellationToken ct) => View("Edicion", new ServicioEdicionModelo(null, new ServicioFormulario(), null, Categorias: await contenido.CategoriasAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Nuevo(ServicioFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await catalogo.CrearAsync(formulario.ADatos(), formulario.AArchivo(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito($"«{formulario.Nombre}» añadido al catálogo.");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", new ServicioEdicionModelo(null, formulario, null, Categorias: await contenido.CategoriasAsync(ct)));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var servicio = await catalogo.ObtenerAsync(id, ct);
        return servicio is null
            ? NotFound()
            : View("Edicion", new ServicioEdicionModelo(id, ServicioFormulario.Desde(servicio), servicio.ImagenRuta, servicio.Activo, await contenido.CategoriasAsync(ct)));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, ServicioFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var resultado = await catalogo.ActualizarAsync(id, formulario.ADatos(), formulario.AArchivo(), formulario.QuitarImagen, ct);
                if (resultado.Exito)
                {
                    this.AvisarExito("Cambios guardados. Las visitas anteriores conservan su precio.");
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AgregarError(resultado.Error!);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                // Al cambiar la dualidad se actualizan sus citas por venir; si una cambiaba a la vez en la agenda, no se guarda nada
                this.AvisarError("Una cita de este servicio acaba de cambiar en la agenda y no se ha guardado nada. Vuelve a intentarlo.");
                return RedirectToAction(nameof(Editar), new { id });
            }
        }

        var servicio = await catalogo.ObtenerAsync(id, ct);
        return servicio is null
            ? NotFound()
            : View("Edicion", new ServicioEdicionModelo(id, formulario, servicio.ImagenRuta, servicio.Activo, await contenido.CategoriasAsync(ct)));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var resultado = await catalogo.EliminarAsync(id, ct);
        if (!resultado.Exito)
            this.AvisarError(resultado.Error!.Mensaje);
        else if (resultado.Valor == Eliminacion.Archivado)
            this.AvisarExito("El servicio aparece en visitas anteriores, así que se ha archivado en lugar de borrarse.");
        else
            this.AvisarExito("Servicio eliminado.");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reactivar(int id, CancellationToken ct)
    {
        var resultado = await catalogo.ReactivarAsync(id, ct);
        if (resultado.Exito)
            this.AvisarExito("El servicio vuelve a estar disponible.");
        else
            this.AvisarError(resultado.Error!.Mensaje);

        return RedirectToAction(nameof(Index));
    }
}
