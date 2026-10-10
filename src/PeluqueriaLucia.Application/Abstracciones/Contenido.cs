using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Application.Comun;

namespace PeluqueriaLucia.Application.Abstracciones;

public interface IContenidoRepositorio
{
    Task<IReadOnlyList<CategoriaTratamiento>> CategoriasAsync(CancellationToken ct);
    Task<CategoriaTratamiento?> CategoriaAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<TratamientoPublico>> TratamientosAsync(bool soloPublicados, CancellationToken ct);
    Task<TratamientoPublico?> TratamientoAsync(int id, CancellationToken ct);
    void Agregar(CategoriaTratamiento categoria);
    void Agregar(TratamientoPublico tratamiento);
    void Eliminar(CategoriaTratamiento categoria);
    void Eliminar(TratamientoPublico tratamiento);
    Task<PermisoResena?> PermisoVisitaAsync(int visitaId, CancellationToken ct);
    Task<PermisoResena?> PermisoCodigoAsync(string hash, CancellationToken ct);
    Task<PermisoResena> GuardarPermisoTicketAsync(PermisoResena permiso, CancellationToken ct);
    void Agregar(Resena resena);
    Task<Resena?> ResenaAsync(Guid id, CancellationToken ct);
    Task<Pagina<Resena>> ResenasAsync(EstadoResena? estado, int pagina, CancellationToken ct);
    Task<(int Total, double Media)> ValoracionAsync(CancellationToken ct);
}

public interface IFotosResenas
{
    Task<Resultado<string>> GuardarAsync(Stream contenido, CancellationToken ct);
    string? RutaFisica(string nombre);
    void Eliminar(string nombre);
}
