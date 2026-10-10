namespace PeluqueriaLucia.Web.Publico;

/// <summary>
/// Datos de la web pública, leídos de contenido-publico.json. Se pueden cambiar (horario, precios, textos) sin
/// recompilar: la aplicación recarga el fichero al guardarlo.
/// </summary>
public sealed class ContenidoPublico
{
    public const string Seccion = "Publico";

    public string Nombre { get; set; } = string.Empty;

    public string Lema { get; set; } = string.Empty;

    public string Presentacion { get; set; } = string.Empty;

    public string Direccion { get; set; } = string.Empty;

    public string CodigoPostal { get; set; } = string.Empty;

    public string Ciudad { get; set; } = string.Empty;

    public string Telefono { get; set; } = string.Empty;

    public string Instagram { get; set; } = string.Empty;

    public string InstagramUsuario { get; set; } = string.Empty;

    /// <summary>Página de reservas online (Booksy).</summary>
    public string Reservas { get; set; } = string.Empty;

    public string Mapa { get; set; } = string.Empty;

    public string Valoracion { get; set; } = string.Empty;

    public int Resenas { get; set; }

    public List<Valor> Valores { get; set; } = [];

    public List<string> Comodidades { get; set; } = [];

    public List<DiaHorario> Horario { get; set; } = [];

    public List<CategoriaServicios> Categorias { get; set; } = [];

    public string TelefonoEnlace => "tel:+34" + new string(Telefono.Where(char.IsDigit).ToArray());

    public IEnumerable<ServicioPublico> Destacados => Categorias.SelectMany(c => c.Servicios).Where(s => s.Destacado);
}

public sealed class Valor
{
    public string Icono { get; set; } = "hoja";

    public string Titulo { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;
}

public sealed class DiaHorario
{
    public string Dia { get; set; } = string.Empty;

    /// <summary>Tramos «09:45 – 13:00». Vacío: cerrado.</summary>
    public List<string> Tramos { get; set; } = [];
}

public sealed class CategoriaServicios
{
    public string Nombre { get; set; } = string.Empty;

    public string Id => new(Nombre.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => char.IsAsciiLetterOrDigit(c)).ToArray());

    public List<ServicioPublico> Servicios { get; set; } = [];
}

public sealed class ServicioPublico
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Texto libre: «20,00 €», «desde 9,00 €», «Consultar».</summary>
    public string Precio { get; set; } = string.Empty;

    public string Duracion { get; set; } = string.Empty;

    public string? Detalle { get; set; }

    public bool Destacado { get; set; }
}
