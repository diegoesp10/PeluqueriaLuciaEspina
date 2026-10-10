using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Seguridad;
using System.Security.Claims;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

[Area("Admin")]
[EnableRateLimiting(LimitesDePeticiones.Acceso)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccesoController(AccesoService acceso) : Controller
{
    [AllowAnonymous, HttpGet("/admin/acceso")]
    public IActionResult Index(string? volver)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Panel");
        return View(new AccesoFormulario { Volver = volver });
    }

    [AllowAnonymous, HttpPost("/admin/acceso")]
    public async Task<IActionResult> Index(AccesoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await acceso.IniciarSesionAsync(formulario.Usuario, formulario.Contrasena,
                new Origen(IpCliente.De(HttpContext), Request.Headers.UserAgent.ToString()), ct);
            if (resultado.Exito)
            {
                var sesion = resultado.Valor!;
                await HttpContext.SignInAsync(Autenticacion.Esquema, Autenticacion.CrearPrincipal(sesion),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = new DateTimeOffset(sesion.ExpiraEn, TimeSpan.Zero) });
                if (Url.IsLocalUrl(formulario.Volver) && formulario.Volver!.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
                    return LocalRedirect(formulario.Volver);
                return RedirectToAction("Index", "Panel");
            }
            ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
        }
        formulario.Contrasena = string.Empty;
        ModelState.SetModelValue(nameof(formulario.Contrasena), string.Empty, string.Empty);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(formulario);
    }

    [Authorize(Policy = Autenticacion.PoliticaAdministrador), HttpPost("/admin/acceso/salir")]
    public async Task<IActionResult> Salir(CancellationToken ct)
    {
        if (Guid.TryParse(User.FindFirstValue(Autenticacion.ClaimSesion), out var sesion))
            await acceso.CerrarSesionAsync(sesion, ct);
        await HttpContext.SignOutAsync(Autenticacion.Esquema);
        return RedirectToAction(nameof(Index));
    }
}
