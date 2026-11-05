using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Domain.Sorteos;

/// <summary>Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum EstadoSorteo
{
    /// <summary>Se pueden cambiar los datos, los premios y los participantes, y sortear.</summary>
    Abierto = 0,

    /// <summary>Tiene ganador. Se puede repetir o elegir otro ganador, pero ya no cambian los premios ni los participantes.</summary>
    Realizado = 1,

    /// <summary>No se hará; queda en el historial.</summary>
    Cancelado = 2,
}

/// <summary>Qué es cada premio. Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum TipoPremio
{
    /// <summary>Un servicio del catálogo de la peluquería.</summary>
    Servicio = 0,

    /// <summary>Un producto de la tienda de la peluquería.</summary>
    Producto = 1,

    /// <summary>Un producto de sorteo: algo que no es de la peluquería, dado de alta para sortearlo.</summary>
    ProductoSorteo = 2,
}

/// <summary>Cómo salió el ganador. Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum FormaGanador
{
    /// <summary>Sorteado al azar entre los participantes.</summary>
    AlAzar = 0,

    /// <summary>Lo eligió el salón.</summary>
    ElegidoPorElSalon = 1,
}

/// <summary>Lo que se elige como premio, con sus textos del momento.</summary>
public sealed record DatosPremio(TipoPremio Tipo, int Id, string Nombre, string? NombreEn, string? Descripcion = null, string? DescripcionEn = null)
{
    public static DatosPremio De(Servicio servicio) => new(TipoPremio.Servicio, servicio.Id, servicio.Nombre, servicio.NombreEn);

    public static DatosPremio De(Producto producto) => new(TipoPremio.Producto, producto.Id, producto.Nombre, null);

    public static DatosPremio De(ProductoSorteo producto) =>
        new(TipoPremio.ProductoSorteo, producto.Id, producto.Nombre, producto.NombreEn, producto.Descripcion, producto.DescripcionEn);
}

/// <summary>Lo que escribe el salón de un sorteo: título y descripción, en español y en inglés, y cuándo se sortea.</summary>
public sealed record DatosSorteo(string? Titulo, string? TituloEn, string? Descripcion, string? DescripcionEn, DateTime FechaSorteo);

/// <summary>
/// Un sorteo entre los clientes que elige el salón. El premio puede ser un lote: servicios del catálogo, productos de la
/// tienda y productos de sorteo, cada uno con su cantidad. El ganador sale al azar (o lo elige el salón) y cada extracción
/// queda en el historial: cuándo, quién, cómo, entre cuántos y cuándo recibió el premio. Los anuncios lo dan a conocer en
/// la web entre las fechas que se elijan.
/// </summary>
public sealed class Sorteo
{
    public const int LongitudTitulo = 120;
    public const int LongitudDescripcion = 1000;
    public const int LongitudMotivo = 300;
    public const int MaximoParticipantes = 5000;
    public const int MaximoPremios = 20;

    private readonly List<ParticipanteSorteo> _participantes = [];
    private readonly List<ExtraccionSorteo> _extracciones = [];
    private readonly List<AnuncioSorteo> _anuncios = [];
    private readonly List<PremioSorteo> _premios = [];

    private Sorteo()
    {
    }

    public int Id { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? TituloEn { get; private set; }

    public string? Descripcion { get; private set; }

    public string? DescripcionEn { get; private set; }

    /// <summary>Cuándo se sortea (hora local del salón).</summary>
    public DateTime FechaSorteo { get; private set; }

    public EstadoSorteo Estado { get; private set; }

    public string? MotivoCancelacion { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    /// <summary>Control de concurrencia: dos «Sortear» a la vez no pueden dar dos ganadores.</summary>
    public byte[] Version { get; private set; } = [];

    /// <summary>Los premios en el orden en que se añadieron.</summary>
    public IReadOnlyList<PremioSorteo> Premios => [.. _premios.OrderBy(p => p.Id == 0).ThenBy(p => p.Id)];

    public IReadOnlyList<ParticipanteSorteo> Participantes => _participantes;

    /// <summary>Todas las extracciones, de la primera a la última.</summary>
    public IReadOnlyList<ExtraccionSorteo> Extracciones => [.. _extracciones.OrderBy(e => e.Fecha).ThenBy(e => e.Id)];

    public IReadOnlyList<AnuncioSorteo> Anuncios => [.. _anuncios.OrderBy(a => a.Desde).ThenBy(a => a.Id)];

    /// <summary>La extracción que vale: la última. Null si aún no se ha sorteado.</summary>
    public ExtraccionSorteo? Ganador => Extracciones.LastOrDefault();

    public bool Abierto => Estado == EstadoSorteo.Abierto;

    public string TituloEnIdioma(bool ingles) => ingles && TituloEn is { Length: > 0 } ? TituloEn : Titulo;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;

    /// <summary>«Tinte ecológico, 2 × Champú y Cena para dos»; vacío si aún no tiene premios.</summary>
    public string PremiosEnTexto(bool ingles = false) => PremioSorteo.Unir(Premios.Select(p => p.TextoEnIdioma(ingles)), ingles);

    public static Sorteo Crear(DatosSorteo datos, DateTime ahora)
    {
        var sorteo = new Sorteo { Estado = EstadoSorteo.Abierto, CreadoEn = ahora };
        sorteo.Aplicar(datos, ahora);
        return sorteo;
    }

    /// <summary>Cambia los datos mientras no se haya sorteado.</summary>
    public void Editar(DatosSorteo datos, DateTime ahora)
    {
        ComprobarAbierto("cambiar sus datos");
        Aplicar(datos, ahora);
    }

    /// <summary>
    /// Añade un premio al lote: un servicio, un producto de la tienda o un producto de sorteo. Si ya estaba, suma la
    /// cantidad. Guarda el nombre y la descripción del momento.
    /// </summary>
    public PremioSorteo AgregarPremio(DatosPremio premio, int cantidad, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(premio);
        ComprobarAbierto("cambiar los premios");
        if (cantidad is < 1 or > PremioSorteo.CantidadMaxima)
            throw new ReglaDeNegocioException($"La cantidad tiene que estar entre 1 y {PremioSorteo.CantidadMaxima}.", "Cantidad");

        var existente = _premios.FirstOrDefault(p => p.Es(premio.Tipo, premio.Id));
        if (existente is not null)
            existente.Sumar(cantidad);
        else if (_premios.Count >= MaximoPremios)
            throw new ReglaDeNegocioException($"Un sorteo admite hasta {MaximoPremios} premios distintos.", "Premio");
        else
            _premios.Add(existente = new PremioSorteo(this, premio, cantidad));
        ActualizadoEn = ahora;
        return existente;
    }

    public void QuitarPremio(int premioId, DateTime ahora)
    {
        ComprobarAbierto("cambiar los premios");
        var premio = _premios.FirstOrDefault(p => p.Id == premioId)
            ?? throw new ReglaDeNegocioException("Ese premio ya no está en el sorteo.", "Premio");
        _premios.Remove(premio);
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Pone al día el nombre y la descripción de un premio que ha cambiado (por ejemplo, se ha corregido un producto de
    /// sorteo). Solo mientras el sorteo está abierto: los realizados y cancelados conservan lo que se sorteó.
    /// </summary>
    public bool ActualizarPremio(DatosPremio datos, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (!Abierto)
            return false;
        var cambiado = false;
        foreach (var premio in _premios.Where(p => p.Es(datos.Tipo, datos.Id)))
            cambiado |= premio.Actualizar(datos);
        if (cambiado)
            ActualizadoEn = ahora;
        return cambiado;
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
    /// elige al ganador en el orden en que se apuntaron. Hacen falta al menos un premio y dos participantes.
    /// </summary>
    public ExtraccionSorteo Sortear(Func<int, int> azar, DateTime ahora, int? administradorId)
    {
        ComprobarAbierto("sortearlo");
        ComprobarPremios();
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
        ComprobarSinEntregar();
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
        ComprobarSinEntregar();
        if (Abierto)
            ComprobarPremios();
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

    /// <summary>Anota que el ganador ya tiene su premio. Desde entonces el ganador no cambia.</summary>
    public void MarcarEntregado(DateTime ahora)
    {
        var ganador = Ganador ?? throw new ReglaDeNegocioException("Aún no hay ganador al que entregar el premio.");
        if (ganador.EntregadoEn is { } entregado)
            throw new ReglaDeNegocioException($"El premio ya se entregó el {entregado:dd/MM/yyyy}.");
        ganador.Entregar(ahora);
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

    private void ComprobarPremios()
    {
        if (_premios.Count == 0)
            throw new ReglaDeNegocioException("Añade al menos un premio antes de sortear.", "Premio");
    }

    /// <summary>Entregado el premio, el ganador ya no cambia.</summary>
    private void ComprobarSinEntregar()
    {
        if (Ganador is { EntregadoEn: { } entregado } ganador)
            throw new ReglaDeNegocioException($"El premio ya se entregó a {ganador.Ganador} el {entregado:dd/MM/yyyy}: el ganador ya no cambia.");
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

    private void Aplicar(DatosSorteo datos, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        var titulo = Texto.Obligatorio(datos.Titulo, LongitudTitulo, "El título", "Titulo");
        // Se publica en la web: como todo lo nuevo, en los dos idiomas
        var tituloEn = Texto.Obligatorio(datos.TituloEn, LongitudTitulo, "El título en inglés", "TituloEn");
        var descripcion = Texto.Opcional(datos.Descripcion);
        var descripcionEn = Texto.Opcional(datos.DescripcionEn);
        if (descripcion?.Length > LongitudDescripcion || descripcionEn?.Length > LongitudDescripcion)
            throw new ReglaDeNegocioException($"La descripción admite hasta {LongitudDescripcion} caracteres.", "Descripcion");
        if (descripcion is not null && descripcionEn is null)
            throw new ReglaDeNegocioException("Escribe también la descripción en inglés.", "DescripcionEn");
        if (datos.FechaSorteo.Year < 2000)
            throw new ReglaDeNegocioException("Indica cuándo se sortea.", "FechaSorteo");

        Titulo = titulo;
        TituloEn = tituloEn;
        Descripcion = descripcion;
        DescripcionEn = descripcionEn;
        FechaSorteo = datos.FechaSorteo.AddSeconds(-datos.FechaSorteo.Second).AddMilliseconds(-datos.FechaSorteo.Millisecond);
        ActualizadoEn = ahora;
    }
}

/// <summary>
/// Un premio del sorteo: un servicio del catálogo, un producto de la tienda o un producto de sorteo, con su cantidad y sus
/// textos del momento. Si luego se borra o cambia el servicio o el producto, el premio conserva lo que se sorteó.
/// </summary>
public sealed class PremioSorteo
{
    public const int LongitudNombre = 120;
    public const int LongitudDescripcion = 1000;
    public const int CantidadMaxima = 99;

    private PremioSorteo()
    {
    }

    internal PremioSorteo(Sorteo sorteo, DatosPremio datos, int cantidad)
    {
        if (!Enum.IsDefined(datos.Tipo))
            throw new ReglaDeNegocioException("Elige el premio: un servicio, un producto de la tienda o un producto de sorteo.", "Premio");
        Sorteo = sorteo;
        Tipo = datos.Tipo;
        ServicioId = datos.Tipo == TipoPremio.Servicio ? datos.Id : null;
        ProductoId = datos.Tipo == TipoPremio.Producto ? datos.Id : null;
        ProductoSorteoId = datos.Tipo == TipoPremio.ProductoSorteo ? datos.Id : null;
        Cantidad = cantidad;
        Actualizar(datos);
    }

    public int Id { get; private set; }

    public int SorteoId { get; private set; }

    public Sorteo? Sorteo { get; private set; }

    public TipoPremio Tipo { get; private set; }

    /// <summary>El servicio del catálogo, si sigue existiendo.</summary>
    public int? ServicioId { get; private set; }

    /// <summary>El producto de la tienda, si sigue existiendo.</summary>
    public int? ProductoId { get; private set; }

    /// <summary>El producto de sorteo, si sigue existiendo.</summary>
    public int? ProductoSorteoId { get; private set; }

    /// <summary>Nombre del momento: no cambia aunque cambie o se borre el servicio o el producto (salvo en sorteos abiertos).</summary>
    public string Nombre { get; private set; } = string.Empty;

    public string? NombreEn { get; private set; }

    public string? Descripcion { get; private set; }

    public string? DescripcionEn { get; private set; }

    public int Cantidad { get; private set; }

    /// <summary>El número del servicio o producto del premio; null si ya no existe.</summary>
    public int? ArticuloId => Tipo switch
    {
        TipoPremio.Servicio => ServicioId,
        TipoPremio.Producto => ProductoId,
        _ => ProductoSorteoId,
    };

    public string NombreEnIdioma(bool ingles) => ingles && NombreEn is { Length: > 0 } ? NombreEn : Nombre;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;

    /// <summary>«2 × Champú» o «Champú».</summary>
    public string TextoEnIdioma(bool ingles) => Cantidad > 1 ? $"{Cantidad} × {NombreEnIdioma(ingles)}" : NombreEnIdioma(ingles);

    /// <summary>
    /// Une los premios como se diría: «Corte, Champú y Cena para dos» o, en inglés, «Cut, Shampoo and Dinner for two».
    /// En español, «e» delante de palabra que empieza por el sonido «i» («Corte e Hidratación»).
    /// </summary>
    public static string Unir(IEnumerable<string> partes, bool ingles)
    {
        var lista = partes.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (lista.Count < 2)
            return lista.FirstOrDefault() ?? string.Empty;
        var ultimo = lista[^1];
        var y = ingles ? "and" : EmpiezaPorI(ultimo) ? "e" : "y";
        return $"{string.Join(", ", lista.Take(lista.Count - 1))} {y} {ultimo}";
    }

    internal bool Es(TipoPremio tipo, int id) => Tipo == tipo && ArticuloId == id;

    internal void Sumar(int cantidad)
    {
        if (Cantidad + cantidad > CantidadMaxima)
            throw new ReglaDeNegocioException($"«{Nombre}» ya está en el sorteo ({Cantidad}); en total no pueden ser más de {CantidadMaxima}.", "Cantidad");
        Cantidad += cantidad;
    }

    /// <summary>Copia el nombre y la descripción del momento. Devuelve si ha cambiado algo.</summary>
    internal bool Actualizar(DatosPremio datos)
    {
        var nombre = Comun.Texto.Obligatorio(datos.Nombre, LongitudNombre, "El nombre del premio", "Premio");
        var nombreEn = Recortar(Comun.Texto.Opcional(datos.NombreEn), LongitudNombre);
        var descripcion = Recortar(Comun.Texto.Opcional(datos.Descripcion), LongitudDescripcion);
        var descripcionEn = Recortar(Comun.Texto.Opcional(datos.DescripcionEn), LongitudDescripcion);
        var cambiado = nombre != Nombre || nombreEn != NombreEn || descripcion != Descripcion || descripcionEn != DescripcionEn;
        Nombre = nombre;
        NombreEn = nombreEn;
        Descripcion = descripcion;
        DescripcionEn = descripcionEn;
        return cambiado;
    }

    private static string? Recortar(string? texto, int maximo) => texto?.Length > maximo ? texto[..maximo] : texto;

    /// <summary>
    /// «i…», «í…», «hi…» o «hí…», salvo «hia…», «hie…» o «hio…» (hiato, hielo), que suenan «y». Con cantidad («2 × Iluminador»)
    /// lo que sigue es el número, así que va «y».
    /// </summary>
    private static bool EmpiezaPorI(string texto)
    {
        var palabra = texto.TrimStart().ToLowerInvariant();
        if (palabra.StartsWith('i') || palabra.StartsWith('í'))
            return true;
        return (palabra.StartsWith("hi", StringComparison.Ordinal) || palabra.StartsWith("hí", StringComparison.Ordinal))
            && !(palabra.Length > 2 && "aeoáéó".Contains(palabra[2], StringComparison.Ordinal));
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
/// si hubo una anterior, si se le avisó y por dónde, y cuándo recibió el premio. Guarda el nombre del ganador del momento.
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

    /// <summary>Cuándo recibió el premio. Null mientras no se le ha dado.</summary>
    public DateTime? EntregadoEn { get; private set; }

    internal void Avisar(CanalConfirmacion canal, DateTime ahora)
    {
        AvisoCanal = canal;
        AvisadoEn = ahora;
    }

    internal void Entregar(DateTime ahora) => EntregadoEn = ahora;
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
