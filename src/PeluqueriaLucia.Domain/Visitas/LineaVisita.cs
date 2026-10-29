using PeluqueriaLucia.Domain.Catalogo;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>
/// Un servicio hecho en una visita. Guarda el nombre y el precio del momento, así que cambiar el catálogo
/// no altera tickets, totales ni exportaciones de días anteriores.
/// </summary>
public sealed class LineaVisita
{
    private LineaVisita()
    {
    }

    public int Id { get; private set; }

    public int VisitaId { get; private set; }

    public int ServicioId { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public decimal Precio { get; private set; }

    internal static LineaVisita Desde(Servicio servicio, decimal? precio = null) =>
        new() { ServicioId = servicio.Id, Nombre = servicio.Nombre, Precio = PrecioValido(precio ?? servicio.Precio, servicio.Nombre) };

    /// <summary>El precio de este servicio en la visita, que puede ser distinto del catálogo. Se cambia hasta cobrarla.</summary>
    internal void CambiarPrecio(decimal precio) => Precio = PrecioValido(precio, Nombre);

    /// <summary>El precio redondeado a céntimos, entre 0,01 y el máximo del catálogo.</summary>
    internal static decimal PrecioValido(decimal precio, string nombre)
    {
        var aplicado = decimal.Round(precio, 2, MidpointRounding.AwayFromZero);
        if (aplicado <= 0 || aplicado > Servicio.PrecioMaximo)
            throw new Comun.ReglaDeNegocioException($"Indica un precio entre 0,01 y 9.999,99 € para «{nombre}».", "Servicios");
        return aplicado;
    }
}
