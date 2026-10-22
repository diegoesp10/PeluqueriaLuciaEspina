using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class PanelController(VisitasService visitas, PeluqueriaLucia.Application.Productos.ProductosService productos, MonitorPeticiones monitor, TimeProvider reloj) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(reloj.GetLocalNow().DateTime);
        var resumen = await visitas.ResumirAsync(FiltroVisitas.Dia(hoy), ct);
        return View(new PanelModelo(hoy, resumen, monitor.Resumen(), await productos.PorReponerAsync(5, ct)));
    }
}
