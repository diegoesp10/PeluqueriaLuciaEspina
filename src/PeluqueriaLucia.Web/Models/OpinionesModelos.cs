using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Publico;

namespace PeluqueriaLucia.Web.Models;

public sealed class OpinionFormulario
{
    [Required(ErrorMessage = "Escribe el código que aparece en tu ticket."), StringLength(64)] public string Codigo { get; set; } = "";
    [Required, StringLength(50, MinimumLength = 2)] public string Alias { get; set; } = "";
    [Required, StringLength(1500, MinimumLength = 10)] public string Comentario { get; set; } = "";
    [Range(1,5)] public int Estrellas { get; set; } = 5;
    public IFormFile? Foto { get; set; }
    [Range(typeof(bool), "true", "true", ErrorMessage = "Confirma que autorizas la publicación de tu opinión y de la foto opcional.")] public bool Consentimiento { get; set; }
}
public sealed record OpinionesModelo(OpinionesPublicas Opiniones, OpinionFormulario Formulario);
