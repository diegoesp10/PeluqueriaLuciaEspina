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

    internal static LineaVisita Desde(Servicio servicio, decimal? precio = null)
    {
        var aplicado = decimal.Round(precio ?? servicio.Precio,2,MidpointRounding.AwayFromZero);
        if (aplicado <= 0 || aplicado > Servicio.PrecioMaximo)
            throw new Comun.ReglaDeNegocioException($"Indica un precio entre 0,01 y 9.999,99 € para «{servicio.Nombre}».", "Servicios");
        return new() { ServicioId = servicio.Id, Nombre = servicio.Nombre, Precio = decimal.Round(aplicado,2,MidpointRounding.AwayFromZero) };
    }
}
