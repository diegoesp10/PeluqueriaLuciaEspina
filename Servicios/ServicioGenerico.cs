using Microsoft.EntityFrameworkCore;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Servicios
{
    public interface IServicioGenericoService
    {
        Task<IEnumerable<ServicioGenerico>> GetAllAsync(Sexo ? Sexo);
        Task<ServicioGenerico?> GetByIdAsync(int id);
        Task CrearAsync(ServicioGenerico servicio, IFormFile? imagen);
        Task ActualizarAsync(ServicioGenerico servicio);
        Task EliminarAsync(int id);
    }
    public class ServicioGenericoService : IServicioGenericoService
    {
        private readonly IGenericRepository<ServicioGenerico> _repo;
        private readonly IWebHostEnvironment _env;

        public ServicioGenericoService(IGenericRepository<ServicioGenerico> repo, IWebHostEnvironment env)
        {
            _repo = repo;
            _env = env;
        }
        public async Task<IEnumerable<ServicioGenerico>> GetAllAsync(Sexo? sexo)
        {
            var query = _repo.GetAll();
            if (sexo.HasValue)
            {
                query = query.AsNoTracking().Where(s => s.Sexo == sexo.Value);
            }
            return await query.ToListAsync();
        }
        public async Task<ServicioGenerico?> GetByIdAsync(int id)
        {
            return await _repo.GetByIdAsync(id);
        }

        public async Task CrearAsync(ServicioGenerico servicio, IFormFile? imagen)
        {
            if (imagen != null && imagen.Length > 0)
            {
                string rutaCarpeta = Path.Combine(_env.WebRootPath, "imagenes");
                Directory.CreateDirectory(rutaCarpeta);

                string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(imagen.FileName);
                string rutaFinal = Path.Combine(rutaCarpeta, nombreUnico);

                using var stream = new FileStream(rutaFinal, FileMode.Create);
                await imagen.CopyToAsync(stream);

                servicio.ImagenRuta = "/imagenes/" + nombreUnico;
            }

            await _repo.AddAsync(servicio);
            await _repo.SaveChangesAsync();
        }

        public async Task ActualizarAsync(ServicioGenerico servicio)
        {
            await _repo.UpdateAsync(servicio);
            await _repo.SaveChangesAsync();
        }

        public async Task EliminarAsync(int id)
        {
            var entidad = await _repo.GetByIdAsync(id);
            if (entidad is null) return;

            if (!string.IsNullOrEmpty(entidad.ImagenRuta))
            {
                string ruta = Path.Combine(_env.WebRootPath, entidad.ImagenRuta.TrimStart('/'));
                if (File.Exists(ruta)) File.Delete(ruta);
            }

            await _repo.DeleteAsync(entidad);
            await _repo.SaveChangesAsync();
        }
    }



}
