using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Domain.Catalogo;

/// <summary>Un servicio del catálogo (corte, tinte…) con su precio vigente.</summary>
public sealed class Servicio
{
    public const int LongitudNombre = 100;
    public const decimal PrecioMinimo = 0m;
    public const decimal PrecioMaximo = 9999.99m;

    private Servicio()
    {
    }

    public int Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public decimal Precio { get; private set; }

    public Sexo Sexo { get; private set; }

    public string? ImagenRuta { get; private set; }

    /// <summary>Los servicios archivados no se ofrecen en visitas nuevas, pero siguen en el historial.</summary>
    public bool Activo { get; private set; } = true;

    public DateTime FechaCreacion { get; private set; }
    public int? CategoriaPublicaId { get; private set; }
    public string? DescripcionPublica { get; private set; }
    public TipoPrecioPublico TipoPrecioPublico { get; private set; }
    public int DuracionMinutos { get; private set; } = 30;
    public int OrdenPublico { get; private set; }
    public bool Publicado { get; private set; }
    public bool Destacado { get; private set; }

    public void ConfigurarPublicacion(int? categoria, string? descripcion, TipoPrecioPublico tipo, int minutos, int orden, bool publicado, bool destacado)
    {
        if ((publicado && (categoria is null or <= 0 || (tipo != TipoPrecioPublico.Consultar && Precio <= 0))) ||
            descripcion?.Length > 1000 || !Enum.IsDefined(tipo) || minutos is < 5 or > 600 || orden is < 0 or > 10000)
            throw new ReglaDeNegocioException("Revisa la categoría, el precio y los datos de publicación.");
        CategoriaPublicaId = categoria; DescripcionPublica = string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();
        TipoPrecioPublico = tipo; DuracionMinutos = minutos; OrdenPublico = orden; Publicado = publicado; Destacado = destacado;
    }

    public static Servicio Crear(string nombre, decimal precio, Sexo sexo, DateTime ahora)
    {
        var servicio = new Servicio { FechaCreacion = ahora, Activo = true };
        servicio.Actualizar(nombre, precio, sexo);
        return servicio;
    }

    /// <summary>
    /// Cambia nombre, precio o público. Las visitas ya registradas conservan el nombre y el precio que tenían.
    /// </summary>
    public void Actualizar(string nombre, decimal precio, Sexo sexo)
    {
        Nombre = Texto.Obligatorio(nombre, LongitudNombre, "El nombre", nameof(Nombre));
        if (precio < PrecioMinimo || precio > PrecioMaximo)
            throw new ReglaDeNegocioException($"El precio debe estar entre {PrecioMinimo:0.00} y {PrecioMaximo:0.00} €.", nameof(Precio));
        Precio = decimal.Round(precio, 2, MidpointRounding.AwayFromZero);
        Sexo = Enum.IsDefined(sexo) ? sexo : throw new ReglaDeNegocioException("El público no es válido.", nameof(Sexo));
    }

    public void CambiarImagen(string? ruta) => ImagenRuta = ruta;

    public void Archivar() => Activo = false;

    public void Reactivar() => Activo = true;
}
