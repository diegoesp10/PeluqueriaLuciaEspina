using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Sorteos;
using PeluqueriaLucia.Domain.Sorteos;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Productos de sorteo: lo que el salón sortea y no es de la peluquería (una cena, unas entradas…), con su nombre y su
/// descripción, y en qué sorteos ha estado, quién lo ganó y si se le entregó.
/// </summary>
public sealed class ProductosSorteoController(ProductosSorteoService productos, SorteosService sorteos, SorteosPublicos publicos) : AdminController
{
    private const string AvisoConcurrencia = "Un sorteo con este premio acaba de cambiar en otra pestaña y no se ha guardado nada. Vuelve a intentarlo.";

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new ProductosSorteoModelo(await productos.ListarAsync(ct)));

    /// <param name="sorteo">Si se crea desde un sorteo abierto, se añade a él como premio.</param>
    [HttpGet]
    public async Task<IActionResult> Nuevo(int? sorteo, CancellationToken ct) =>
        View("Edicion", new ProductoSorteoEdicionModelo(null, new ProductoSorteoFormulario(), await DestinoAsync(sorteo, ct)));

    [HttpPost]
    public async Task<IActionResult> Nuevo(ProductoSorteoFormulario formulario, int? sorteo, CancellationToken ct)
    {
        var destino = await DestinoAsync(sorteo, ct);
        if (ModelState.IsValid)
        {
            var resultado = await productos.CrearAsync(formulario.Ficha(), ct);
            if (resultado.Exito)
            {
                var nombre = formulario.Nombre?.Trim();
                if (destino is null)
                {
                    this.AvisarExito($"«{nombre}» creado. Ya puedes añadirlo como premio a un sorteo.");
                    return RedirectToAction(nameof(Ficha), new { id = resultado.Valor });
                }

                var agregado = await ProtegerAsync(() => sorteos.AgregarPremioAsync(destino.Id, new PremioElegido(TipoPremio.ProductoSorteo, resultado.Valor), 1, ct));
                if (agregado.Exito)
                {
                    publicos.Invalidar();
                    this.AvisarExito($"«{nombre}» creado y añadido como premio.");
                    return Redirect(Url.Action(nameof(SorteosController.Detalle), "Sorteos", new { id = destino.Id }) + "#premios");
                }

                this.AvisarError($"«{nombre}» se ha creado, pero no se ha podido añadir al sorteo: {agregado.Error!.Mensaje}");
                return RedirectToAction(nameof(Ficha), new { id = resultado.Valor });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", new ProductoSorteoEdicionModelo(null, formulario, destino));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        await productos.ObtenerAsync(id, ct) is { } producto
            ? View("Edicion", new ProductoSorteoEdicionModelo(id, ProductoSorteoFormulario.Desde(producto)))
            : NotFound();

    [HttpPost]
    public async Task<IActionResult> Editar(int id, ProductoSorteoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await ProtegerAsync(async () => await productos.EditarAsync(id, formulario.Ficha(), ct));
            if (resultado is Resultado<int> { Exito: true, Valor: var puestosAlDia })
            {
                if (puestosAlDia > 0)
                    publicos.Invalidar();
                this.AvisarExito(puestosAlDia switch
                {
                    0 => "Cambios guardados.",
                    1 => "Cambios guardados, también en el sorteo en marcha que lo tiene como premio.",
                    _ => $"Cambios guardados, también en los {puestosAlDia} sorteos en marcha que lo tienen como premio.",
                });
                return RedirectToAction(nameof(Ficha), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return await productos.ObtenerAsync(id, ct) is null ? NotFound() : View("Edicion", new ProductoSorteoEdicionModelo(id, formulario));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(int id, CancellationToken ct)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return NotFound();
        return View(new ProductoSorteoFichaModelo(producto, await productos.HistorialAsync(id, ct)));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var resultado = await productos.EliminarAsync(id, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Ficha), new { id });
        }

        this.AvisarExito(resultado.Valor switch
        {
            0 => "Eliminado: ya no se ofrece como premio. Los sorteos en los que estuvo lo conservan en su historial.",
            1 => "Eliminado: ya no se ofrece como premio. Sigue en 1 sorteo en marcha; quítalo de allí si ya no se sortea.",
            var n => $"Eliminado: ya no se ofrece como premio. Sigue en {n} sorteos en marcha; quítalo de allí si ya no se sortea.",
        });
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Recuperar(int id, CancellationToken ct)
    {
        var resultado = await productos.RecuperarAsync(id, ct);
        if (resultado.Exito)
            this.AvisarExito("Recuperado: ya se puede añadir otra vez como premio.");
        else
            this.AvisarError(resultado.Error!.Mensaje);
        return RedirectToAction(nameof(Ficha), new { id });
    }

    /// <summary>El sorteo al que se añadirá el producto nuevo, si existe y sigue abierto.</summary>
    private async Task<Sorteo?> DestinoAsync(int? sorteo, CancellationToken ct) =>
        sorteo is { } id && await sorteos.ObtenerAsync(id, ct) is { Abierto: true } abierto ? abierto : null;

    private static async Task<Resultado> ProtegerAsync(Func<Task<Resultado>> accion)
    {
        try
        {
            return await accion();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Error(AvisoConcurrencia);
        }
    }
}
