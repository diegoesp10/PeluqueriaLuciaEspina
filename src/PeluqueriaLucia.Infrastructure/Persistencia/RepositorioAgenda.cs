using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Infrastructure.Persistencia;

internal sealed class RepositorioAgenda(PeluqueriaDbContext db) : IRepositorioAgenda
{
    public Task<T?> BuscarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class => db.Set<T>().SingleOrDefaultAsync(filtro, ct);
    public Task<List<T>> ListarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class => db.Set<T>().Where(filtro).ToListAsync(ct);
    public Task<bool> ExisteAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class => db.Set<T>().AnyAsync(filtro, ct);
    public Task<int> ContarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class => db.Set<T>().CountAsync(filtro, ct);
    public void Agregar<T>(T entidad) where T : class => db.Set<T>().Add(entidad);
    public void Quitar<T>(T entidad) where T : class => db.Set<T>().Remove(entidad);
    public Task<List<PeluqueriaLucia.Domain.Clientes.Cliente>> ClientesPorContactoAsync(string telefono, string correo, CancellationToken ct)
    {
        return ConsultaContactos.Candidatos(db.Clientes,telefono,correo).ToListAsync(ct);
    }
    public Task<List<ReservaCita>> CitasConClientesAsync(System.Linq.Expressions.Expression<Func<ReservaCita, bool>> filtro, CancellationToken ct) =>
        db.ReservasCitas.Where(filtro).Include(x => x.Cliente).ThenInclude(x => x!.Faltas).AsSplitQuery().ToListAsync(ct);
    public Task<DateTime?> UltimaSolicitudAsync(CancellationToken ct) => db.ReservasCitas.Select(x => (DateTime?)x.CreadaUtc).MaxAsync(ct);
    public async Task<IReadOnlyList<MensajeCita>> UltimosMensajesAsync(CancellationToken ct) =>
        await db.MensajesCitas.AsNoTracking().OrderByDescending(x => x.CreadoUtc).Take(100).ToListAsync(ct);
    public Task<T> ExclusivoAsync<T>(Func<Task<T>> accion, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'LuciaAgenda', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @r<0 THROW 51000, 'Agenda ocupada temporalmente', 1;", ct);
            var resultado = await accion();
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return resultado;
        });
}
