using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PeluqueriaLucia.Web.Models;

namespace PeluqueriaLucia.Web.Controllers;

/// <summary>Página de error amable. Responde a cualquier método porque se ejecuta en lugar de la petición que falló.</summary>
[IgnoreAntiforgeryToken]
[Microsoft.AspNetCore.RateLimiting.DisableRateLimiting]
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

    /// <summary>
    /// Tras un error (404, 429…) o cuando un script redirige aquí al fallar una llamada. Sin reejecución, la respuesta
    /// lleva el mismo código de estado que muestra, para que buscadores y herramientas no la tomen por una página normal.
    /// </summary>
    [Route("error/{codigo:int}")]
    public IActionResult Codigo(int codigo)
    {
        if (codigo is < 400 or > 599) codigo = StatusCodes.Status404NotFound;
        if (Response.StatusCode == StatusCodes.Status200OK) Response.StatusCode = codigo;
        return View("Error", new ErrorModelo(codigo, Activity.Current?.Id ?? HttpContext.TraceIdentifier));
    }
}
