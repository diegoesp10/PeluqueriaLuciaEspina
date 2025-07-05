using PelqueriaLuciaEspina.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace PeluqueriaLuciaEspina.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio.")]
        public string Apellido { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "El correo electrónico no es válido.")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "El número de teléfono no es válido.")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "El sexo es obligatorio.")]
        public Sexo Sexo { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.Now;
    }
}

