using System.ComponentModel.DataAnnotations;
using System.Globalization;
using PeluqueriaLucia.Application.Sorteos;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Sorteos;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Models;

public enum VistaSorteos
{
    Activos,
    Historial,
}

public sealed record SorteosIndiceModelo(VistaSorteos Vista, IReadOnlyList<Sorteo> Sorteos, DateOnly Hoy);

public sealed class SorteoFormulario
{
    [Required(ErrorMessage = "Escribe el título del sorteo.")]
    [StringLength(Sorteo.LongitudTitulo, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Titulo { get; set; }

    [Required(ErrorMessage = "Escribe también el título en inglés: se ve en la web.")]
    [StringLength(Sorteo.LongitudTitulo, ErrorMessage = "Máximo {1} caracteres.")]
    public string? TituloEn { get; set; }

    [StringLength(Sorteo.LongitudDescripcion, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Descripcion { get; set; }

    [StringLength(Sorteo.LongitudDescripcion, ErrorMessage = "Máximo {1} caracteres.")]
    public string? DescripcionEn { get; set; }

    /// <summary>«Servicio-12» o «Producto-5».</summary>
    [Required(ErrorMessage = "Elige el premio.")]
    public string? Premio { get; set; }

    [Required(ErrorMessage = "Indica cuándo se sortea.")]
    public DateTime? FechaSorteo { get; set; }

    public DatosSorteo Datos() => new(Titulo, TituloEn, Descripcion, DescripcionEn, FechaSorteo ?? default);

    public PremioElegido? PremioElegido()
    {
        var partes = Premio?.Split('-', 2);
        return partes is [var tipo, var id] && Enum.TryParse<TipoPremio>(tipo, out var t) && Enum.IsDefined(t)
            && int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? new PremioElegido(t, numero) : null;
    }

    public static string Clave(TipoPremio tipo, int id) => $"{tipo}-{id.ToString(CultureInfo.InvariantCulture)}";

    public static SorteoFormulario Desde(Sorteo s) => new()
    {
        Titulo = s.Titulo, TituloEn = s.TituloEn, Descripcion = s.Descripcion, DescripcionEn = s.DescripcionEn, FechaSorteo = s.FechaSorteo,
        Premio = s.ServicioId is { } servicio ? Clave(TipoPremio.Servicio, servicio) : s.ProductoId is { } producto ? Clave(TipoPremio.Producto, producto) : null,
    };
}

/// <param name="PremioActual">Si el premio guardado ya no está en el catálogo o en la tienda, su nombre (para avisar).</param>
public sealed record SorteoEdicionModelo(int? Id, SorteoFormulario Formulario, IReadOnlyList<Servicio> Servicios, IReadOnlyList<Producto> Productos, string? PremioActual = null);

/// <summary>Un anuncio del sorteo en la web, entre dos fechas.</summary>
public sealed class AnuncioFormulario
{
    [StringLength(AnuncioSorteo.LongitudTexto, ErrorMessage = "Máximo {1} caracteres.")]
    public string? TextoAnuncio { get; set; }

    [StringLength(AnuncioSorteo.LongitudTexto, ErrorMessage = "Máximo {1} caracteres.")]
    public string? TextoAnuncioEn { get; set; }

    public DateOnly? DesdeAnuncio { get; set; }

    public DateOnly? HastaAnuncio { get; set; }
}

/// <param name="Aviso">Texto propuesto y canales del ganador; null si aún no lo hay.</param>
/// <param name="TextoAviso">El mensaje que se estaba escribiendo cuando hubo un error.</param>
/// <param name="Revelar">Recién sorteado: la pantalla enseña el ganador con la animación de la ruleta.</param>
public sealed record SorteoDetalleModelo(Sorteo Sorteo, DateOnly Hoy, AvisoGanador? Aviso, bool CorreoConfigurado, AnuncioFormulario Anuncio,
    string? TextoAviso = null, bool Revelar = false)
{
    public IReadOnlyList<ParticipanteSorteo> Participantes =>
        [.. Sorteo.Participantes.OrderBy(p => p.Cliente?.Nombre).ThenBy(p => p.Cliente?.Apellido).ThenBy(p => p.ClienteId)];

    /// <summary>Clientes que ya han ganado en alguna extracción (los de antes de repetir, también).</summary>
    public bool HaGanado(int clienteId) => Sorteo.Extracciones.Any(e => e.ClienteId == clienteId);

    public ParticipanteSorteo? Ficha(int? clienteId) => Sorteo.Participantes.FirstOrDefault(p => p.ClienteId == clienteId);
}

/// <summary>Textos y tintes de los sorteos en el panel.</summary>
public static class FormatoSorteos
{
    public static string Estado(Sorteo s) => s.Estado switch
    {
        EstadoSorteo.Realizado => "Realizado",
        EstadoSorteo.Cancelado => "Cancelado",
        _ => "Abierto",
    };

    public static string Tinte(Sorteo s) => s.Estado switch
    {
        EstadoSorteo.Realizado => "tint-green",
        EstadoSorteo.Cancelado => "tint-gray",
        _ => "tint-pink",
    };

    public static string IconoPremio(TipoPremio tipo) => tipo == TipoPremio.Servicio ? "tijeras" : "producto";

    public static string TipoPremioTexto(TipoPremio tipo) => tipo == TipoPremio.Servicio ? "Servicio del catálogo" : "Producto de la tienda";

    /// <summary>«Hoy a las 18:00», «Mañana a las 18:00», «En 5 días» o, si ya pasó sin sortear, «Tocaba el 12/11».</summary>
    public static string Cuando(Sorteo s, DateOnly hoy)
    {
        var dia = DateOnly.FromDateTime(s.FechaSorteo);
        var hora = Formato.Hora(s.FechaSorteo);
        return (dia.DayNumber - hoy.DayNumber) switch
        {
            0 => $"Hoy a las {hora}",
            1 => $"Mañana a las {hora}",
            > 1 and var n => $"En {n} días · {s.FechaSorteo:dd/MM} a las {hora}",
            _ when s.Abierto => $"Tocaba el {s.FechaSorteo:dd/MM/yyyy}: aún sin sortear",
            _ => $"{s.FechaSorteo:dd/MM/yyyy} a las {hora}",
        };
    }

    /// <summary>Ha llegado el día (o ha pasado) y aún no se ha sorteado.</summary>
    public static bool TocaSortear(Sorteo s, DateOnly hoy) => s.Abierto && DateOnly.FromDateTime(s.FechaSorteo) <= hoy;

    public static string Aviso(ExtraccionSorteo e) => e.AvisoCanal switch
    {
        CanalConfirmacion.Correo => $"Avisado por correo el {e.AvisadoEn:dd/MM/yyyy HH:mm}",
        CanalConfirmacion.WhatsApp => $"Avisado por WhatsApp el {e.AvisadoEn:dd/MM/yyyy HH:mm}",
        _ => "Sin avisar todavía",
    };

    /// <summary>El anuncio se ve en la web hoy, se verá más adelante o ya terminó.</summary>
    public static (string Texto, string Tinte) EstadoAnuncio(AnuncioSorteo a, DateOnly hoy) =>
        a.VisibleEl(hoy) ? ("En la web ahora", "tint-green")
        : a.Desde > hoy ? ($"Desde el {a.Desde:dd/MM}", "tint-blue")
        : ("Terminado", "tint-gray");

    /// <summary>Lo que se ve de los anuncios en la tarjeta de un sorteo.</summary>
    public static string Web(Sorteo s, DateOnly hoy) =>
        s.Anuncios.FirstOrDefault(a => a.VisibleEl(hoy)) is { } visible ? $"En la web hasta el {visible.Hasta:dd/MM}"
        : s.Anuncios.Where(a => a.Desde > hoy).OrderBy(a => a.Desde).FirstOrDefault() is { } proximo ? $"En la web desde el {proximo.Desde:dd/MM}"
        : "Sin anuncio en la web";
}
