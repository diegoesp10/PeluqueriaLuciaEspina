using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class ServicioClienteViewModel
    {
        public int ClienteId { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        public OpcionPago OpcionPago { get; set; } = OpcionPago.Efectivo;

        public List<int> ServiciosSeleccionados { get; set; } = new();

        public Sexo SexoSeleccionado { get; set; } 
    }

}

