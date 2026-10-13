using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Publico;

namespace PeluqueriaLucia.Web.Models;

public sealed class OpinionFormulario
{
    [Required(ErrorMessage = "Escribe el código que aparece en tu ticket."), StringLength(64, ErrorMessage = "El código no es válido.")] public string Codigo { get; set; } = "";
    [Required(ErrorMessage = "Escribe el nombre que quieres mostrar."), StringLength(50, MinimumLength = 2, ErrorMessage = "El nombre público debe tener entre 2 y 50 caracteres.")] public string Alias { get; set; } = "";
    [Required(ErrorMessage = "Cuéntanos tu experiencia."), StringLength(1500, MinimumLength = 10, ErrorMessage = "El comentario debe tener entre 10 y 1500 caracteres.")] public string Comentario { get; set; } = "";
    [Range(1, 5, ErrorMessage = "Elige de 1 a 5 estrellas.")] public int Estrellas { get; set; } = 5;
    public IFormFile? Foto { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirma que autorizas la publicación de tu opinión y de la foto opcional.")] public bool Consentimiento { get; set; }
}
public sealed record OpinionesModelo(OpinionesPublicas Opiniones, OpinionFormulario Formulario);
