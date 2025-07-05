using Microsoft.EntityFrameworkCore;
using PeluqueriaLuciaEspina.Models;
using System.Linq.Expressions;

namespace PeluqueriaLuciaEspina.Repo
{
    public interface IGenericRepository<T> where T : class
    {
        IQueryable<T> GetAll();
        Task<List<T>> GetAllListAsync();
        Task<T?> GetByIdAsync(int id);
        Task AddAsync(T entity);
        Task UpdateAsync(T entity);
        Task DeleteAsync(T entity);
        Task SaveChangesAsync();
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);
        IQueryable<T> Query();

    }

    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly AppDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(AppDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public IQueryable<T> GetAll() => _dbSet.AsQueryable();

        public async Task<List<T>> GetAllListAsync() => await _dbSet.ToListAsync();

        public async Task<T?> GetByIdAsync(int id) => await _dbSet.FindAsync(id);

        public async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }
        public IQueryable<T> Query()
        {
            return _dbSet.AsQueryable();
        }

    }
}
