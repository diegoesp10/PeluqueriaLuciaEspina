using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Models;
using PeluqueriaLucia.Web.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// «Contacto de la web»: los teléfonos que enseña la web y que firman los avisos de las citas, en su orden. Sin
/// JavaScript, «Añadir otro teléfono» vuelve a pintar el formulario con una fila más sin guardar nada.
/// </summary>
public sealed class ContactoController(ContenidoService contenido, TelefonosPublicos publicos) : AdminController
{
    private const int Maximo = ContenidoService.MaximoTelefonos;

    [HttpGet("/admin/contacto")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new ContactoPagina((await publicos.ListarAsync(ct)).Select(t => t.Numero).DefaultIfEmpty("").ToList(), Maximo));

    [HttpPost("/admin/contacto")]
    public async Task<IActionResult> Index(List<string?>? telefonos, string? anadir, CancellationToken ct)
    {
        var lista = (telefonos ?? []).Select(t => t?.Trim() ?? "").ToList();
        if (anadir is not null)
        {
            if (lista.Count < Maximo) lista.Add("");
            return View(new ContactoPagina(lista, Maximo));
        }
        var resultado = await contenido.GuardarTelefonosAsync(lista, ct);
        if (!resultado.Exito)
        {
            ModelState.AddModelError("Telefonos", resultado.Error!.Mensaje);
            return View(new ContactoPagina(lista.Count == 0 ? [""] : lista, Maximo));
        }
        publicos.Invalidar();
        this.AvisarExito("Teléfonos de contacto guardados. La web ya los muestra.");
        return Redirect("/admin/contacto");
    }
}
