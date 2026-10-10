using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Web.Models;

public sealed class ServicioFormulario
{
    [Required(ErrorMessage = "Escribe el nombre del servicio.")]
    [StringLength(Servicio.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "Escribe el precio.")]
    [Range(typeof(decimal), "0.01", "9999.99", ErrorMessage = "El precio debe estar entre 0,01 y 9.999,99 €.",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Precio { get; set; }

    [Required(ErrorMessage = "Elige para quién es.")]
    public Sexo? Sexo { get; set; }

    public IFormFile? Imagen { get; set; }

    public bool QuitarImagen { get; set; }

    public DatosServicio ADatos() => new(Nombre, Precio ?? 0, Sexo ?? Domain.Comun.Sexo.Otro);

    public ArchivoImagen? AArchivo() =>
        Imagen is { Length: > 0 } f ? new ArchivoImagen(f.OpenReadStream(), f.FileName, f.Length) : null;

    public static ServicioFormulario Desde(Servicio s) => new() { Nombre = s.Nombre, Precio = s.Precio, Sexo = s.Sexo };
}

public sealed record ServicioEdicionModelo(int? Id, ServicioFormulario Formulario, string? ImagenActual, bool Activo = true);

public sealed record CatalogoIndiceModelo(IReadOnlyList<Servicio> Activos, IReadOnlyList<Servicio> Archivados, Sexo? Sexo);
