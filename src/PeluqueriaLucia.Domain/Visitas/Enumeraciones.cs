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

/// <summary>Cómo sale un producto en una visita. Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum UsoProducto
{
    /// <summary>Se vende entero: se cobra a su precio y sale del stock al cobrar.</summary>
    Venta = 0,

    /// <summary>Usado en el servicio y terminado: va incluido en él (0,00 €) y sale del stock al cobrar.</summary>
    ServicioTerminado = 1,

    /// <summary>Usado en el servicio y sin terminar: va incluido en él (0,00 €) y sigue en el stock.</summary>
    ServicioEnUso = 2,
}
