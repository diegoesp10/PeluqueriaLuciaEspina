using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Infrastructure.Almacenamiento;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class ImagenesController(IOptions<OpcionesImagenes> opciones) : AdminController
{
    [HttpGet("/admin/imagenes/{nombre}")]
    public IActionResult Obtener(string nombre)
    {
        if (nombre.Length > 128 || nombre != Path.GetFileName(nombre) || nombre.Contains('\\')) return NotFound();
        var extension = Path.GetExtension(nombre).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif")) return NotFound();
        var ruta = Path.Combine(opciones.Value.Carpeta, nombre);
        if (!System.IO.File.Exists(ruta)) return NotFound();
        new FileExtensionContentTypeProvider().TryGetContentType(nombre, out var tipo);
        return PhysicalFile(ruta, tipo ?? "application/octet-stream");
    }
}
