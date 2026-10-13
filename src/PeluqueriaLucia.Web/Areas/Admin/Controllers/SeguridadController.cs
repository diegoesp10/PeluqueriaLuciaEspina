using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class SeguridadController(AccesoService acceso, MfaService mfa, DesafioCookie desafioCookie, MonitorPeticiones monitor, IOptions<OpcionesSeguridad> opciones) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new SeguridadModelo(SesionId,
        await acceso.ListarSesionesAsync(AdministradorId, ct), await acceso.UltimosIntentosAsync(50, ct), monitor.Resumen(), opciones.Value));

    [HttpPost]
    public async Task<IActionResult> CerrarOtras(CancellationToken ct)
    {
        var cantidad = await acceso.CerrarOtrasSesionesAsync(AdministradorId, SesionId, ct);
        TempData[Autenticacion.AvisoSesiones] = true;
        this.AvisarExito($"Se han cerrado {cantidad} sesiones.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revocar(Guid id, CancellationToken ct)
    {
        var resultado = await acceso.CerrarSesionDeAsync(AdministradorId, id, ct);
        if (!resultado.Exito) return NotFound();
        TempData[Autenticacion.AvisoSesiones] = true;
        if (id == SesionId)
        {
            await HttpContext.SignOutAsync(Autenticacion.Esquema);
            return RedirectToAction("Index", "Acceso");
        }
        this.AvisarExito("Sesión cerrada.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> CerrarTodas(CancellationToken ct)
    {
        await acceso.CerrarTodasSesionesAsync(AdministradorId, ct);
        await HttpContext.SignOutAsync(Autenticacion.Esquema);
        TempData[Autenticacion.AvisoSesiones] = true;
        return RedirectToAction("Index", "Acceso");
    }

    [HttpGet]
    public IActionResult Contrasena() => View(new ContrasenaFormulario());

    [HttpPost]
    public async Task<IActionResult> Contrasena(ContrasenaFormulario formulario, CancellationToken ct)
    {
        try {
        if (ModelState.IsValid)
        {
            var resultado = await acceso.CambiarContrasenaAsync(AdministradorId, SesionId, formulario.Actual, formulario.Nueva, ct, formulario.Codigo);
            if (resultado.Exito)
            {
                TempData[Autenticacion.AvisoSesiones] = true;
                this.AvisarExito("Contraseña cambiada. Las otras sesiones se han cerrado.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
        }
        } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) {
            ModelState.AddModelError(string.Empty, "La cuenta ha cambiado en otra pestaña. Revisa el acceso y usa un código nuevo.");
            Response.StatusCode = StatusCodes.Status409Conflict;
        }
        ModelState.SetModelValue(nameof(formulario.Actual), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(formulario.Nueva), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(formulario.Confirmacion), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(formulario.Codigo), string.Empty, string.Empty);
        return View(new ContrasenaFormulario());
    }

    [HttpGet]
    public IActionResult Authenticator() => View(new ReconfigurarMfaFormulario());

    [HttpPost]
    public async Task<IActionResult> Authenticator(ReconfigurarMfaFormulario formulario, CancellationToken ct)
    {
        try {
        if (ModelState.IsValid)
        {
            var resultado = await mfa.ReconfigurarAsync(AdministradorId, formulario.Contrasena, formulario.Codigo,
                new Origen(IpCliente.De(HttpContext), Request.Headers.UserAgent.ToString()), ct);
            if (resultado.Exito) { desafioCookie.Guardar(HttpContext, resultado.Valor!, null); return RedirectToAction("Verificar", "Acceso"); }
            ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
        }
        } catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) {
            ModelState.AddModelError(string.Empty, "Este código ya se ha utilizado o la cuenta ha cambiado. Usa un código nuevo.");
            Response.StatusCode = StatusCodes.Status409Conflict;
        }
        ModelState.Remove(nameof(formulario.Contrasena)); ModelState.Remove(nameof(formulario.Codigo));
        return View(new ReconfigurarMfaFormulario());
    }
}
