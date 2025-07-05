using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class ServicioGenerico
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        [Range(0.01, 999.99, ErrorMessage = "El precio debe estar entre 0,01 y 999,99 €")]
        public decimal Precio { get; set; }

        public string? ImagenRuta { get; set; }

        public Sexo Sexo { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
        public ICollection<ServicioGenericoRealizado> Realizados { get; set; } = new List<ServicioGenericoRealizado>();

    }
}
