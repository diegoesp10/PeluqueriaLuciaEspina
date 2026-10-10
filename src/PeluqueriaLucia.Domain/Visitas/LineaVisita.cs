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

    internal static LineaVisita Desde(Servicio servicio) =>
        new() { ServicioId = servicio.Id, Nombre = servicio.Nombre, Precio = servicio.Precio };
}
