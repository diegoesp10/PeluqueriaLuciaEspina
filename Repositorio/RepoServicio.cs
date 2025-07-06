using Microsoft.EntityFrameworkCore;
using PeluqueriaLuciaEspina.Models;
using PeluqueriaLuciaEspina.Repo;

namespace PeluqueriaLuciaEspina.RepoServicio
{
    public interface IServicioGenericoRepository : IGenericRepository<ServicioGenerico>
    {
        Task<ServicioGenerico?> GetByIdWithRealizadosAsync(int id);
    }

    public class ServicioGenericoRepository : GenericRepository<ServicioGenerico>, IServicioGenericoRepository
    {
        private readonly AppDbContext _context;

        public ServicioGenericoRepository(AppDbContext context) : base(context)
        {
            _context = context;
        }

        public async Task<ServicioGenerico?> GetByIdWithRealizadosAsync(int id)
        {
            return await _context.ServiciosGenericos
                                 .Include(s => s.Realizados)
                                 .FirstOrDefaultAsync(s => s.Id == id);
        }
    }
}
