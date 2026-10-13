using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>Comprobación del panel abierto, sin prolongar la sesión por el sondeo automático.</summary>
public sealed class SesionController : AdminController
{
    [HttpGet(Autenticacion.RutaEstado)]
    public IActionResult Estado() => Json(new { sesionId = SesionId });
}
