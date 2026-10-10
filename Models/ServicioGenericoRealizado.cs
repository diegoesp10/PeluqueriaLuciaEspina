using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class ServicioGenericoRealizado
    {
        public int Id { get; set; }

        public int ServicioClienteId { get; set; }
        public ServicioCliente ? ServicioCliente { get; set; }

        public int ServicioGenericoId { get; set; }
        public ServicioGenerico? ServicioGenerico { get; set; } // ❌ NO instancies aquí

        // Precio realmente aplicado a este servicio en este ticket. Se rellena a partir del
        // precio de catálogo al crear el ticket, pero es editable en ese momento, por lo que
        // no debe recalcularse nunca a partir de ServicioGenerico.Precio.
        [Range(0.01, 9999.99, ErrorMessage = "El precio debe estar entre 0,01 y 9999,99 €")]
        public decimal PrecioAplicado { get; set; }
    }
}
