using Microsoft.EntityFrameworkCore;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;
using PeluqueriaLuciaEspina.RepoServicio;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Servicios
{
    public interface IServicioGenericoService
    {
        Task<IEnumerable<ServicioGenerico>> GetAllAsync(Sexo ? Sexo);
        Task<ServicioGenerico?> GetByIdAsync(int id);
        Task CrearAsync(ServicioGenerico servicio, IFormFile? imagen);
        Task ActualizarAsync(ServicioGenerico servicio, IFormFile ? imagen);
        Task EliminarAsync(int id);
    }
    public class ServicioGenericoService : IServicioGenericoService
    {
        private readonly IGenericRepository<ServicioGenerico> _repo;
        private readonly IWebHostEnvironment _env;
        private readonly IServicioGenericoRepository _repoServicio;


        public ServicioGenericoService(IGenericRepository<ServicioGenerico> repo, IWebHostEnvironment env, IServicioGenericoRepository repoGenerico)
        {
            _repo = repo;
            _env = env;
            _repoServicio = repoGenerico;
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

            var servicio = await _repoServicio.GetByIdWithRealizadosAsync(id);
            if (servicio == null) return null;

            if (servicio.Realizados == null)
            {
                servicio.Realizados = new List<ServicioGenericoRealizado>();
            }
            return servicio;
           
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

        public async Task ActualizarAsync(ServicioGenerico servicio, IFormFile? nuevaImagen = null)
        {
            var entidad = await _repo.GetByIdAsync(servicio.Id);
            if (entidad is null) return;

            entidad.Nombre = servicio.Nombre;
            entidad.Precio = servicio.Precio;
            entidad.Sexo = servicio.Sexo;

            if (nuevaImagen != null && nuevaImagen.Length > 0)
            {
                // Eliminar imagen anterior si existe
                if (!string.IsNullOrEmpty(entidad.ImagenRuta))
                {
                    string rutaAntigua = Path.Combine(_env.WebRootPath, entidad.ImagenRuta.TrimStart('/'));
                    if (File.Exists(rutaAntigua)) File.Delete(rutaAntigua);
                }

                // Guardar nueva imagen
                string rutaCarpeta = Path.Combine(_env.WebRootPath, "imagenes");
                Directory.CreateDirectory(rutaCarpeta);

                string nombreUnico = Guid.NewGuid().ToString() + Path.GetExtension(nuevaImagen.FileName);
                string rutaFinal = Path.Combine(rutaCarpeta, nombreUnico);

                using var stream = new FileStream(rutaFinal, FileMode.Create);
                await nuevaImagen.CopyToAsync(stream);

                entidad.ImagenRuta = "/imagenes/" + nombreUnico;
            }

            await _repo.UpdateAsync(entidad);
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
