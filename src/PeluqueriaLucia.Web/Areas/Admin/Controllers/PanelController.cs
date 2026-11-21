using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Gastos;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>Portada del panel: las cifras de hoy (con los gastos), los cobros y gastos del mes y los avisos.</summary>
public sealed class PanelController(VisitasService visitas, GastosService gastos, ProductosService productos, MonitorPeticiones monitor) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // El día del salón, aunque el servidor esté en otra zona
        var hoy = DateOnly.FromDateTime(gastos.Ahora);
        var dia = FiltroVisitas.Dia(hoy);
        var mes = FiltroVisitas.Mes(hoy.Year, hoy.Month);
        return View(new PanelModelo(hoy, await visitas.ResumirAsync(dia, ct), monitor.Resumen(), await productos.PorReponerAsync(5, ct),
            await gastos.ResumirAsync(dia, ct), await visitas.ResumirAsync(mes, ct), await gastos.ResumirAsync(mes, ct),
            await gastos.TotalesPorTipoAsync(mes, ct)));
    }
}
