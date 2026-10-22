using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Application.Abstracciones;

public interface IClienteRepositorio
{
    Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Busca por nombre, apellido, teléfono o email; ordena por nombre.</summary>
    Task<Pagina<Cliente>> BuscarAsync(string? texto, Sexo? sexo, int pagina, int tamano, CancellationToken ct = default);

    Task<bool> ExisteEmailAsync(string email, int? excluirId, CancellationToken ct = default);

    Task<bool> ExisteTelefonoAsync(string telefono, int? excluirId, CancellationToken ct = default);

    Task<bool> TieneVisitasAsync(int id, CancellationToken ct = default);

    Task<List<Cliente>> BuscarPorContactoAsync(string telefono, string correo, CancellationToken ct = default);

    Task<bool> TieneCitasAsync(int id, CancellationToken ct = default);

    void Agregar(Cliente cliente);

    void Eliminar(Cliente cliente);
}

public interface IServicioRepositorio
{
    Task<Servicio?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<Servicio>> ListarAsync(Sexo? sexo, bool incluirArchivados, CancellationToken ct = default);

    /// <summary>Como <see cref="ListarAsync"/>, filtrando en SQL por palabras (LIKE) en el nombre y la descripción pública.</summary>
    Task<IReadOnlyList<Servicio>> BuscarAsync(string? texto, Sexo? sexo, bool incluirArchivados, CancellationToken ct = default);

    Task<IReadOnlyList<Servicio>> ObtenerVariosAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    Task<bool> TieneVisitasAsync(int id, CancellationToken ct = default);

    /// <summary>Las citas vivas del servicio que terminan después de <paramref name="desde"/> pasan a tener (o no) dualidad.
    /// Se guardan con el resto de cambios.</summary>
    Task CambiarDualidadCitasAsync(int servicioId, bool dualidad, DateTime desde, CancellationToken ct = default);

    void Agregar(Servicio servicio);

    void Eliminar(Servicio servicio);
}

public interface IVisitaRepositorio
{
    /// <summary>Visita con su cliente y sus servicios.</summary>
    Task<Visita?> ObtenerAsync(int id, CancellationToken ct = default);

    Task<Pagina<Visita>> ListarAsync(FiltroVisitas filtro, int pagina, int tamano, CancellationToken ct = default);

    /// <summary>Todas las visitas del filtro, para exportar. Sin paginar.</summary>
    Task<IReadOnlyList<Visita>> ListarTodasAsync(FiltroVisitas filtro, CancellationToken ct = default);

    /// <summary>Totales calculados en la base de datos, sin cargar las visitas.</summary>
    Task<ResumenVisitas> ResumirAsync(FiltroVisitas filtro, CancellationToken ct = default);

    void Agregar(Visita visita);

    void Eliminar(Visita visita);
}

/// <summary>Confirma de una vez todos los cambios hechos con los repositorios en la misma petición.</summary>
public interface IUnidadDeTrabajo
{
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
