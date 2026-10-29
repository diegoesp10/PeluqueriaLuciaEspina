using System.Globalization;
using System.Text.RegularExpressions;
using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Sorteos;

namespace PeluqueriaLucia.Application.Sorteos;

/// <summary>Sorteos guardados. El sorteo se carga con sus participantes (y sus fichas), sus extracciones y sus anuncios.</summary>
public interface ISorteoRepositorio
{
    Task<Sorteo?> ObtenerAsync(int id, CancellationToken ct = default);

    /// <summary>Los abiertos (por fecha del sorteo) o el historial: realizados y cancelados, lo último primero.</summary>
    Task<IReadOnlyList<Sorteo>> ListarAsync(bool historial, CancellationToken ct = default);

    /// <summary>Anuncios que la web enseña ese día, de sorteos no cancelados, por fecha del sorteo.</summary>
    Task<IReadOnlyList<AnuncioPublico>> AnunciosVisiblesAsync(DateOnly dia, CancellationToken ct = default);

    Task<IReadOnlyList<Cliente>> ClientesAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Clientes con alguna visita entre esas fechas (incluidas).</summary>
    Task<IReadOnlyList<Cliente>> ClientesConVisitasAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default);

    void Agregar(Sorteo sorteo);

    void Eliminar(Sorteo sorteo);
}

/// <summary>Números al azar para sortear. La implementación usa un generador criptográfico: nadie puede predecir al ganador.</summary>
public interface IAzar
{
    /// <summary>Un número entre 0 y <paramref name="maximo"/> (sin incluirlo), todos con la misma probabilidad.</summary>
    int Siguiente(int maximo);
}

/// <summary>Un anuncio de sorteo tal como lo enseña la web (sin datos de los participantes ni del ganador).</summary>
public sealed record AnuncioPublico(int AnuncioId, int SorteoId, string Titulo, string? TituloEn, string? Descripcion, string? DescripcionEn,
    string Premio, string? PremioEn, TipoPremio TipoPremio, DateTime FechaSorteo, bool Realizado, string Texto, string? TextoEn, DateOnly Hasta)
{
    public string TituloEnIdioma(bool ingles) => ingles && TituloEn is { Length: > 0 } ? TituloEn : Titulo;

    public string? DescripcionEnIdioma(bool ingles) => ingles && DescripcionEn is { Length: > 0 } ? DescripcionEn : Descripcion;

    public string PremioEnIdioma(bool ingles) => ingles && PremioEn is { Length: > 0 } ? PremioEn : Premio;

    public string TextoEnIdioma(bool ingles) => ingles && TextoEn is { Length: > 0 } ? TextoEn : Texto;
}

/// <summary>El premio elegido en el formulario: un servicio o un producto, por su número.</summary>
public sealed record PremioElegido(TipoPremio Tipo, int Id);

/// <summary>Cómo avisar al ganador: los canales que tiene su ficha y el texto que se propone.</summary>
public sealed record AvisoGanador(string Texto, string? Correo, string? WhatsApp);

public sealed class SorteosService(
    ISorteoRepositorio sorteos,
    IServicioRepositorio servicios,
    IProductoRepositorio productos,
    IUnidadDeTrabajo unidadDeTrabajo,
    IAzar azar,
    IEnviadorCorreo correo,
    IWhatsAppManual whatsApp,
    TimeProvider reloj,
    IDatosSalon? salon = null)
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Hora del salón, aunque el servidor esté en otra zona.</summary>
    private DateTime Ahora => TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), AgendaService.Zona).DateTime;

    public DateOnly Hoy => DateOnly.FromDateTime(Ahora);

    public bool CorreoConfigurado => correo.Configurado;

    public Task<IReadOnlyList<Sorteo>> ListarAsync(bool historial, CancellationToken ct = default) => sorteos.ListarAsync(historial, ct);

    public Task<Sorteo?> ObtenerAsync(int id, CancellationToken ct = default) => sorteos.ObtenerAsync(id, ct);

    /// <summary>Lo que la web enseña hoy.</summary>
    public Task<IReadOnlyList<AnuncioPublico>> AnunciosVisiblesAsync(CancellationToken ct = default) => sorteos.AnunciosVisiblesAsync(Hoy, ct);

    public Task<Resultado<int>> CrearAsync(DatosSorteo datos, PremioElegido? premio, CancellationToken ct = default) =>
        Resultado.ProtegerAsync<int>(async () =>
        {
            var sorteo = Sorteo.Crear(datos, await PremioAsync(premio, ct), Ahora);
            sorteos.Agregar(sorteo);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(sorteo.Id);
        });

    public Task<Resultado> EditarAsync(int id, DatosSorteo datos, PremioElegido? premio, CancellationToken ct = default) =>
        CambiarAsync(id, async s => s.Editar(datos, await PremioAsync(premio, ct), Ahora), ct);

    /// <summary>Añade clientes elegidos uno a uno. Devuelve cuántos son nuevos en el sorteo.</summary>
    public async Task<Resultado<int>> AgregarParticipantesAsync(int id, IReadOnlyCollection<int> clienteIds, CancellationToken ct = default)
    {
        var clientes = clienteIds.Count == 0 ? [] : await sorteos.ClientesAsync(clienteIds.Distinct().ToList(), ct);
        if (clientes.Count == 0)
            return new Error("Elige un cliente de la lista.", "ClienteId");
        return await AgregarAsync(id, clientes, ct);
    }

    /// <summary>Añade a todos los clientes que vinieron entre esas fechas. Devuelve cuántos son nuevos en el sorteo.</summary>
    public async Task<Resultado<int>> AgregarClientesConVisitasAsync(int id, DateOnly desde, DateOnly hasta, CancellationToken ct = default)
    {
        if (hasta < desde)
            return new Error("La fecha de fin tiene que ser posterior a la de inicio.", "HastaVisitas");
        var clientes = await sorteos.ClientesConVisitasAsync(desde, hasta, ct);
        if (clientes.Count == 0)
            return new Error($"No hubo visitas con cliente entre el {desde:dd/MM/yyyy} y el {hasta:dd/MM/yyyy}.", "DesdeVisitas");
        return await AgregarAsync(id, clientes, ct);
    }

    public Task<Resultado> QuitarParticipanteAsync(int id, int clienteId, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.QuitarParticipante(clienteId, Ahora), ct);

    /// <summary>
    /// Sortea al azar entre los participantes. Si otra pestaña sortea a la vez, el guardado falla por concurrencia y no se
    /// guarda nada (quien llama lo avisa).
    /// </summary>
    public Task<Resultado> SortearAsync(int id, int? administradorId, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.Sortear(azar.Siguiente, Ahora, administradorId), ct);

    public Task<Resultado> RepetirAsync(int id, string? motivo, int? administradorId, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.Repetir(azar.Siguiente, motivo, Ahora, administradorId), ct);

    public Task<Resultado> ElegirGanadorAsync(int id, int clienteId, string? motivo, int? administradorId, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.ElegirGanador(clienteId, motivo, Ahora, administradorId), ct);

    public Task<Resultado> CancelarAsync(int id, string? motivo, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.Cancelar(motivo, Ahora), ct);

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return Error.NoEncontrado("El sorteo");
        return await Resultado.ProtegerAsync(async () =>
        {
            sorteo.ComprobarQueSePuedeEliminar();
            sorteos.Eliminar(sorteo);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    public Task<Resultado> AnunciarAsync(int id, string? texto, string? textoEn, DateOnly? desde, DateOnly? hasta, CancellationToken ct = default) =>
        desde is null || hasta is null
            ? Task.FromResult<Resultado>(new Error("Indica entre qué fechas se ve el anuncio en la web.", desde is null ? "DesdeAnuncio" : "HastaAnuncio"))
            : CambiarAsync(id, s => s.Anunciar(texto, textoEn, desde.Value, hasta.Value, Ahora), ct);

    public Task<Resultado> QuitarAnuncioAsync(int id, int anuncioId, CancellationToken ct = default) =>
        CambiarAsync(id, s => s.QuitarAnuncio(anuncioId, Ahora), ct);

    /// <summary>El texto que se propone para avisar al ganador y por dónde se le puede avisar. Null si aún no hay ganador.</summary>
    public async Task<AvisoGanador?> AvisoAsync(Sorteo sorteo, CancellationToken ct = default)
    {
        if (sorteo.Ganador is not { } ganador)
            return null;
        var ficha = sorteo.Participantes.FirstOrDefault(p => p.ClienteId == ganador.ClienteId)?.Cliente;
        var datos = salon is null ? null : await salon.ObtenerAsync(ct);
        var nombreSalon = datos?.Nombre is { Length: > 0 } n ? n : "la peluquería";
        var llamar = datos is { Telefonos.Count: > 0 } ? $" Llámanos al {datos.TelefonosEnTexto} para acordar cuándo te viene bien." : " Pásate por el salón cuando te venga bien.";
        var texto = $"¡Enhorabuena, {ficha?.Nombre ?? ganador.Ganador}! Has ganado {sorteo.Premio} en el sorteo «{sorteo.Titulo}» de {nombreSalon}.{llamar} ¡Te esperamos!";
        return new AvisoGanador(texto, AgendaService.NormalizarCorreo(ficha?.Email), TelefonoWhatsApp(ficha?.Telefono));
    }

    /// <summary>Envía el aviso al ganador por correo, con la imagen del salón, y lo anota.</summary>
    public async Task<Resultado> AvisarPorCorreoAsync(int id, string? texto, CancellationToken ct = default)
    {
        if (!correo.Configurado)
            return new Error("El correo no está configurado todavía. Revisa la configuración SMTP del servidor.");
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return Error.NoEncontrado("El sorteo");
        if (await AvisoAsync(sorteo, ct) is not { } aviso)
            return new Error("Aún no hay ganador al que avisar.");
        if (aviso.Correo is not { } destinatario)
            return new Error("La ficha del ganador no tiene un correo válido. Avísale por WhatsApp o corrige su ficha.");
        if (TextoValido(texto) is not { } mensaje)
            return new Error("Escribe el mensaje para el ganador (entre 10 y 2000 caracteres).", "TextoAviso");

        var datos = salon is null ? null : await salon.ObtenerAsync(ct);
        var envio = await correo.EnviarAsync(PlantillaCorreoCita.ComponerPremio(destinatario, mensaje, sorteo.Premio, datos), ct);
        if (envio == ResultadoEnvio.Rechazado)
            return new Error("El servidor de correo ha rechazado el envío. Revisa la dirección o la configuración SMTP y vuelve a intentarlo.");
        // Incierto: puede que haya salido; se anota igual para no mandarlo dos veces sin querer
        sorteo.MarcarAvisado(CanalConfirmacion.Correo, Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return envio == ResultadoEnvio.Aceptado ? Resultado.Ok()
            : new Error("No se sabe si el correo llegó a salir. Se ha anotado como avisado: compruébalo antes de volver a enviarlo.");
    }

    /// <summary>
    /// Prepara el mensaje de WhatsApp para el ganador (el enlace abre WhatsApp con el texto; lo envía quien lo abre) y anota
    /// que se le ha avisado por ahí.
    /// </summary>
    public async Task<Resultado<Uri>> AvisarPorWhatsAppAsync(int id, string? texto, CancellationToken ct = default)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return Error.NoEncontrado("El sorteo");
        if (await AvisoAsync(sorteo, ct) is not { } aviso)
            return new Error("Aún no hay ganador al que avisar.");
        if (aviso.WhatsApp is not { } telefono)
            return new Error("La ficha del ganador no tiene un móvil válido para WhatsApp. Avísale por correo o corrige su ficha.");
        if (TextoValido(texto) is not { } mensaje)
            return new Error("Escribe el mensaje para el ganador (entre 10 y 2000 caracteres).", "TextoAviso");

        var enlace = whatsApp.Preparar(telefono, mensaje);
        sorteo.MarcarAvisado(CanalConfirmacion.WhatsApp, Ahora);
        await unidadDeTrabajo.GuardarCambiosAsync(ct);
        return Resultado.Ok(enlace);
    }

    /// <summary>Texto para el historial: «Al azar entre 12» o «Elegido por el salón».</summary>
    public static string Como(ExtraccionSorteo e) => e.Forma == FormaGanador.AlAzar
        ? $"Al azar entre {e.Participantes} {(e.Participantes == 1 ? "participante" : "participantes")}"
        : "Elegido por el salón";

    /// <summary>«15 de noviembre de 2026 a las 18:00».</summary>
    public static string Cuando(DateTime fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy 'a las' HH:mm", Es);

    private async Task<Resultado<int>> AgregarAsync(int id, IReadOnlyList<Cliente> clientes, CancellationToken ct)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return Error.NoEncontrado("El sorteo");
        return await Resultado.ProtegerAsync<int>(async () =>
        {
            var nuevos = sorteo.AgregarParticipantes(clientes, Ahora);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
            return Resultado.Ok(nuevos);
        });
    }

    /// <summary>Carga el sorteo, aplica el cambio y lo guarda; las reglas incumplidas vuelven como error.</summary>
    private async Task<Resultado> CambiarAsync(int id, Func<Sorteo, Task> cambio, CancellationToken ct)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return Error.NoEncontrado("El sorteo");
        return await Resultado.ProtegerAsync(async () =>
        {
            await cambio(sorteo);
            await unidadDeTrabajo.GuardarCambiosAsync(ct);
        });
    }

    private Task<Resultado> CambiarAsync(int id, Action<Sorteo> cambio, CancellationToken ct) =>
        CambiarAsync(id, s =>
        {
            cambio(s);
            return Task.CompletedTask;
        }, ct);

    private async Task<PremioSorteo> PremioAsync(PremioElegido? premio, CancellationToken ct)
    {
        switch (premio?.Tipo)
        {
            case TipoPremio.Servicio when await servicios.ObtenerAsync(premio.Id, ct) is { } servicio:
                if (!servicio.Activo)
                    throw new ReglaDeNegocioException($"«{servicio.Nombre}» está archivado: elige otro premio.", "Premio");
                return PremioSorteo.De(servicio);
            case TipoPremio.Producto when await productos.ObtenerAsync(premio.Id, ct) is { } producto:
                if (!producto.Activo)
                    throw new ReglaDeNegocioException($"«{producto.Nombre}» está archivado: elige otro premio.", "Premio");
                return PremioSorteo.De(producto);
            default:
                throw new ReglaDeNegocioException("Elige el premio: un servicio del catálogo o un producto de la tienda.", "Premio");
        }
    }

    private static string? TextoValido(string? texto) =>
        texto?.Trim() is { Length: >= 10 and <= ComunicacionesCitas.LongitudTexto } limpio ? limpio : null;

    /// <summary>Móvil en formato internacional (+34…), el que necesita WhatsApp; null si no lo es.</summary>
    private static string? TelefonoWhatsApp(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
            return null;
        var limpio = Regex.Replace(telefono, "[ ()-]", "");
        return AgendaService.NormalizarTelefono(limpio) ?? (Regex.IsMatch(limpio, @"^\+[1-9][0-9]{6,14}$") ? limpio : null);
    }
}
