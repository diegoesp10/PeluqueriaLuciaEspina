using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class CortePelo
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del corte es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Range(0.01, 999.99, ErrorMessage = "El precio debe estar entre 0,01 y 999,99 €")]
        public decimal PrecioBase { get; set; }

        [Required]
        public Sexo Sexo { get; set; }

        public string? ImagenRuta { get; set; } 

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}
