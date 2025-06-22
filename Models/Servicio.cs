using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class Servicio
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del servicio es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Range(0.01, 999.99, ErrorMessage = "El precio debe estar entre 0,01 y 999,99 €")] public decimal Precio { get; set; }

        [Required]
        public Estado Estado { get; set; } = Estado.NoCobrado;

        public OpcionPago? OpcionPago { get; set; }

        public int? CortePeloId { get; set; }

        public CortePelo? CortePelo { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}

