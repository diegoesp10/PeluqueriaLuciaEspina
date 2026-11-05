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

    [Required(ErrorMessage = "Indica cuándo se sortea.")]
    public DateTime? FechaSorteo { get; set; }

    public DatosSorteo Datos() => new(Titulo, TituloEn, Descripcion, DescripcionEn, FechaSorteo ?? default);

    public static SorteoFormulario Desde(Sorteo s) => new()
    {
        Titulo = s.Titulo, TituloEn = s.TituloEn, Descripcion = s.Descripcion, DescripcionEn = s.DescripcionEn, FechaSorteo = s.FechaSorteo,
    };
}

/// <summary>Cómo viaja un premio en los formularios: «Servicio-12», «Producto-5» o «ProductoSorteo-3».</summary>
public static class ClavePremio
{
    public static string De(TipoPremio tipo, int id) => $"{tipo}-{id.ToString(CultureInfo.InvariantCulture)}";

    public static PremioElegido? Leer(string? clave)
    {
        var partes = clave?.Split('-', 2);
        return partes is [var tipo, var id] && Enum.TryParse<TipoPremio>(tipo, out var t) && Enum.IsDefined(t) && !int.TryParse(tipo, out _)
            && int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var numero) ? new PremioElegido(t, numero) : null;
    }
}

public sealed record SorteoEdicionModelo(int? Id, SorteoFormulario Formulario);

/// <summary>Lo que se puede añadir como premio a un sorteo abierto.</summary>
public sealed record OpcionesPremio(IReadOnlyList<ProductoSorteo> ProductosSorteo, IReadOnlyList<Servicio> Servicios, IReadOnlyList<Producto> Productos);

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
/// <param name="Opciones">Lo que se puede añadir como premio; null si el sorteo ya no está abierto.</param>
/// <param name="TextoAviso">El mensaje que se estaba escribiendo cuando hubo un error.</param>
/// <param name="Revelar">Recién sorteado: la pantalla enseña el ganador con la animación de la ruleta.</param>
public sealed record SorteoDetalleModelo(Sorteo Sorteo, DateOnly Hoy, AvisoGanador? Aviso, bool CorreoConfigurado, AnuncioFormulario Anuncio,
    OpcionesPremio? Opciones, string? TextoAviso = null, bool Revelar = false)
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

    public static string IconoPremio(TipoPremio tipo) => tipo switch
    {
        TipoPremio.Servicio => "tijeras",
        TipoPremio.Producto => "producto",
        _ => "regalo",
    };

    /// <summary>El color de cada clase de premio, el mismo que su apartado en el menú.</summary>
    public static string TintePremio(TipoPremio tipo) => tipo switch
    {
        TipoPremio.Servicio => "tint-orange",
        TipoPremio.Producto => "tint-purple",
        _ => "tint-pink",
    };

    public static string TipoPremioTexto(TipoPremio tipo) => tipo switch
    {
        TipoPremio.Servicio => "Servicio del catálogo",
        TipoPremio.Producto => "Producto de la tienda",
        _ => "Producto de sorteo",
    };

    /// <summary>«Corte, 2 × Champú y Cena para dos», o un aviso si aún no tiene premios.</summary>
    public static string Premios(Sorteo s) => s.Premios.Count == 0 ? "Sin premios todavía" : s.PremiosEnTexto();

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

    public static string Entrega(ExtraccionSorteo e) =>
        e.EntregadoEn is { } entregado ? $"Premio entregado el {entregado:dd/MM/yyyy}" : "Premio sin entregar";

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

    /// <summary>«En 1 sorteo en marcha · sorteado 2 veces · 1 entregado» o «Aún no ha estado en ningún sorteo».</summary>
    public static string Uso(int enMarcha, int sorteados, int entregados)
    {
        var partes = new List<string>();
        if (enMarcha > 0)
            partes.Add(enMarcha == 1 ? "En 1 sorteo en marcha" : $"En {enMarcha} sorteos en marcha");
        if (sorteados > 0)
            partes.Add((sorteados == 1 ? "Sorteado 1 vez" : $"Sorteado {sorteados} veces") + (entregados == 0 ? ", sin entregar" : entregados == sorteados ? ", entregado" : $", {entregados} entregados"));
        return partes.Count == 0 ? "Aún no ha estado en ningún sorteo" : string.Join(" · ", partes);
    }
}

public sealed class ProductoSorteoFormulario
{
    [Required(ErrorMessage = "Escribe el nombre.")]
    [StringLength(ProductoSorteo.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Nombre { get; set; }

    [Required(ErrorMessage = "Escribe también el nombre en inglés: se ve en la web.")]
    [StringLength(ProductoSorteo.LongitudNombre, ErrorMessage = "Máximo {1} caracteres.")]
    public string? NombreEn { get; set; }

    [StringLength(ProductoSorteo.LongitudDescripcion, ErrorMessage = "Máximo {1} caracteres.")]
    public string? Descripcion { get; set; }

    [StringLength(ProductoSorteo.LongitudDescripcion, ErrorMessage = "Máximo {1} caracteres.")]
    public string? DescripcionEn { get; set; }

    public FichaProductoSorteo Ficha() => new(Nombre, NombreEn, Descripcion, DescripcionEn);

    public static ProductoSorteoFormulario Desde(ProductoSorteo p) => new()
    {
        Nombre = p.Nombre, NombreEn = p.NombreEn, Descripcion = p.Descripcion, DescripcionEn = p.DescripcionEn,
    };
}

public sealed record ProductosSorteoModelo(IReadOnlyList<ResumenProductoSorteo> Productos)
{
    public IReadOnlyList<ResumenProductoSorteo> Activos => [.. Productos.Where(p => p.Producto.Activo)];

    public IReadOnlyList<ResumenProductoSorteo> Eliminados => [.. Productos.Where(p => !p.Producto.Activo)];
}

/// <param name="Destino">Al crearlo desde la pantalla de un sorteo abierto, el sorteo al que se añade como premio.</param>
public sealed record ProductoSorteoEdicionModelo(int? Id, ProductoSorteoFormulario Formulario, Sorteo? Destino = null);

/// <param name="Sorteos">Los sorteos en los que ha estado como premio, lo último primero.</param>
public sealed record ProductoSorteoFichaModelo(ProductoSorteo Producto, IReadOnlyList<Sorteo> Sorteos)
{
    /// <summary>La línea de premio de este producto en ese sorteo (por la cantidad y el nombre con que se sorteó).</summary>
    public PremioSorteo? Premio(Sorteo s) => s.Premios.FirstOrDefault(p => p.ProductoSorteoId == Producto.Id);

    public int EnMarcha => Sorteos.Count(s => s.Abierto);

    public int Sorteados => Sorteos.Count(s => s.Estado == EstadoSorteo.Realizado);

    public int Entregados => Sorteos.Count(s => s.Estado == EstadoSorteo.Realizado && s.Ganador?.EntregadoEn is not null);
}
