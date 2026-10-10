using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Application.Comun;

/// <summary>Motivo por el que una operación no se ha hecho, listo para mostrarse.</summary>
/// <param name="Campo">Campo del formulario afectado, o <c>null</c> si el error es general.</param>
public sealed record Error(string Mensaje, string? Campo = null)
{
    public static Error NoEncontrado(string que) => new($"{que} no existe o ya se ha eliminado.");
}

/// <summary>Resultado de una operación que puede fallar por una regla del negocio.</summary>
public class Resultado
{
    protected Resultado(Error? error) => Error = error;

    public Error? Error { get; }

    public bool Exito => Error is null;

    public static Resultado Ok() => new(null);

    public static Resultado Fallo(Error error) => new(error);

    public static Resultado<T> Ok<T>(T valor) => new(valor, null);

    public static implicit operator Resultado(Error error) => Fallo(error);

    /// <summary>Ejecuta una acción y convierte las reglas de negocio incumplidas en un resultado fallido.</summary>
    internal static async Task<Resultado> ProtegerAsync(Func<Task> accion)
    {
        try
        {
            await accion();
            return Ok();
        }
        catch (ReglaDeNegocioException ex)
        {
            return Fallo(new Error(ex.Message, ex.Campo));
        }
    }

    internal static async Task<Resultado<T>> ProtegerAsync<T>(Func<Task<Resultado<T>>> accion)
    {
        try
        {
            return await accion();
        }
        catch (ReglaDeNegocioException ex)
        {
            return new Error(ex.Message, ex.Campo);
        }
    }
}

public sealed class Resultado<T> : Resultado
{
    internal Resultado(T? valor, Error? error) : base(error) => Valor = valor;

    /// <summary>Solo tiene sentido si <see cref="Resultado.Exito"/> es verdadero.</summary>
    public T? Valor { get; }

    public static implicit operator Resultado<T>(Error error) => new(default, error);
}
