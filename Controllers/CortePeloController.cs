using Microsoft.AspNetCore.Mvc;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using System.Linq;

namespace PeluqueriaLuciaTspina.Controllers
{
    public class CortePeloController : Controller
    {
        private readonly AppDbContext _context;

        public CortePeloController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index(Sexo? sexo)
        {
            var cortes = _context.CortesPelo.AsQueryable();

            if (sexo.HasValue)
            {
                cortes = cortes.Where(c => c.Sexo == sexo);
                ViewBag.SexoSeleccionado = sexo;
            }
            return View(cortes.ToList());
        }


        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CortePelo cortePelo, IFormFile? Imagen)
        {
            if (ModelState.IsValid)
            {
                if (Imagen != null && Imagen.Length > 0)
                {
                    string rutaCarpeta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "cortesDePelo");
                    if (!Directory.Exists(rutaCarpeta))
                        Directory.CreateDirectory(rutaCarpeta);

                    string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(Imagen.FileName);
                    string rutaArchivo = Path.Combine(rutaCarpeta, nombreUnico);

                    using (var stream = new FileStream(rutaArchivo, FileMode.Create))
                    {
                        await Imagen.CopyToAsync(stream);
                    }

                    cortePelo.ImagenRuta = "/cortesDePelo/" + nombreUnico;
                }

                _context.Add(cortePelo);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(cortePelo);
        }

        public IActionResult Edit(int id)
        {
            var corte = _context.CortesPelo.Find(id);
            if (corte == null) return NotFound();
            return View(corte);
        }

        [HttpPost]
        public IActionResult Edit(CortePelo corte)
        {
            if (ModelState.IsValid)
            {
                var existente = _context.CortesPelo.Find(corte.Id);
                if (existente == null) return NotFound();

                // Mantener la imagen anterior
                corte.ImagenRuta = existente.ImagenRuta;

                _context.Entry(existente).CurrentValues.SetValues(corte);
                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            return View(corte);
        }


        public IActionResult Delete(int id)
        {
            var corte = _context.CortesPelo.Find(id);
            if (corte == null) return NotFound();

            // Eliminar la imagen física si existe
            if (!string.IsNullOrEmpty(corte.ImagenRuta))
            {
                string rutaArchivo = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", corte.ImagenRuta.TrimStart('/'));
                if (System.IO.File.Exists(rutaArchivo))
                {
                    System.IO.File.Delete(rutaArchivo);
                }
            }

            _context.CortesPelo.Remove(corte);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

    }
}
