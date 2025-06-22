using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using System.Linq;
using System.Text;

namespace PeluqueriaLuciaEspina.Controllers
{
    public class ServiciosController : Controller
    {
        private readonly AppDbContext _context;

        public ServiciosController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index(DateTime? fecha, int page = 1)
        {
            var query = _context.Servicios.AsQueryable();

            if (fecha.HasValue)
            {
                DateTime fechaFiltro = fecha.Value.Date;
                query = query.Where(s => s.FechaCreacion.Date == fechaFiltro);
                ViewBag.FechaSeleccionada = fechaFiltro;
            }
            else
            {
                ViewBag.FechaSeleccionada = null;
            }

            int pageSize = 50;
            int totalItems = query.Count();
            var servicios = query
                .OrderByDescending(s => s.FechaCreacion)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.CurrentPage = page;

            return View(servicios);
        }



        public IActionResult Create(Sexo? sexo)
        {
            var cortes = _context.CortesPelo.AsQueryable();

            if (sexo != null)
            {
                cortes = cortes.Where(c => c.Sexo == sexo);
            }

            ViewBag.CortesPelo = cortes.OrderBy(c => c.Nombre).ToList();
            ViewBag.CortesPeloJson = System.Text.Json.JsonSerializer.Serialize(
            cortes.Select(c => new
            {
                value = c.Id,
                text = $"{c.Nombre} ({c.Sexo})",
                sexo = c.Sexo.ToString(),
                nombre = c.Nombre,
                precio = c.PrecioBase.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
        );

            return View();
        }




        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Servicio servicio)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    servicio.Estado = Estado.NoCobrado;
                    servicio.FechaCreacion = DateTime.Now;
                    _context.Servicios.Add(servicio);
                    _context.SaveChanges();
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                ViewBag.ErrorGeneral = "❌ Error al guardar el servicio. Detalles: " + ex.Message;
            }

            // Recuperamos los cortes
            var cortes = _context.CortesPelo.ToList();

            // Detectamos sexo seleccionado para que JS lo use
            if (servicio.CortePeloId.HasValue)
            {
                var corte = _context.CortesPelo.FirstOrDefault(x => x.Id == servicio.CortePeloId);
                if (corte != null)
                {
                    ViewBag.SexoSeleccionado = corte.Sexo.ToString();
                }
            }

            // Si no viene por un corte, pero el usuario seleccionó sexo previamente (por botón)
            if (Request.Form.TryGetValue("sexoSeleccionado", out var sexoForm))
            {
                ViewBag.SexoSeleccionado = sexoForm.ToString();
            }
            ViewBag.CortesPelo = cortes.OrderBy(c => c.Nombre).ToList();
            ViewBag.CortesPeloJson = System.Text.Json.JsonSerializer.Serialize(
            cortes.Select(c => new
            {
                value = c.Id,
                text = $"{c.Nombre} ({c.Sexo})",
                sexo = c.Sexo.ToString(),
                nombre = c.Nombre,
                precio = c.PrecioBase.ToString(System.Globalization.CultureInfo.InvariantCulture)
            }));
            ViewBag.ErrorGeneral = "❌ Error al crear el servicio. Por favor, revisa los datos ingresados.";
            return View(servicio);
        }

        private SelectList ObtenerCortes()
        {
            return new SelectList(_context.CortesPelo.OrderBy(c => c.Nombre), "Id", "Nombre");
        }

        public IActionResult PostCrearModal(int id)
        {
            var servicio = _context.Servicios.Find(id);
            if (servicio == null) return NotFound();

            ViewBag.CortesPelo = new SelectList(_context.CortesPelo.OrderBy(c => c.Nombre), "Id", "Nombre");
            return PartialView("_PostCrearModal", servicio);
        }

        public IActionResult Edit(int id)
        {
            var servicio = _context.Servicios.Find(id);
            ViewBag.CortesPelo = new SelectList(_context.CortesPelo.OrderBy(c => c.Nombre), "Id", "Nombre");
            if (servicio == null) return NotFound();
            return View(servicio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Servicio servicio)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.CortesPelo = new SelectList(_context.CortesPelo.OrderBy(c => c.Nombre), "Id", "Nombre");
                return View(servicio);
            }

            var original = _context.Servicios.Find(servicio.Id);
            if (original == null)
                return NotFound();

            // Solo actualizamos lo editable
            original.Nombre = servicio.Nombre;
            original.Precio = servicio.Precio;

            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }


        // CONTROLADOR
        [HttpGet]
        public IActionResult Cobrar(int id)
        {
            var servicio = _context.Servicios.FirstOrDefault(s => s.Id == id);
            if (servicio == null || servicio.Estado == Estado.Cobrado)
                return NotFound();

            return View(servicio);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CobrarConfirmado(int id, OpcionPago? opcionPago)
        {

            try
            {
                if(opcionPago is null)
                {
                    ViewBag.ErrorCobrando = "❌ Tienes que seleccionar un método de pago.  " ;
                    return View("Cobrar", new { id });
                }
                var servicio = _context.Servicios.FirstOrDefault(s => s.Id == id);
                if (servicio == null || opcionPago == null)
                    return NotFound();

                servicio.Estado = Estado.Cobrado;
                servicio.OpcionPago = opcionPago;
                _context.SaveChanges();

                return RedirectToAction("Ticket", new { id = servicio.Id });
            } catch (Exception ex)
            {
                ViewBag.ErrorCobrando = "❌ Error al cobrar el servicio. Detalles: " + ex.Message;
                return View("Cobrar", new { id });
            }
        }

        public IActionResult Delete(int id)
        {
            var servicio = _context.Servicios.Find(id);
            if (servicio == null) return NotFound();
            _context.Servicios.Remove(servicio);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Ticket(int id)
        {
            var servicio = _context.Servicios
                .FirstOrDefault(s => s.Id == id && s.Estado == Estado.Cobrado);

            if (servicio == null)
                return NotFound();

            return View(servicio);
        }
        [HttpGet]
        [HttpGet]
        public IActionResult ExportarExcel(DateTime? fecha)
        {
            var query = _context.Servicios.AsQueryable();
            if (fecha.HasValue)
            {
                var fechaFiltro = fecha.Value.Date;
                query = query.Where(s => s.FechaCreacion.Date == fechaFiltro);
            }

            var servicios = query.OrderByDescending(s => s.FechaCreacion).ToList();
            if (!servicios.Any())
            {
                TempData["ErrorExportacion"] = "❌ No hay servicios para exportar.";
                return RedirectToAction(nameof(Index));
            }
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Servicios");

            // Cabeceras
            string[] cabeceras = { "Nombre", "Precio (€)", "Estado", "Fecha", "Forma de Pago" };
            for (int i = 0; i < cabeceras.Length; i++)
            {
                var cell = worksheet.Cell(1, i + 1);
                cell.Value = cabeceras[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Datos
            for (int i = 0; i < servicios.Count; i++)
            {
                var s = servicios[i];
                worksheet.Cell(i + 2, 1).Value = s.Nombre;
                worksheet.Cell(i + 2, 2).Value = s.Precio;
                worksheet.Cell(i + 2, 3).Value = s.Estado.ToString();
                worksheet.Cell(i + 2, 4).Value = s.FechaCreacion.ToString("dd/MM/yyyy");
                worksheet.Cell(i + 2, 5).Value = s.OpcionPago?.ToString() ?? "—";
            }

            // Ajustar ancho de columnas automáticamente
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            var nombreArchivo = $"{DateTime.Now:yyyy-MM-dd}.xlsx";

            return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        nombreArchivo);

        }


    }
}
