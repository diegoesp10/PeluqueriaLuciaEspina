using Microsoft.AspNetCore.Mvc;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Services;
using PeluqueriaLuciaEspina.Servicios;

namespace PeluqueriaLuciaEspina.Controllers
{
    public class ServicioGenericoController : Controller
    {
        private readonly IServicioGenericoService _servicioGenericoService;
        private readonly IClienteService _clienteService;
        private readonly IServicioClienteService _servicioClienteService;

        public ServicioGenericoController(IServicioGenericoService servicioGenericoService, IClienteService clienteService, IServicioClienteService servicioCliente)
        {
            _servicioGenericoService = servicioGenericoService;
            _clienteService = clienteService;
            _servicioClienteService = servicioCliente;
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
        public async Task<IActionResult> Edit(ServicioGenerico servicio, IFormFile ? imagen)
        {
            if (!ModelState.IsValid) return View(servicio);
            //check if imagen is null or empty
            // pero la funcionalidad se la paso en el servicio, la hago alli 

            await _servicioGenericoService.ActualizarAsync(servicio, imagen);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var servicio = await _servicioGenericoService.GetByIdAsync(id);
            if (servicio == null) return NotFound();

            // Verificar si el servicio tiene servicios realizados asociados
            foreach (var realizado in servicio.Realizados)
            {
                if (realizado.ServicioGenericoId == id)
                {
                    TempData["Error"] = "No puedes eliminar un servicio que ha sido realizado y asociado a un cliente.";
                    return RedirectToAction(nameof(Index));
                }
            }
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
