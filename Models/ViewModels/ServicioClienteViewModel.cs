using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class ServicioClienteViewModel
    {
        // Nullable: permite generar el ticket sin cliente asociado.
        public int? ClienteId { get; set; }

        [Required]
        public DateTime Fecha { get; set; }

        public OpcionPago OpcionPago { get; set; } = OpcionPago.Efectivo;

        public List<ServicioSeleccionadoViewModel> ServiciosSeleccionados { get; set; } = new();

        public Sexo SexoSeleccionado { get; set; }
    }

    // Servicio elegido al crear el ticket, con el precio (editable en el formulario,
    // precargado con el precio de catálogo pero modificable antes de guardar).
    public class ServicioSeleccionadoViewModel
    {
        [Required]
        public int ServicioGenericoId { get; set; }

        [Range(0.01, 9999.99, ErrorMessage = "El precio debe estar entre 0,01 y 9999,99 €")]
        public decimal Precio { get; set; }
    }

}

