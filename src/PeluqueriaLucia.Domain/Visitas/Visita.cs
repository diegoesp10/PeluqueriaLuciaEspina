using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Visitas;

/// <summary>Una visita de un cliente: los servicios que se le hicieron y cómo se cobraron.</summary>
public sealed class Visita
{
    private readonly List<LineaVisita> _lineas = [];

    private Visita()
    {
    }

    public int Id { get; private set; }

    public DateTime Fecha { get; private set; }

    public int ClienteId { get; private set; }

    public Cliente? Cliente { get; private set; }

    /// <summary>Forma de pago prevista al registrar la visita; al cobrar queda la definitiva.</summary>
    public FormaPago FormaPago { get; private set; }

    public EstadoVisita Estado { get; private set; }

    public decimal? TotalEfectivo { get; private set; }

    public decimal? TotalTarjeta { get; private set; }

    public IReadOnlyList<LineaVisita> Lineas => _lineas;

    public decimal Total => _lineas.Sum(l => l.Precio);

    public bool EstaCobrada => Estado == EstadoVisita.Cobrada;

    public static Visita Registrar(Cliente cliente, DateTime fecha, FormaPago formaPago, IReadOnlyCollection<Servicio> servicios, IReadOnlyDictionary<int,decimal>? precios = null)
    {
        ArgumentNullException.ThrowIfNull(cliente);
        if (servicios.Count == 0)
            throw new ReglaDeNegocioException("Elige al menos un servicio.", "Servicios");
        if (servicios.FirstOrDefault(s => !s.Activo) is { } archivado)
            throw new ReglaDeNegocioException($"«{archivado.Nombre}» está archivado y ya no se ofrece.", "Servicios");
        ComprobarFormaPago(formaPago);

        var visita = new Visita
        {
            Cliente = cliente,
            ClienteId = cliente.Id,
            Fecha = fecha,
            FormaPago = formaPago,
            Estado = EstadoVisita.Pendiente,
        };
        visita._lineas.AddRange(servicios.Select(s => LineaVisita.Desde(s, precios is not null && precios.TryGetValue(s.Id,out var precio) ? precio : null)));
        return visita;
    }

    /// <summary>
    /// Cobra la visita. En pago mixto, efectivo y tarjeta tienen que sumar exactamente el total;
    /// en los demás casos el importe completo va a la forma elegida.
    /// </summary>
    public void Cobrar(FormaPago formaPago, decimal? efectivo, decimal? tarjeta)
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException("Esta visita ya estaba cobrada.");
        if (_lineas.Count == 0)
            throw new ReglaDeNegocioException("La visita no tiene servicios que cobrar.");
        ComprobarFormaPago(formaPago);

        var total = Total;
        switch (formaPago)
        {
            case FormaPago.Mixto:
                var enEfectivo = Redondear(efectivo ?? 0);
                var conTarjeta = Redondear(tarjeta ?? 0);
                if (enEfectivo < 0 || conTarjeta < 0)
                    throw new ReglaDeNegocioException("Los importes no pueden ser negativos.");
                if (enEfectivo == 0 || conTarjeta == 0)
                    throw new ReglaDeNegocioException("En un pago mixto hay que poner una parte en efectivo y otra con tarjeta.");
                if (enEfectivo + conTarjeta != total)
                    throw new ReglaDeNegocioException(
                        $"Efectivo y tarjeta suman {enEfectivo + conTarjeta:0.00} € y el total es {total:0.00} €.");
                TotalEfectivo = enEfectivo;
                TotalTarjeta = conTarjeta;
                break;
            case FormaPago.Tarjeta:
                TotalEfectivo = 0;
                TotalTarjeta = total;
                break;
            default:
                TotalEfectivo = total;
                TotalTarjeta = 0;
                break;
        }

        FormaPago = formaPago;
        Estado = EstadoVisita.Cobrada;
    }

    /// <summary>Una visita cobrada forma parte de la caja y no se puede borrar.</summary>
    public void ComprobarQueSePuedeEliminar()
    {
        if (EstaCobrada)
            throw new ReglaDeNegocioException("No se puede eliminar una visita que ya está cobrada.");
    }

    private static void ComprobarFormaPago(FormaPago formaPago)
    {
        if (!Enum.IsDefined(formaPago))
            throw new ReglaDeNegocioException("La forma de pago no es válida.", nameof(FormaPago));
    }

    private static decimal Redondear(decimal importe) => decimal.Round(importe, 2, MidpointRounding.AwayFromZero);
}
