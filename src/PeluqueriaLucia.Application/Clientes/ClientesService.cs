using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Application.Clientes;

public sealed record DatosCliente(string Nombre, string Apellido, string? Email, string? Telefono, Sexo Sexo, string? Apodo = null);

public sealed record ClienteReconocido(int Id, string Nombre, string Apellido, Sexo Sexo, string? Apodo);

public sealed class ClientesService(IClienteRepositorio clientes, IUnidadDeTrabajo unidadDeTrabajo, TimeProvider reloj)
{
    public const int TamanoPagina = 25;

    /// <summary>Resultados del buscador de clientes al registrar una visita.</summary>
    public const int MaximoSugerencias = 20;

    public Task<Pagina<Cliente>> BuscarAsync(string? texto, Sexo? sexo, int pagina, CancellationToken ct = default) =>
        clientes.BuscarAsync(texto, sexo, Pagina<Cliente>.Normalizar(pagina), TamanoPagina, ct);

    public async Task<IReadOnlyList<Cliente>> SugerirAsync(string? texto, CancellationToken ct = default) =>
        (await clientes.BuscarAsync(texto, null, 1, MaximoSugerencias, ct)).Elementos;

    public async Task<ClienteReconocido?> ReconocerAsync(string? telefono, string? email, CancellationToken ct = default)
    {
        var movil = Agenda.AgendaService.NormalizarTelefono(telefono); var correo = Agenda.AgendaService.NormalizarCorreo(email);
        if (movil is null || correo is null) return null;
        var candidatos = await clientes.BuscarPorContactoAsync(movil,correo,ct);
        if (candidatos.Count != 1) return null;
        var c = candidatos[0];
        return Agenda.AgendaService.NormalizarTelefono(c.Telefono) == movil && string.Equals(c.Email?.Trim(),correo,StringComparison.OrdinalIgnoreCase)
            ? new(c.Id,c.Nombre,c.Apellido,c.Sexo,c.Apodo) : null;
    }

    public Task<bool> TieneCitasAsync(int id, CancellationToken ct = default) => clientes.TieneCitasAsync(id,ct);

    public Task<Cliente?> ObtenerAsync(int id, CancellationToken ct = default) => clientes.ObtenerAsync(id, ct);

    public Task<Resultado<int>> CrearAsync(DatosCliente datos, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            if (await BuscarDuplicadoAsync(datos, null, ct) is { } duplicado)
                return duplicado;

            var cliente = Cliente.Crear(datos.Nombre, datos.Apellido, datos.Email, datos.Telefono, datos.Sexo, reloj.GetLocalNow().DateTime);
            cliente.CambiarApodo(datos.Apodo);
            clientes.Agregar(cliente);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(cliente.Id);
        });

    public Task<Resultado<int>> ActualizarAsync(int id, DatosCliente datos, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var cliente = await clientes.ObtenerAsync(id, ct);
            if (cliente is null)
                return Error.NoEncontrado("El cliente");
            if (await BuscarDuplicadoAsync(datos, id, ct) is { } duplicado)
                return duplicado;

            cliente.CambiarApodo(datos.Apodo);
            cliente.Actualizar(datos.Nombre, datos.Apellido, datos.Email, datos.Telefono, datos.Sexo);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(cliente.Id);
        });

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
            return Error.NoEncontrado("El cliente");
        if (await clientes.TieneVisitasAsync(id, ct) || await clientes.TieneCitasAsync(id, ct))
            return new Error($"{cliente.NombreCompleto} tiene visitas o citas registradas y no se puede eliminar.");

        clientes.Eliminar(cliente);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Email y teléfono son opcionales: solo se comprueban si vienen rellenos, y nunca contra el propio cliente.
    /// </summary>
    private async Task<Error?> BuscarDuplicadoAsync(DatosCliente datos, int? excluirId, CancellationToken ct)
    {
        var email = datos.Email?.Trim();
        if (!string.IsNullOrEmpty(email) && await clientes.ExisteEmailAsync(email, excluirId, ct))
            return new Error("Ya hay otro cliente con ese email.", nameof(DatosCliente.Email));

        var telefono = datos.Telefono?.Trim();
        if (!string.IsNullOrEmpty(telefono) && await clientes.ExisteTelefonoAsync(telefono, excluirId, ct))
            return new Error("Ya hay otro cliente con ese teléfono.", nameof(DatosCliente.Telefono));

        return null;
    }
}
