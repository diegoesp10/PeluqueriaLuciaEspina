using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous]
[EnableRateLimiting(LimitesDePeticiones.Publico)]
public sealed class InicioController(IOptionsSnapshot<ContenidoPublico> contenido) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View(contenido.Value);

    [HttpGet("/servicios")]
    public IActionResult Servicios() => View(contenido.Value);
}
