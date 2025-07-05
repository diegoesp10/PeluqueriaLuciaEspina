using Microsoft.AspNetCore.Mvc;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Services;
using PeluqueriaLuciaEspina.Servicios;

namespace PeluqueriaLuciaEspina.Controllers
{
    public class ClientesController : Controller
    {
        private readonly IClienteService _clienteService;
        private readonly IServicioClienteService _servicioClienteService;

        public ClientesController(IClienteService clienteService, IServicioClienteService servicioClienteService)
        {
            _clienteService = clienteService;
            _servicioClienteService = servicioClienteService;
        }

        // GET: Clientes
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var clientes = await _clienteService.ObtenerTodosAsync(null);
            return View(clientes);
        }
        // GET: Clientes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cliente cliente)
        {
            var yaExisteMail = await _clienteService.AnyAsync(c => c.Email == cliente.Email);

            if (yaExisteMail)
                ModelState.AddModelError("Email", "Ya existe un cliente con ese email.");
            var yaExisteTelefono = await _clienteService.AnyAsync(c => c.Telefono == cliente.Telefono);

            if (yaExisteTelefono)
                ModelState.AddModelError("Telefono", "Ya existe un cliente con ese teléfono.");

            if (!ModelState.IsValid)
                return View(cliente);

            await _clienteService.CrearAsync(cliente);
            return RedirectToAction(nameof(Index));
        }


        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var cliente = await _clienteService.ObtenerPorIdAsync(id);
            if (cliente == null) return NotFound();

            return View(cliente);
        }

        // POST: Clientes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Cliente cliente)
        {
            var yaExisteMail = await _clienteService.AnyAsync(c => c.Email == cliente.Email);

            if (yaExisteMail)
                ModelState.AddModelError("Email", "Ya existe un cliente con ese email.");
            var yaExisteTelefono = await _clienteService.AnyAsync(c => c.Telefono == cliente.Telefono);

            if (yaExisteTelefono)
                ModelState.AddModelError("Telefono", "Ya existe un cliente con ese teléfono.");

            if (!ModelState.IsValid) return View(cliente);

            await _clienteService.ActualizarAsync(cliente);
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var cliente = await _clienteService.ObtenerPorIdAsync(id);
            if (cliente == null) return NotFound();
            var tieneServicios = await _servicioClienteService.AnyClienteAsync(c => c.ClienteId == id);
            if (tieneServicios)
            { 
                TempData["Error"] = "No se puede eliminar un cliente que tiene servicios asociados.";
                return RedirectToAction(nameof(Index));
            }
            await _clienteService.EliminarAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerClientesPorSexo(int sexo)
        {
            var sexoEnum = (Sexo)sexo;
            var clientesFiltrados = await _clienteService.ObtenerTodosAsync(sexoEnum);
            return Json(clientesFiltrados);
        }


    }
}
