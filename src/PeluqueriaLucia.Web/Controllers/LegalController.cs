using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

/// <summary>Política de privacidad y términos de uso. El texto legal completo está en una vista por idioma.</summary>
[AllowAnonymous]
[EnableRateLimiting(LimitesDePeticiones.Publico)]
public sealed class LegalController(IOptionsSnapshot<ContenidoPublico> contenido) : Controller
{
    [HttpGet("/privacidad")]
    public IActionResult Privacidad() => View(PorIdioma("Privacidad"), contenido.Value);

    [HttpGet("/terminos")]
    public IActionResult Terminos() => View(PorIdioma("Terminos"), contenido.Value);

    private string PorIdioma(string vista) => $"{vista}.{Idioma.Actual(HttpContext)}";
}
