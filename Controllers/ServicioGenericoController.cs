using Microsoft.AspNetCore.Mvc;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Servicios;

namespace PeluqueriaLuciaEspina.Controllers
{
    public class ServicioGenericoController : Controller
    {
        private readonly IServicioGenericoService _servicioGenericoService;
        private readonly IClienteService _clienteService;

        public ServicioGenericoController(IServicioGenericoService servicioGenericoService, IClienteService clienteService)
        {
            _servicioGenericoService = servicioGenericoService;
            _clienteService = clienteService;
        }

        // GET: /ServicioGenerico
        // que acepte posible parametro sexo en el index 
        [HttpGet]

        public async Task<IActionResult> Index(string sexo)
        {
            Sexo? sexoEnum = null;
            if (!string.IsNullOrEmpty(sexo))
            {
                if (Enum.TryParse<Sexo>(sexo, true, out var parsedSexo))
                {
                    sexoEnum = parsedSexo;
                }
                else
                {
                    ModelState.AddModelError("sexo", "Sexo no válido.");
                }
            }

            ViewBag.SexoSeleccionado = sexoEnum;

            var servicios = await _servicioGenericoService.GetAllAsync(sexoEnum);
            return View(servicios);
        }


        // GET: /ServicioGenerico/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var servicio = await _servicioGenericoService.GetByIdAsync(id);
            if (servicio == null) return NotFound();
            return View(servicio);
        }

        // GET: /ServicioGenerico/Create
        public IActionResult Create()
        {
            return View();
        }


        // POST: /ServicioGenerico/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServicioGenerico servicio, IFormFile? ImagenRuta)
        {
            if (!ModelState.IsValid) return View(servicio);

            await _servicioGenericoService.CrearAsync(servicio, ImagenRuta);
            return RedirectToAction(nameof(Index));
        }

        // GET: /ServicioGenerico/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var servicio = await _servicioGenericoService.GetByIdAsync(id);
            if (servicio == null) return NotFound();

            return View(servicio);
        }

        // POST: /ServicioGenerico/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ServicioGenerico servicio)
        {
            if (!ModelState.IsValid) return View(servicio);

            await _servicioGenericoService.ActualizarAsync(servicio);
            return RedirectToAction(nameof(Index));
        }

        // GET: /ServicioGenerico/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var servicio = await _servicioGenericoService.GetByIdAsync(id);
            if (servicio == null) return NotFound();

            return View(servicio);
        }

        // POST: /ServicioGenerico/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _servicioGenericoService.EliminarAsync(id);
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerServiciosPorSexo(int sexo)
        {
            var sexoEnum = (Sexo)sexo;
            var servicios = await _servicioGenericoService.GetAllAsync(sexoEnum);

            if (servicios == null || !servicios.Any())
                return Json(new { error = "No se encontraron servicios." });

            var resultado = servicios.Select(s => new
            {
                id = s.Id,
                nombre = s.Nombre,
                precio = s.Precio,
                sexo = s.Sexo
            });

            return Json(resultado);
        }





    }
}
