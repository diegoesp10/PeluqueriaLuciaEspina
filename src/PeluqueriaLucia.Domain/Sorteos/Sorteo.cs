using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Sorteos;

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum EstadoSorteo
{
    /// <summary>Se pueden cambiar los datos y los participantes, y sortear.</summary>
    Abierto = 0,

    /// <summary>Tiene ganador. Se puede repetir o elegir otro ganador, pero ya no cambian los participantes.</summary>
    Realizado = 1,

    /// <summary>No se hará; queda en el historial.</summary>
    Cancelado = 2,
}

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum TipoPremio
{
    Servicio = 0,
    Producto = 1,
}

/// <summary>Cómo salió el ganador. Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum FormaGanador
{
    /// <summary>Sorteado al azar entre los participantes.</summary>
    AlAzar = 0,

    /// <summary>Lo eligió el salón.</summary>
    ElegidoPorElSalon = 1,
}

/// <summary>El premio de un sorteo: un servicio del catálogo o un producto de la tienda, con su nombre del momento.</summary>
public sealed record PremioSorteo(TipoPremio Tipo, int Id, string Nombre, string? NombreEn)
{
    public static PremioSorteo De(Servicio servicio) => new(TipoPremio.Servicio, servicio.Id, servicio.Nombre, servicio.NombreEn);

    public static PremioSorteo De(Producto producto) => new(TipoPremio.Producto, producto.Id, producto.Nombre, null);
}

/// <summary>Lo que escribe el salón de un sorteo: título y descripción, en español y en inglés, y cuándo se sortea.</summary>
public sealed record DatosSorteo(string? Titulo, string? TituloEn, string? Descripcion, string? DescripcionEn, DateTime FechaSorteo);

/// <summary>
/// Un sorteo de un servicio o un producto entre los clientes que elige el salón. El ganador sale al azar (o lo elige el
/// salón) y cada extracción queda en el historial: cuándo, quién, cómo y entre cuántos. Los anuncios lo dan a conocer en
/// la web entre las fechas que se elijan.
/// </summary>
public sealed class Sorteo
{
    public const int LongitudTitulo = 120;
    public const int LongitudDescripcion = 1000;
    public const int LongitudMotivo = 300;
    public const int MaximoParticipantes = 5000;

    private readonly List<ParticipanteSorteo> _participantes = [];
    private readonly List<ExtraccionSorteo> _extracciones = [];
    private readonly List<AnuncioSorteo> _anuncios = [];

    private Sorteo()
    {
    }

    public int Id { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? TituloEn { get; private set; }

    public string? Descripcion { get; private set; }

    public string? DescripcionEn { get; private set; }

    public TipoPremio TipoPremio { get; private set; }

    /// <summary>El servicio que se sortea, si sigue en el catálogo.</summary>
    public int? ServicioId { get; private set; }

    /// <summary>El producto que se sortea, si sigue en la tienda.</summary>
    public int? ProductoId { get; private set; }

    /// <summary>Nombre del premio al crear o editar el sorteo: no cambia aunque cambie o se borre el servicio o el producto.</summary>
    public string Premio { get; private set; } = string.Empty;

    public string? PremioEn { get; private set; }

    /// <summary>Cuándo se sortea (hora local del salón).</summary>
    public DateTime FechaSorteo { get; private set; }

    public EstadoSorteo Estado { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    /// <summary>Control de concurrencia: dos «Sortear» a la vez no pueden dar dos ganadores.</summary>
    public byte[] Version { get; private set; } = [];

    public IReadOnlyList<ParticipanteSorteo> Participantes => _participantes;

    /// <summary>Todas las extracciones, de la primera a la última.</summary>
    public IReadOnlyList<ExtraccionSorteo> Extracciones => [.. _extracciones.OrderBy(e => e.Fecha).ThenBy(e => e.Id)];

    public IReadOnlyList<AnuncioSorteo> Anuncios => [.. _anuncios.OrderBy(a => a.Desde).ThenBy(a => a.Id)];

    /// <summary>La extracción que vale: la última. Null si aún no se ha sorteado.</summary>
    public ExtraccionSorteo? Ganador => Extracciones.LastOrDefault();

    public bool Abierto => Estado == EstadoSorteo.Abierto;

    public string TituloEnIdioma(bool ingles) => ingles && TituloEn is { Length: > 0 } ? TituloEn : Titulo;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;

    public string PremioEnIdioma(bool ingles) => ingles && PremioEn is { Length: > 0 } ? PremioEn : Premio;

    public static Sorteo Crear(DatosSorteo datos, PremioSorteo premio, DateTime ahora)
    {
        var sorteo = new Sorteo { Estado = EstadoSorteo.Abierto, CreadoEn = ahora };
        sorteo.Aplicar(datos, premio, ahora);
        return sorteo;
    }

    /// <summary>Cambia los datos y el premio mientras no se haya sorteado.</summary>
    public void Editar(DatosSorteo datos, PremioSorteo premio, DateTime ahora)
    {
        ComprobarAbierto("cambiar sus datos");
        Aplicar(datos, premio, ahora);
    }

    /// <summary>Añade participantes; los que ya estaban se pasan por alto. Devuelve cuántos se han añadido.</summary>
    public int AgregarParticipantes(IEnumerable<Cliente> clientes, DateTime ahora)
    {
        ComprobarAbierto("cambiar los participantes");
        var nuevos = clientes.DistinctBy(c => c.Id).Where(c => _participantes.All(p => p.ClienteId != c.Id)).ToList();
        if (_participantes.Count + nuevos.Count > MaximoParticipantes)
            throw new ReglaDeNegocioException($"Un sorteo admite hasta {MaximoParticipantes} participantes.", "Participantes");
        _participantes.AddRange(nuevos.Select(c => new ParticipanteSorteo(c, ahora)));
        if (nuevos.Count > 0)
            ActualizadoEn = ahora;
        return nuevos.Count;
    }

    public void QuitarParticipante(int clienteId, DateTime ahora)
    {
        ComprobarAbierto("cambiar los participantes");
        var participante = _participantes.FirstOrDefault(p => p.ClienteId == clienteId)
            ?? throw new ReglaDeNegocioException("Ese cliente ya no participa en el sorteo.", "Participantes");
        _participantes.Remove(participante);
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Sortea el premio: <paramref name="azar"/> da un número entre 0 y el número de participantes (sin incluirlo), que
    /// elige al ganador en el orden en que se apuntaron. Hacen falta al menos dos participantes.
    /// </summary>
    public ExtraccionSorteo Sortear(Func<int, int> azar, DateTime ahora, int? administradorId)
    {
        ComprobarAbierto("sortearlo");
        if (_participantes.Count < 2)
            throw new ReglaDeNegocioException("Añade al menos dos participantes para sortear.", "Participantes");
        return Extraer(Candidatos(), azar, null, ahora, administradorId);
    }

    /// <summary>
    /// Vuelve a sortear un sorteo realizado (por ejemplo, si el ganador no responde) entre los participantes que aún no han
    /// ganado. La extracción anterior se conserva en el historial con el motivo de la repetición.
    /// </summary>
    public ExtraccionSorteo Repetir(Func<int, int> azar, string? motivo, DateTime ahora, int? administradorId)
    {
        if (Estado != EstadoSorteo.Realizado)
            throw new ReglaDeNegocioException("Solo se repite un sorteo que ya se ha realizado.");
        var limpio = Motivo(motivo, obligatorio: true);
        var candidatos = Candidatos();
        if (candidatos.Count == 0)
            throw new ReglaDeNegocioException("Ya han ganado todos los participantes: no queda nadie entre quien sortear.", "Participantes");
        return Extraer(candidatos, azar, limpio, ahora, administradorId);
    }

    /// <summary>
    /// El salón decide quién gana (entre los participantes). Si ya había ganador, hace falta el motivo y la extracción anterior
    /// se conserva en el historial.
    /// </summary>
    public ExtraccionSorteo ElegirGanador(int clienteId, string? motivo, DateTime ahora, int? administradorId)
    {
        if (Estado == EstadoSorteo.Cancelado)
            throw new ReglaDeNegocioException("El sorteo está cancelado.");
        var participante = _participantes.FirstOrDefault(p => p.ClienteId == clienteId)
            ?? throw new ReglaDeNegocioException("Elige a uno de los participantes.", "ClienteId");
        if (Ganador?.ClienteId == clienteId)
            throw new ReglaDeNegocioException("Ese cliente ya es el ganador.", "ClienteId");
        var limpio = Motivo(motivo, obligatorio: Estado == EstadoSorteo.Realizado);

        var extraccion = new ExtraccionSorteo(this, participante, FormaGanador.ElegidoPorElSalon, ahora, _participantes.Count, limpio, administradorId);
        _extracciones.Add(extraccion);
        Estado = EstadoSorteo.Realizado;
        ActualizadoEn = ahora;
        return extraccion;
    }

    /// <summary>Anota que se ha avisado al ganador actual y por dónde.</summary>
    public void MarcarAvisado(CanalConfirmacion canal, DateTime ahora)
    {
        var ganador = Ganador ?? throw new ReglaDeNegocioException("Aún no hay ganador al que avisar.");
        if (!Enum.IsDefined(canal))
            throw new ReglaDeNegocioException("Elige correo o WhatsApp.");
        ganador.Avisar(canal, ahora);
        ActualizadoEn = ahora;
    }

    public void Cancelar(string? motivo, DateTime ahora)
    {
        ComprobarAbierto("cancelarlo");
        MotivoCancelacion = Motivo(motivo, obligatorio: false);
        Estado = EstadoSorteo.Cancelado;
        ActualizadoEn = ahora;
    }

    /// <summary>Un sorteo con ganador forma parte del historial y no se borra (si no se va a hacer, se cancela).</summary>
    public void ComprobarQueSePuedeEliminar()
    {
        if (_extracciones.Count > 0)
            throw new ReglaDeNegocioException("Este sorteo ya tiene ganador y se conserva en el historial.");
    }

    /// <summary>Un aviso en la web entre dos fechas (incluidas), en español y en inglés.</summary>
    public AnuncioSorteo Anunciar(string? texto, string? textoEn, DateOnly desde, DateOnly hasta, DateTime ahora)
    {
        if (Estado == EstadoSorteo.Cancelado)
            throw new ReglaDeNegocioException("El sorteo está cancelado: no se puede anunciar.");
        var anuncio = new AnuncioSorteo(this, texto, textoEn, desde, hasta, ahora);
        _anuncios.Add(anuncio);
        ActualizadoEn = ahora;
        return anuncio;
    }

    public void QuitarAnuncio(int anuncioId, DateTime ahora)
    {
        var anuncio = _anuncios.FirstOrDefault(a => a.Id == anuncioId)
            ?? throw new ReglaDeNegocioException("Ese anuncio ya no está.");
        _anuncios.Remove(anuncio);
        ActualizadoEn = ahora;
    }

    /// <summary>Los participantes que aún no han ganado, en el orden en que se apuntaron.</summary>
    private List<ParticipanteSorteo> Candidatos() =>
        [.. _participantes.Where(p => _extracciones.All(e => e.ClienteId != p.ClienteId)).OrderBy(p => p.Id).ThenBy(p => p.AnadidoEn).ThenBy(p => p.ClienteId)];

    private ExtraccionSorteo Extraer(List<ParticipanteSorteo> candidatos, Func<int, int> azar, string? motivo, DateTime ahora, int? administradorId)
    {
        var indice = azar(candidatos.Count);
        if (indice < 0 || indice >= candidatos.Count)
            throw new InvalidOperationException($"El azar ha dado {indice}, fuera de 0 a {candidatos.Count - 1}.");
        var extraccion = new ExtraccionSorteo(this, candidatos[indice], FormaGanador.AlAzar, ahora, candidatos.Count, motivo, administradorId);
        _extracciones.Add(extraccion);
        Estado = EstadoSorteo.Realizado;
        ActualizadoEn = ahora;
        return extraccion;
    }

    private void ComprobarAbierto(string accion)
    {
        if (Estado == EstadoSorteo.Realizado)
            throw new ReglaDeNegocioException($"El sorteo ya se ha realizado: no se puede {accion}.");
        if (Estado == EstadoSorteo.Cancelado)
            throw new ReglaDeNegocioException($"El sorteo está cancelado: no se puede {accion}.");
    }

    private static string? Motivo(string? motivo, bool obligatorio)
    {
        var limpio = Texto.Opcional(motivo);
        if (obligatorio && limpio is null)
            throw new ReglaDeNegocioException("Indica el motivo (por ejemplo, «no ha respondido» o «no puede venir»).", "Motivo");
        if (limpio?.Length > LongitudMotivo)
            throw new ReglaDeNegocioException($"El motivo admite hasta {LongitudMotivo} caracteres.", "Motivo");
        return limpio;
    }

    private void Aplicar(DatosSorteo datos, PremioSorteo premio, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(premio);
        var titulo = Texto.Obligatorio(datos.Titulo, LongitudTitulo, "El título", "Titulo");
        // Se publica en la web: como todo lo nuevo, en los dos idiomas
        var tituloEn = Texto.Obligatorio(datos.TituloEn, LongitudTitulo, "El título en inglés", "TituloEn");
        var descripcion = Texto.Opcional(datos.Descripcion);
        var descripcionEn = Texto.Opcional(datos.DescripcionEn);
        if (descripcion?.Length > LongitudDescripcion || descripcionEn?.Length > LongitudDescripcion)
            throw new ReglaDeNegocioException($"La descripción admite hasta {LongitudDescripcion} caracteres.", "Descripcion");
        if (descripcion is not null && descripcionEn is null)
            throw new ReglaDeNegocioException("Escribe también la descripción en inglés.", "DescripcionEn");
        if (!Enum.IsDefined(premio.Tipo) || string.IsNullOrWhiteSpace(premio.Nombre))
            throw new ReglaDeNegocioException("Elige el premio: un servicio del catálogo o un producto de la tienda.", "Premio");
        if (datos.FechaSorteo.Year < 2000)
            throw new ReglaDeNegocioException("Indica cuándo se sortea.", "FechaSorteo");

        Titulo = titulo;
        TituloEn = tituloEn;
        Descripcion = descripcion;
        DescripcionEn = descripcionEn;
        TipoPremio = premio.Tipo;
        ServicioId = premio.Tipo == TipoPremio.Servicio ? premio.Id : null;
        ProductoId = premio.Tipo == TipoPremio.Producto ? premio.Id : null;
        Premio = premio.Nombre.Trim();
        PremioEn = Texto.Opcional(premio.NombreEn);
        FechaSorteo = datos.FechaSorteo.AddSeconds(-datos.FechaSorteo.Second).AddMilliseconds(-datos.FechaSorteo.Millisecond);
        ActualizadoEn = ahora;
    }
}

/// <summary>Un cliente que entra en un sorteo.</summary>
public sealed class ParticipanteSorteo
{
    private ParticipanteSorteo()
    {
    }

    internal ParticipanteSorteo(Cliente cliente, DateTime ahora)
    {
        Cliente = cliente;
        ClienteId = cliente.Id;
        AnadidoEn = ahora;
    }

    public int Id { get; private set; }

    public int SorteoId { get; private set; }

    public int ClienteId { get; private set; }

    public Cliente? Cliente { get; private set; }

    public DateTime AnadidoEn { get; private set; }
}

/// <summary>
/// Una extracción del sorteo: quién ganó, cuándo, cómo (al azar entre cuántos, o elegido por el salón), por qué se repitió
/// si hubo una anterior, y si se le avisó y por dónde. Guarda el nombre del ganador del momento.
/// </summary>
public sealed class ExtraccionSorteo
{
    private ExtraccionSorteo()
    {
    }

    internal ExtraccionSorteo(Sorteo sorteo, ParticipanteSorteo ganador, FormaGanador forma, DateTime fecha, int participantes, string? motivo, int? administradorId)
    {
        Sorteo = sorteo;
        ClienteId = ganador.ClienteId;
        Ganador = ganador.Cliente?.NombreCompleto is { Length: > 0 } nombre ? nombre : $"Cliente {ganador.ClienteId}";
        Forma = forma;
        Fecha = fecha;
        Participantes = participantes;
        Motivo = motivo;
        AdministradorId = administradorId;
    }

    public int Id { get; private set; }

    public int SorteoId { get; private set; }

    public Sorteo? Sorteo { get; private set; }

    /// <summary>La ficha del ganador; null si se ha borrado (queda su nombre).</summary>
    public int? ClienteId { get; private set; }

    public Cliente? Cliente { get; private set; }

    public string Ganador { get; private set; } = string.Empty;

    public FormaGanador Forma { get; private set; }

    public DateTime Fecha { get; private set; }

    /// <summary>Entre cuántos se sorteó (los que aún no habían ganado).</summary>
    public int Participantes { get; private set; }

    /// <summary>Por qué se repitió el sorteo o se eligió otro ganador.</summary>
    public string? Motivo { get; private set; }

    public int? AdministradorId { get; private set; }

    public CanalConfirmacion? AvisoCanal { get; private set; }

    public DateTime? AvisadoEn { get; private set; }

    internal void Avisar(CanalConfirmacion canal, DateTime ahora)
    {
        AvisoCanal = canal;
        AvisadoEn = ahora;
    }
}

/// <summary>Aviso del sorteo en la web pública entre dos fechas (incluidas), en español y en inglés.</summary>
public sealed class AnuncioSorteo
{
    public const int LongitudTexto = 500;

    /// <summary>Lo más que puede durar un anuncio.</summary>
    public const int DiasMaximos = 366;

    private AnuncioSorteo()
    {
    }

    internal AnuncioSorteo(Sorteo sorteo, string? texto, string? textoEn, DateOnly desde, DateOnly hasta, DateTime ahora)
    {
        Texto = Comun.Texto.Obligatorio(texto, LongitudTexto, "El texto del anuncio", "TextoAnuncio");
        TextoEn = Comun.Texto.Obligatorio(textoEn, LongitudTexto, "El texto en inglés", "TextoAnuncioEn");
        if (hasta < desde)
            throw new ReglaDeNegocioException("La fecha de fin tiene que ser posterior a la de inicio.", "HastaAnuncio");
        if (hasta < DateOnly.FromDateTime(ahora))
            throw new ReglaDeNegocioException("El anuncio terminaría antes de hoy: elige otra fecha de fin.", "HastaAnuncio");
        if (hasta.DayNumber - desde.DayNumber >= DiasMaximos)
            throw new ReglaDeNegocioException("Un anuncio puede estar en la web hasta un año.", "HastaAnuncio");
        Sorteo = sorteo;
        Desde = desde;
        Hasta = hasta;
        CreadoEn = ahora;
    }

    public int Id { get; private set; }

    public int SorteoId { get; private set; }

    public Sorteo? Sorteo { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public string? TextoEn { get; private set; }

    public DateOnly Desde { get; private set; }

    public DateOnly Hasta { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public bool VisibleEl(DateOnly dia) => Desde <= dia && dia <= Hasta;

    public string TextoEnIdioma(bool ingles) => ingles && TextoEn is { Length: > 0 } ? TextoEn : Texto;
}
