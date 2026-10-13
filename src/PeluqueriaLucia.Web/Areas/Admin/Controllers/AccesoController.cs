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
public sealed class AccesoController(AccesoService acceso, MfaService mfa, DesafioCookie desafioCookie) : Controller
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
            var resultado = await mfa.IniciarAsync(formulario.Usuario, formulario.Contrasena,
                new Origen(IpCliente.De(HttpContext), Request.Headers.UserAgent.ToString()), ct);
            if (resultado.Exito)
            {
                desafioCookie.Guardar(HttpContext, resultado.Valor!, formulario.Volver);
                return RedirectToAction(nameof(Verificar));
            }
            ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
        }
        formulario.Contrasena = string.Empty;
        ModelState.SetModelValue(nameof(formulario.Contrasena), string.Empty, string.Empty);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(formulario);
    }

    [AllowAnonymous, HttpGet("/admin/acceso/verificar")]
    public async Task<IActionResult> Verificar(CancellationToken ct)
    {
        var cookie = desafioCookie.Leer(HttpContext);
        var modelo = cookie is null ? null : await ModeloMfaAsync(cookie, ct);
        if (modelo is null) { desafioCookie.Borrar(HttpContext); return RedirectToAction(nameof(Index)); }
        return View(modelo);
    }

    [AllowAnonymous, HttpPost("/admin/acceso/verificar")]
    public async Task<IActionResult> Verificar(MfaFormulario formulario, CancellationToken ct)
    {
        var cookie = desafioCookie.Leer(HttpContext);
        if (cookie is null) return RedirectToAction(nameof(Index));
        if (ModelState.IsValid)
        {
            try
            {
                var resultado = await mfa.CompletarAsync(cookie.Id, cookie.Token, formulario.Codigo,
                    new Origen(IpCliente.De(HttpContext), Request.Headers.UserAgent.ToString()), ct);
                if (resultado.Exito)
                {
                    var sesion = resultado.Valor!.Sesion;
                    await HttpContext.SignInAsync(Autenticacion.Esquema, Autenticacion.CrearPrincipal(sesion),
                        new AuthenticationProperties { IsPersistent = false, ExpiresUtc = new DateTimeOffset(sesion.ExpiraEn, TimeSpan.Zero) });
                    desafioCookie.Borrar(HttpContext);
                    if (resultado.Valor.Recuperacion.Count > 0) {
                        TempData[Autenticacion.AvisoSesiones] = true;
                        return View("Recuperacion", resultado.Valor.Recuperacion);
                    }
                    if (Url.IsLocalUrl(cookie.Volver) && new PathString(cookie.Volver!.Split('?')[0]).StartsWithSegments("/admin")) return LocalRedirect(cookie.Volver);
                    return RedirectToAction("Index", "Panel");
                }
                ModelState.AddModelError(string.Empty, resultado.Error!.Mensaje);
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                desafioCookie.Borrar(HttpContext);
                return RedirectToAction(nameof(Index));
            }
        }
        ModelState.Remove(nameof(formulario.Codigo));
        var modelo = await ModeloMfaAsync(cookie, ct);
        if (modelo is null) { desafioCookie.Borrar(HttpContext); return RedirectToAction(nameof(Index)); }
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(modelo);
    }

    private async Task<MfaModelo?> ModeloMfaAsync(DesafioCookieDatos cookie, CancellationToken ct)
    {
        var presentacion = await mfa.PresentarAsync(cookie.Id, cookie.Token, IpCliente.De(HttpContext), ct);
        if (presentacion is null) return null;
        string? qr = null;
        if (presentacion.Secreto is { } secreto)
        {
            var uri = $"otpauth://totp/{Uri.EscapeDataString("Lucía Peluquería:" + presentacion.Usuario)}?secret={secreto}&issuer={Uri.EscapeDataString("Lucía Peluquería")}&algorithm=SHA1&digits=6&period=30";
            using var datos = QRCoder.QRCodeGenerator.GenerateQrCode(uri, QRCoder.QRCodeGenerator.ECCLevel.Q);
            using var imagen = new QRCoder.PngByteQRCode(datos);
            qr = "data:image/png;base64," + Convert.ToBase64String(imagen.GetGraphic(6));
        }
        return new(presentacion.Usuario, presentacion.Secreto, qr);
    }

    [Authorize(Policy = Autenticacion.PoliticaAdministrador), HttpPost("/admin/acceso/salir")]
    public async Task<IActionResult> Salir(CancellationToken ct)
    {
        if (Guid.TryParse(User.FindFirstValue(Autenticacion.ClaimSesion), out var sesion))
            await acceso.CerrarSesionAsync(sesion, ct);
        await HttpContext.SignOutAsync(Autenticacion.Esquema);
        desafioCookie.Borrar(HttpContext);
        TempData[Autenticacion.AvisoSesiones] = true;
        return RedirectToAction(nameof(Index));
    }
}
