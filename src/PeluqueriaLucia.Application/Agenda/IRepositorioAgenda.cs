using System.Linq.Expressions;
using PeluqueriaLucia.Domain.Agenda;

namespace PeluqueriaLucia.Application.Agenda;

/// <summary>Puerto de persistencia. Las reglas de agenda y comunicaciones pertenecen a la aplicación.</summary>
public interface IRepositorioAgenda
{
    Task<T?> BuscarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class;
    Task<List<T>> ListarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class;
    Task<bool> ExisteAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class;
    Task<int> ContarAsync<T>(Expression<Func<T, bool>> filtro, CancellationToken ct) where T : class;
    void Agregar<T>(T entidad) where T : class;
    void Quitar<T>(T entidad) where T : class;
    Task<T> ExclusivoAsync<T>(Func<Task<T>> accion, CancellationToken ct);
    Task<List<PeluqueriaLucia.Domain.Clientes.Cliente>> ClientesPorContactoAsync(string telefono, string correo, CancellationToken ct);
    Task<List<ReservaCita>> CitasConClientesAsync(Expression<Func<ReservaCita, bool>> filtro, CancellationToken ct);
    Task<DateTime?> UltimaSolicitudAsync(CancellationToken ct);
    Task<IReadOnlyList<MensajeCita>> UltimosMensajesAsync(CancellationToken ct);
}
