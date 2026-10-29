using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

/// <summary>Los sorteos que el salón anuncia en la web: qué se sortea y cuándo. Nunca enseña participantes ni ganadores.</summary>
[AllowAnonymous]
[EnableRateLimiting(LimitesDePeticiones.Publico)]
public sealed class SorteosWebController(SorteosPublicos sorteos) : Controller
{
    [HttpGet("/sorteos")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await sorteos.ListarAsync(ct));
}
