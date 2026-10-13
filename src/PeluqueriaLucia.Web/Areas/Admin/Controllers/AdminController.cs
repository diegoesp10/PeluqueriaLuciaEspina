using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Base de todas las pantallas del panel: exigen una sesión de administrador válida (token comprobado en cada
/// petición) y tienen su propio límite de llamadas por IP.
/// </summary>
[Area("Admin")]
[Authorize(Policy = Autenticacion.PoliticaAdministrador)]
[EnableRateLimiting(LimitesDePeticiones.Panel)]
public abstract class AdminController : Controller
{
    protected int AdministradorId => int.Parse(User.FindFirstValue(Autenticacion.ClaimAdministrador)!, System.Globalization.CultureInfo.InvariantCulture);

    protected Guid SesionId => Guid.Parse(User.FindFirstValue(Autenticacion.ClaimSesion)!);
}
