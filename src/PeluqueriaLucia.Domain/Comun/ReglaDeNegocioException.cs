namespace PeluqueriaLucia.Domain.Comun;

/// <summary>
/// Una operación incumple una regla del negocio (cobrar dos veces, un pago mixto que no cuadra…).
/// El mensaje está pensado para enseñarse tal cual en pantalla.
/// </summary>
public sealed class ReglaDeNegocioException(string mensaje, string? campo = null) : Exception(mensaje)
{
    /// <summary>Campo del formulario al que se refiere el error, si es de uno concreto.</summary>
    public string? Campo { get; } = campo;
}
