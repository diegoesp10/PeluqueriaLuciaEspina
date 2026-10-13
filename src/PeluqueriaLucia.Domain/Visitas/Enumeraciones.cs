namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum EstadoVisita
{
    Cobrada = 1,
    Pendiente = 2,

    /// <summary>Valor heredado de la primera versión; no se usa, pero puede existir en datos antiguos.</summary>
    Anulada = 3,
}

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum FormaPago
{
    Tarjeta = 1,
    Efectivo = 2,
    Mixto = 3,
}
