using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class ServicioCliente
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }

        // Nullable: un ticket puede generarse sin cliente asociado (venta anónima / "público general").
        public int? ClienteId { get; set; }

        [Required(ErrorMessage = "La opción de pago es obligatoria")]
        public OpcionPago OpcionPago { get; set; } = OpcionPago.Efectivo;

        public Estado Estado { get; set; } = Estado.NoCobrado;
        public Cliente? Cliente { get; set; }
        public ICollection<ServicioGenericoRealizado> ServiciosRealizados { get; set; } = new List<ServicioGenericoRealizado>();
        public decimal ? TotalEfectivo { get; set; } = 0;

        public decimal ? TotalTarjeta { get; set; } = 0;

        // Porcentaje de IVA aplicado al cobrar (0 o 21). Se decide en el momento del cobro
        // y se refleja en el desglose del ticket.
        [Range(0, 21, ErrorMessage = "El IVA debe ser 0% o 21%")]
        public decimal PorcentajeIva { get; set; } = 0;

    }
}
