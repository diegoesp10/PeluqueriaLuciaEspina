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
    }
}
