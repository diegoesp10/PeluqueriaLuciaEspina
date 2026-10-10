using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class SeguridadController(AccesoService acceso, MonitorPeticiones monitor, IOptions<OpcionesSeguridad> opciones) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(new SeguridadModelo(SesionId,
        await acceso.ListarSesionesAsync(AdministradorId, ct), await acceso.UltimosIntentosAsync(50, ct), monitor.Resumen(), opciones.Value));

    [HttpPost]
    public async Task<IActionResult> CerrarOtras(CancellationToken ct)
    {
        var cantidad = await acceso.CerrarOtrasSesionesAsync(AdministradorId, SesionId, ct);
        this.AvisarExito($"Se han cerrado {cantidad} sesiones.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Revocar(Guid id, CancellationToken ct)
    {
        if (id == SesionId)
            return BadRequest();
        var resultado = await acceso.CerrarSesionDeAsync(AdministradorId, id, ct);
        if (!resultado.Exito) return NotFound();
        this.AvisarExito("Sesión cerrada.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Contrasena() => View(new ContrasenaFormulario());

    [HttpPost]
    public async Task<IActionResult> Contrasena(ContrasenaFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await acceso.CambiarContrasenaAsync(AdministradorId, SesionId, formulario.Actual, formulario.Nueva, ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Contraseña cambiada. Las otras sesiones se han cerrado.");
                return RedirectToAction(nameof(Index));
            }
            ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
        }
        ModelState.SetModelValue(nameof(formulario.Actual), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(formulario.Nueva), string.Empty, string.Empty);
        ModelState.SetModelValue(nameof(formulario.Confirmacion), string.Empty, string.Empty);
        return View(new ContrasenaFormulario());
    }
}
