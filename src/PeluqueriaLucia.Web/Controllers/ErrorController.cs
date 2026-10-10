using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Controllers;

/// <summary>Página de error amable. Responde a cualquier método porque se ejecuta en lugar de la petición que falló.</summary>
[IgnoreAntiforgeryToken]
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ErrorController(ILogger<ErrorController> logger) : Controller
{
    [Route("error")]
    public IActionResult Index()
    {
        var fallo = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        if (fallo is not null)
            logger.LogError(fallo.Error, "Error no controlado en {Ruta}", fallo.Path);

        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View("Error", new ErrorModelo(500, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }

    [Route("error/{codigo:int}")]
    public IActionResult Codigo(int codigo) =>
        View("Error", new ErrorModelo(codigo, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
