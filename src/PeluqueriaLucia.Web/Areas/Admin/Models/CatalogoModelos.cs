using System.ComponentModel.DataAnnotations;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public sealed class ServicioFormulario
{
    [Required(ErrorMessage = "Escribe el nombre del servicio.")]
    [StringLength(Servicio.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Obligatorio al crear; en los servicios anteriores al inglés puede faltar (la web muestra el español).</summary>
    [StringLength(100)] public string? NombreEn { get; set; }

    [Required(ErrorMessage = "Escribe el precio.")]
    [Range(typeof(decimal), "0", "9999.99", ErrorMessage = "El precio debe estar entre 0 y 9.999,99 €.",
        ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public decimal? Precio { get; set; }

    [Required(ErrorMessage = "Elige para quién es.")]
    public Sexo? Sexo { get; set; }

    public IFormFile? Imagen { get; set; }

    public bool QuitarImagen { get; set; }
    public int? CategoriaPublicaId { get; set; }
    [StringLength(1000)] public string? DescripcionPublica { get; set; }
    [StringLength(1000)] public string? DescripcionPublicaEn { get; set; }
    public Domain.Publico.TipoPrecioPublico TipoPrecioPublico { get; set; }
    [Range(5,600)] public int DuracionMinutos { get; set; } = 30;
    [Range(0,10000)] public int OrdenPublico { get; set; }
    public bool Publicado { get; set; }
    public bool Destacado { get; set; }

    public DatosServicio ADatos() => new(Nombre, Precio ?? 0, Sexo ?? Domain.Comun.Sexo.Otro,
        new(CategoriaPublicaId, DescripcionPublica, TipoPrecioPublico, DuracionMinutos, OrdenPublico, Publicado, Destacado, DescripcionPublicaEn), NombreEn);

    public ArchivoImagen? AArchivo() =>
        Imagen is { Length: > 0 } f ? new ArchivoImagen(f.OpenReadStream(), f.FileName, f.Length) : null;

    public static ServicioFormulario Desde(Servicio s) => new() { Nombre = s.Nombre, Precio = s.Precio, Sexo = s.Sexo,
        CategoriaPublicaId=s.CategoriaPublicaId, DescripcionPublica=s.DescripcionPublica, TipoPrecioPublico=s.TipoPrecioPublico,
        DuracionMinutos=s.DuracionMinutos, OrdenPublico=s.OrdenPublico, Publicado=s.Publicado, Destacado=s.Destacado };
}

public sealed record ServicioEdicionModelo(int? Id, ServicioFormulario Formulario, string? ImagenActual, bool Activo = true,
    IReadOnlyList<Domain.Publico.CategoriaTratamiento>? Categorias = null);

public sealed record CatalogoIndiceModelo(IReadOnlyList<Servicio> Activos, IReadOnlyList<Servicio> Archivados, Sexo? Sexo, string? Texto = null);
