using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Services;
using ClosedXML.Excel;
using System.Text.Json;
using System.Globalization;

namespace PeluqueriaLuciaEspina.Controllers
{
    public class ServicioClienteController : Controller
    {
        private readonly IServicioClienteService _servicioService;

        public ServicioClienteController(IServicioClienteService servicioService)
        {
            _servicioService = servicioService;
        }

        public async Task<IActionResult> Index(DateTime? fecha, int page = 1)
        {
            var servicios = await _servicioService.ObtenerServiciosPorFechaAsync(fecha);

            int pageSize = 50;
            int totalItems = servicios.Count();
            var pagedServicios = servicios
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;

            ViewBag.FechaSeleccionada = fecha;

            return View(pagedServicios);
        }

        [HttpGet]
        public async Task<IActionResult> Create(Sexo? sexo)
        {
            var model = new ServicioClienteViewModel();

            if (sexo.HasValue)
            {
                var sexoEnum = (Sexo)sexo;
                model.SexoSeleccionado = sexoEnum;
            }

            ViewBag.SexoSeleccionado = sexo?.ToString() ?? string.Empty;
            ViewBag.CortesPeloJson = await _servicioService.ObtenerCortesJsonAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServicioClienteViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.SexoSeleccionado = model.SexoSeleccionado.ToString();
                ViewBag.CortesPeloJson = await _servicioService.ObtenerCortesJsonAsync();
                return View(model);
            }

            var nuevoServicio = new ServicioCliente
            {
                ClienteId = model.ClienteId,
                Fecha = model.Fecha,
                OpcionPago = model.OpcionPago,
                Estado = Estado.NoCobrado,
                ServiciosRealizados = model.ServiciosSeleccionados.Select(id => new ServicioGenericoRealizado
                {
                    ServicioGenericoId = id
                }).ToList()
            };

            await _servicioService.CrearServicioAsync(nuevoServicio);
            return RedirectToAction(nameof(Index), new { fecha = DateTime.Now.ToString("yyyy-MM-dd") });
        }

        [HttpGet]

        public async Task<IActionResult> Details(int id)
        {
            var servicio = await _servicioService.ObtenerPorIdAsync(id);

            if (servicio == null)
            {
                return NotFound();
            }

            return View(servicio);
        }

        [HttpGet]
        public async Task<IActionResult> Cobrar(int id)
        {
            var servicio = await _servicioService.ObtenerPorIdAsync(id);
            if (servicio == null || servicio.Estado == Estado.Cobrado)
                return NotFound();

            return View(servicio);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> CobrarConfirmado(int id, decimal? totalEfectivo, decimal? totalTarjeta)
        {
            var servicio = await _servicioService.ObtenerPorIdAsync(id);
            if (servicio == null)
                return NotFound();

            // Rellenar montos para validación en el service
            if (servicio.OpcionPago == OpcionPago.Mixto)
            {
                servicio.TotalEfectivo = totalEfectivo ?? 0;
                servicio.TotalTarjeta = totalTarjeta ?? 0;
            }

            try
            {
                await _servicioService.CobrarServicioAsync(servicio);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Cobrar", new { id });
            }

            //return RedirectToAction("Index");
            return RedirectToAction("Ticket", new { id = servicio.Id });

        }



        public async Task<IActionResult> Delete(int id)
        {
            await _servicioService.EliminarServicioAsync(id);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Ticket(int id)
        {
            var servicio = await _servicioService.ObtenerPorIdAsync(id);
            if (servicio == null)
                return NotFound();

            ViewBag.OpcionPago = servicio.OpcionPago.ToString();
            return View(servicio);
        }

        [HttpGet("exportar-excel")]
        public async Task<IActionResult> ExportarExcel(DateTime? fecha)
        {
            var servicios = await _servicioService.ObtenerServiciosPorFechaAsync(fecha);

            if (!servicios.Any())
            {
                TempData["ErrorExportacion"] = "❌ No hay servicios para exportar.";
                return RedirectToAction("Index", "ServicioCliente", new { fecha = fecha?.ToString("yyyy-MM-dd") });
            }

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Servicios");

            // Cabeceras
            string[] cabeceras = { "Cliente", "Servicios Realizados", "Precio Total (€)", "Estado", "Fecha", "Forma de Pago" };
            for (int i = 0; i < cabeceras.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = cabeceras[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            int row = 2;
            foreach (var s in servicios)
            {
                var nombres = s.ServiciosRealizados?.Any(sr => sr.ServicioGenerico != null) == true
                    ? string.Join(", ", s.ServiciosRealizados
                                            .Where(sr => sr.ServicioGenerico != null)
                                            .Select(sr => sr.ServicioGenerico.Nombre))
                    : "—";

                var total = s.ServiciosRealizados?.Where(sr => sr.ServicioGenerico != null)
                                  .Sum(sr => sr.ServicioGenerico.Precio) ?? 0;

                worksheet.Cell(row, 1).Value = $"{s.Cliente?.Nombre} {s.Cliente?.Apellido}".Trim();
                worksheet.Cell(row, 2).Value = nombres;
                worksheet.Cell(row, 3).Value = total;
                worksheet.Cell(row, 4).Value = s.Estado.ToString();
                worksheet.Cell(row, 5).Value = s.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                worksheet.Cell(row, 6).Value = s.OpcionPago.ToString();
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            var nombreArchivo = $"Servicios_{DateTime.Now:yyyy-MM-dd}.xlsx";

            return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        nombreArchivo);
        }


    }
}