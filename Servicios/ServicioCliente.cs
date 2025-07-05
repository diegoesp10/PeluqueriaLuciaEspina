using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using PelqueriaLuciaEspina.Enums;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;
using System.Linq.Expressions;
using System.Text.Json;

namespace PeluqueriaLuciaEspina.Services
{
    public interface IServicioClienteService
    {
        Task<IEnumerable<ServicioCliente>> ObtenerServiciosPorFechaAsync(DateTime? fecha);
        Task<ServicioCliente?> ObtenerPorIdAsync(int id);
        Task CrearServicioAsync(ServicioCliente servicio);
        Task EditarServicioAsync(ServicioCliente servicio);
        Task CobrarServicioAsync(ServicioCliente servicio);
        Task EliminarServicioAsync(int id);
        Task<List<ServicioGenerico>> ObtenerCortesAsync();
        Task<string> ObtenerCortesJsonAsync();
        Task<bool> AnyClienteAsync(Expression<Func<ServicioCliente, bool>> predicate);
    }

    public class ServicioClienteService : IServicioClienteService
    {
        private readonly IGenericRepository<ServicioCliente> _servicioRepo;
        private readonly IGenericRepository<ServicioGenerico> _cortesRepo;
        private readonly IGenericRepository<ServicioGenericoRealizado> _servicioRealizadoRepo;

        public ServicioClienteService(
            IGenericRepository<ServicioCliente> servicioRepo,
            IGenericRepository<ServicioGenerico> cortesRepo,
            IGenericRepository<ServicioGenericoRealizado> servicioRealizadoRepo)
        {
            _servicioRepo = servicioRepo;
            _cortesRepo = cortesRepo;
            _servicioRealizadoRepo = servicioRealizadoRepo;
        }

        public async Task<IEnumerable<ServicioCliente>> ObtenerServiciosPorFechaAsync(DateTime? fecha)
        {
            var servicios = _servicioRepo.GetAll()
                .Include(s => s.Cliente)
                .Include(s => s.ServiciosRealizados)
                    .ThenInclude(sr => sr.ServicioGenerico);

            return fecha.HasValue
                ? await servicios.Where(s => s.Fecha.Date == fecha.Value.Date)
                                 .OrderByDescending(s => s.Fecha)
                                 .ToListAsync()
                : await servicios.OrderByDescending(s => s.Fecha).ToListAsync();
        }


        public async Task<ServicioCliente?> ObtenerPorIdAsync(int id)
        {
            return await _servicioRepo.Query()
                .Include(s => s.Cliente)
                .Include(s => s.ServiciosRealizados)
                    .ThenInclude(sr => sr.ServicioGenerico)
                .FirstOrDefaultAsync(s => s.Id == id);
        }


        public async Task CrearServicioAsync(ServicioCliente servicio)
        {
            // Ya no necesitas asignar ServicioGenerico con new
            await _servicioRepo.AddAsync(servicio);
            await _servicioRepo.SaveChangesAsync();
        }



        public async Task EditarServicioAsync(ServicioCliente servicio)
        {
            await _servicioRepo.UpdateAsync(servicio);
            await _servicioRepo.SaveChangesAsync();
        }

        public async Task CobrarServicioAsync(ServicioCliente servicio)
        {
            decimal total = servicio.ServiciosRealizados
             .Where(sr => sr.ServicioGenerico != null)
             .Sum(sr => sr.ServicioGenerico!.Precio);
            if (servicio.OpcionPago == OpcionPago.Mixto)
            {
                decimal efectivo = servicio.TotalEfectivo ?? 0;
                decimal tarjeta = servicio.TotalTarjeta ?? 0;
                decimal suma = efectivo + tarjeta;
                //comprueba la nulidad 
                if (suma <= 0)
                {
                    throw new InvalidOperationException("El total de efectivo y tarjeta debe ser mayor que cero.");
                }
                // Comprueba que la suma de efectivo y tarjeta coincide con el total del servicio
                if (servicio.ServiciosRealizados.Count == 0)
                {
                    throw new InvalidOperationException("No hay servicios realizados para cobrar.");
                }


                if (Math.Round(suma, 2) != Math.Round(total, 2))
                {
                    throw new InvalidOperationException($"La suma de efectivo y tarjeta ({suma:F2} €) no coincide con el total del servicio ({total:F2} €).");
                }
            }
            else if(servicio.OpcionPago == OpcionPago.Tarjeta)
            {
                servicio.TotalTarjeta = total;
                servicio.TotalEfectivo = 0;
            }
            else 
            {
                servicio.TotalEfectivo = total;
                servicio.TotalTarjeta = 0;
            }
            servicio.Estado = Estado.Cobrado;
            await _servicioRepo.UpdateAsync(servicio);
            await _servicioRepo.SaveChangesAsync();
        }


        public async Task EliminarServicioAsync(int id)
        {
            var servicio = await _servicioRepo.GetAll()
                .Where(s => s.Id == id)
                .Select(s => new ServicioCliente
                {
                    Id = s.Id,
                    Estado = s.Estado,
                    ServiciosRealizados = s.ServiciosRealizados.ToList()
                })
                .FirstOrDefaultAsync();

            if (servicio is null)
                return;

            if (servicio.Estado == Estado.Cobrado)
                throw new InvalidOperationException("No se puede eliminar un servicio que ya ha sido cobrado.");

            // Eliminar servicios relacionados
            foreach (var realizado in servicio.ServiciosRealizados)
            {
                await _servicioRealizadoRepo.DeleteAsync(realizado);
            }

            // Eliminar servicio principal
            await _servicioRepo.DeleteAsync(servicio);

            // Guardar cambios
            await _servicioRealizadoRepo.SaveChangesAsync();
            await _servicioRepo.SaveChangesAsync();
        }
        public async Task<List<ServicioGenerico>> ObtenerCortesAsync()
        {
            var cortes = await _cortesRepo.GetAllListAsync();
            return cortes.OrderBy(c => c.Nombre).ToList();
        }

        public async Task<string> ObtenerCortesJsonAsync()
        {
            var cortes = await ObtenerCortesAsync();

            var json = JsonSerializer.Serialize(
                cortes.Select(c => new
                {
                    value = c.Id,
                    text = $"{c.Nombre} ({c.Sexo})",
                    sexo = c.Sexo.ToString(),
                    nombre = c.Nombre,
                    precio = c.Precio.ToString(System.Globalization.CultureInfo.InvariantCulture)
                })
            );

            return json;
        }
        public async Task<bool> AnyClienteAsync(Expression<Func<ServicioCliente, bool>> predicate)
        {
            return await _servicioRepo.AnyAsync(predicate);
        }

    }
}
