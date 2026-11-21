using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Comun;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Sorteos;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Publico;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Sorteos entre clientes: premios (servicios, productos de la tienda y productos de sorteo), participantes, extracción al
/// azar (o elección del salón), aviso al ganador por correo o WhatsApp, entrega del premio, anuncios en la web e historial.
/// </summary>
public sealed class SorteosController(
    SorteosService sorteos,
    ProductosSorteoService productosSorteo,
    CatalogoService catalogo,
    ProductosService productos,
    SorteosPublicos publicos) : AdminController
{
    private const string Revelar = "RevelarGanador";
    private const string AvisoConcurrencia = "El sorteo acaba de cambiar en otra pestaña y no se ha guardado nada. Revisa cómo ha quedado.";

    [HttpGet]
    public async Task<IActionResult> Index(VistaSorteos vista = VistaSorteos.Activos, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(vista))
            vista = VistaSorteos.Activos;
        return View(new SorteosIndiceModelo(vista, await sorteos.ListarAsync(vista == VistaSorteos.Historial, ct), sorteos.Hoy));
    }

    [HttpGet]
    public IActionResult Nuevo()
    {
        // Por defecto, dentro de una semana a las 18:00
        var formulario = new SorteoFormulario { FechaSorteo = sorteos.Hoy.AddDays(7).ToDateTime(new TimeOnly(18, 0)) };
        return View("Edicion", new SorteoEdicionModelo(null, formulario));
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(SorteoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await sorteos.CrearAsync(formulario.Datos(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Sorteo creado. Añade los premios y a los participantes y, si quieres, un anuncio para la web.");
                return Redirect(Url.Action(nameof(Detalle), new { id = resultado.Valor }) + "#premios");
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", new SorteoEdicionModelo(null, formulario));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return NotFound();
        if (!sorteo.Abierto)
        {
            this.AvisarError("Un sorteo realizado o cancelado ya no se cambia.");
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return View("Edicion", new SorteoEdicionModelo(id, SorteoFormulario.Desde(sorteo)));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, SorteoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await ProtegerAsync(() => sorteos.EditarAsync(id, formulario.Datos(), ct));
            if (resultado.Exito)
            {
                publicos.Invalidar();
                this.AvisarExito("Cambios guardados.");
                return RedirectToAction(nameof(Detalle), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return await sorteos.ObtenerAsync(id, ct) is null ? NotFound() : View("Edicion", new SorteoEdicionModelo(id, formulario));
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, CancellationToken ct)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return NotFound();

        // El anuncio que se propone: desde hoy hasta el día del sorteo (o una semana, si ya pasó)
        var diaSorteo = DateOnly.FromDateTime(sorteo.FechaSorteo);
        var anuncio = new AnuncioFormulario { DesdeAnuncio = sorteos.Hoy, HastaAnuncio = diaSorteo >= sorteos.Hoy ? diaSorteo : sorteos.Hoy.AddDays(7) };
        return View(await ModeloDetalleAsync(sorteo, anuncio, null, TempData[Revelar] is true, ct));
    }

    /// <summary>Un premio más: servicio, producto de la tienda o producto de sorteo (si ya estaba, suma la cantidad).</summary>
    [HttpPost]
    public async Task<IActionResult> AgregarPremio(int id, string? premio, int? cantidad, CancellationToken ct)
    {
        var elegido = ClavePremio.Leer(premio);
        var resultado = elegido is null
            ? new Error("Elige el premio de la lista.", "Premio")
            : await ProtegerAsync(() => sorteos.AgregarPremioAsync(id, elegido, cantidad ?? 1, ct));
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Premio añadido.", "premios");
    }

    [HttpPost]
    public async Task<IActionResult> QuitarPremio(int id, int premioId, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.QuitarPremioAsync(id, premioId, ct));
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Premio quitado.", "premios");
    }

    /// <summary>Un cliente elegido con el buscador, con el número escrito o, sin número, con el siguiente libre.</summary>
    [HttpPost]
    public async Task<IActionResult> AgregarParticipante(int id, int? clienteId, int? numero, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(async () => await sorteos.AgregarClienteAsync(id, clienteId, numero, ct));
        return Volver(id, resultado, resultado is Resultado<bool> { Valor: false } ? "Ese cliente ya participaba." : "Participante añadido.", "participantes");
    }

    /// <summary>Números escritos a mano, sin ficha: «1-50», «51 María», «60, 61 Juan»…</summary>
    [HttpPost]
    public async Task<IActionResult> AgregarNumeros(int id, string? numeros, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(async () => await sorteos.AgregarNumerosAsync(id, numeros, ct));
        if (!resultado.Exito)
            return await DetalleConErrorAsync(id, resultado.Error!, null, null, ct, numeros);

        var agregados = ((Resultado<Domain.Sorteos.NumerosAgregados>)resultado).Valor!;
        return Volver(id, resultado, FormatoSorteos.NumerosAgregados(agregados), "participantes");
    }

    /// <summary>Cambia el número de un participante y, en los números sin ficha, el nombre de quien lo tiene.</summary>
    [HttpPost]
    public async Task<IActionResult> CambiarNumero(int id, int numero, int? nuevo, string? nombre, CancellationToken ct) =>
        Volver(id, await ProtegerAsync(() => sorteos.CambiarNumeroAsync(id, numero, nuevo, nombre, ct)), "Número cambiado.", "participantes");

    /// <summary>Todos los clientes que vinieron entre dos fechas.</summary>
    [HttpPost]
    public async Task<IActionResult> AgregarVisitas(int id, DateOnly? desdeVisitas, DateOnly? hastaVisitas, CancellationToken ct)
    {
        if (desdeVisitas is null || hastaVisitas is null)
        {
            this.AvisarError("Indica entre qué fechas buscar las visitas.");
            return Redirect(Url.Action(nameof(Detalle), new { id }) + "#participantes");
        }

        var resultado = await ProtegerAsync(async () => await sorteos.AgregarClientesConVisitasAsync(id, desdeVisitas.Value, hastaVisitas.Value, ct));
        var nuevos = (resultado as Resultado<int>)?.Valor ?? 0;
        return Volver(id, resultado, nuevos switch
        {
            0 => "Esos clientes ya participaban todos.",
            1 => "Se ha añadido 1 participante.",
            _ => $"Se han añadido {nuevos} participantes.",
        }, "participantes");
    }

    [HttpPost]
    public async Task<IActionResult> QuitarParticipante(int id, int numero, CancellationToken ct) =>
        Volver(id, await ProtegerAsync(() => sorteos.QuitarParticipanteAsync(id, numero, ct)), "Participante quitado.", "participantes");

    /// <summary>Sortea al azar y vuelve a la pantalla con la animación que descubre al ganador.</summary>
    [HttpPost]
    public async Task<IActionResult> Sortear(int id, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.SortearAsync(id, AdministradorId, ct));
        if (resultado.Exito)
        {
            publicos.Invalidar();
            TempData[Revelar] = true;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return Volver(id, resultado, "", null);
    }

    [HttpPost]
    public async Task<IActionResult> Repetir(int id, string? motivo, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.RepetirAsync(id, motivo, AdministradorId, ct));
        if (resultado.Exito)
        {
            TempData[Revelar] = true;
            return RedirectToAction(nameof(Detalle), new { id });
        }

        return Volver(id, resultado, "", "ganador");
    }

    /// <summary>El salón elige al ganador por su número (por ejemplo, el que ha salido en un sorteo hecho en otro sitio).</summary>
    [HttpPost]
    public async Task<IActionResult> ElegirGanador(int id, int? numero, string? motivo, CancellationToken ct)
    {
        var resultado = numero is { } elegido
            ? await ProtegerAsync(() => sorteos.ElegirGanadorAsync(id, elegido, motivo, AdministradorId, ct))
            : new Error("Elige a uno de los participantes.");
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Ganador elegido. Ya puedes avisarle.", "ganador");
    }

    [HttpPost]
    public async Task<IActionResult> MarcarEntregado(int id, CancellationToken ct) =>
        Volver(id, await ProtegerAsync(() => sorteos.MarcarEntregadoAsync(id, ct)), "Anotado: el ganador ya tiene su premio.", "ganador");

    [HttpPost]
    public async Task<IActionResult> AvisarCorreo(int id, string? textoAviso, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.AvisarPorCorreoAsync(id, textoAviso, ct));
        if (resultado.Exito)
        {
            this.AvisarExito("Correo enviado al ganador.");
            return Redirect(Url.Action(nameof(Detalle), new { id }) + "#ganador");
        }

        return await DetalleConErrorAsync(id, resultado.Error!, null, textoAviso, ct);
    }

    /// <summary>Anota el aviso y abre WhatsApp con el mensaje preparado (lo envía quien lo abre).</summary>
    [HttpPost]
    public async Task<IActionResult> AvisarWhatsApp(int id, string? textoAviso, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(async () => await sorteos.AvisarPorWhatsAppAsync(id, textoAviso, ct));
        if (resultado is Resultado<Uri> { Exito: true, Valor: { } enlace })
            return Redirect(enlace.AbsoluteUri);

        return await DetalleConErrorAsync(id, resultado.Error!, null, textoAviso, ct);
    }

    [HttpPost]
    public async Task<IActionResult> Anunciar(int id, AnuncioFormulario anuncio, CancellationToken ct)
    {
        var resultado = ModelState.IsValid
            ? await ProtegerAsync(() => sorteos.AnunciarAsync(id, anuncio.TextoAnuncio, anuncio.TextoAnuncioEn, anuncio.DesdeAnuncio, anuncio.HastaAnuncio, ct))
            : new Error(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        if (resultado.Exito)
        {
            publicos.Invalidar();
            this.AvisarExito("Anuncio guardado. La web lo enseña entre esas fechas.");
            return Redirect(Url.Action(nameof(Detalle), new { id }) + "#anuncios");
        }

        return await DetalleConErrorAsync(id, resultado.Error!, anuncio, null, ct);
    }

    [HttpPost]
    public async Task<IActionResult> QuitarAnuncio(int id, int anuncioId, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.QuitarAnuncioAsync(id, anuncioId, ct));
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Anuncio borrado del historial.", "anuncios");
    }

    /// <summary>Cambia el texto o las fechas del anuncio que está en la web (o programado).</summary>
    [HttpPost]
    public async Task<IActionResult> CambiarAnuncio(int id, int anuncioId, AnuncioFormulario anuncio, CancellationToken ct)
    {
        var resultado = ModelState.IsValid
            ? await ProtegerAsync(() => sorteos.CambiarAnuncioAsync(id, anuncioId, anuncio.TextoAnuncio, anuncio.TextoAnuncioEn, anuncio.DesdeAnuncio, anuncio.HastaAnuncio, ct))
            : new Error(ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage);
        if (resultado.Exito)
        {
            publicos.Invalidar();
            this.AvisarExito("Anuncio cambiado. La web ya enseña el texto y las fechas nuevos.");
            return Redirect(Url.Action(nameof(Detalle), new { id }) + "#anuncios");
        }

        anuncio.AnuncioId = anuncioId;
        return await DetalleConErrorAsync(id, resultado.Error!, anuncio, null, ct);
    }

    /// <summary>Deja de enseñar el anuncio en la web desde ahora (queda en el historial si ya se había visto).</summary>
    [HttpPost]
    public async Task<IActionResult> TerminarAnuncio(int id, int anuncioId, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.TerminarAnuncioAsync(id, anuncioId, ct));
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Listo: la web ya no enseña este sorteo. Puedes volver a anunciarlo cuando quieras.", "anuncios");
    }

    /// <summary>Tablero con todos los números del sorteo: libres, ocupados (y por quién) y los que han salido.</summary>
    [HttpGet]
    public async Task<IActionResult> Tablero(int id, CancellationToken ct) =>
        await sorteos.ObtenerAsync(id, ct) is { } sorteo ? View(new SorteoTableroModelo(sorteo, sorteos.Hoy)) : NotFound();

    [HttpPost]
    public async Task<IActionResult> Talonario(int id, int? hasta, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.CambiarTalonarioAsync(id, hasta, ct));
        if (resultado.Exito)
            this.AvisarExito(hasta is { } n ? $"Talonario guardado: del 1 al {n}." : "Talonario quitado: el tablero llega hasta el número más alto que haya.");
        else
            this.AvisarError(resultado.Error!.Mensaje);
        return RedirectToAction(nameof(Tablero), new { id });
    }

    /// <summary>Apunta un número libre desde el tablero, con el nombre de quien lo tiene (opcional).</summary>
    [HttpPost]
    public async Task<IActionResult> ApuntarNumero(int id, int numero, string? nombre, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(async () => await sorteos.AgregarNumerosAsync(id, $"{numero} {nombre?.Trim()}".Trim(), ct));
        if (!resultado.Exito)
            this.AvisarError(resultado.Error!.Mensaje);
        else if (((Resultado<Domain.Sorteos.NumerosAgregados>)resultado).Valor!.Agregados == 0)
            this.AvisarError($"El número {numero} ya lo tiene alguien.");
        else
            this.AvisarExito(string.IsNullOrWhiteSpace(nombre) ? $"Número {numero} apuntado." : $"Número {numero} apuntado para {nombre.Trim()}.");
        return Redirect(Url.Action(nameof(Tablero), new { id }) + $"#n-{numero}");
    }

    [HttpPost]
    public async Task<IActionResult> Cancelar(int id, string? motivo, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(() => sorteos.CancelarAsync(id, motivo, ct));
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Sorteo cancelado. Queda en el historial y la web deja de anunciarlo.", null);
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var resultado = await sorteos.EliminarAsync(id, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Detalle), new { id });
        }

        publicos.Invalidar();
        this.AvisarExito("Sorteo eliminado.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Vuelve a la pantalla del sorteo (a su apartado) con el aviso del resultado.</summary>
    private RedirectResult Volver(int id, Resultado resultado, string exito, string? apartado)
    {
        if (resultado.Exito)
            this.AvisarExito(exito);
        else
            this.AvisarError(resultado.Error!.Mensaje);
        return Redirect(Url.Action(nameof(Detalle), new { id }) + (apartado is null ? "" : "#" + apartado));
    }

    /// <summary>Si otra pestaña ha cambiado el sorteo a la vez (por ejemplo, ha sorteado), no se guarda nada.</summary>
    private static async Task<Resultado> ProtegerAsync(Func<Task<Resultado>> accion)
    {
        try
        {
            return await accion();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new Error(AvisoConcurrencia);
        }
    }

    /// <param name="numeros">Los números que se estaban escribiendo, para no perderlos.</param>
    private async Task<IActionResult> DetalleConErrorAsync(int id, Error error, AnuncioFormulario? anuncio, string? textoAviso, CancellationToken ct, string? numeros = null)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return NotFound();
        ModelState.Clear();
        ModelState.AddModelError(error.Campo ?? string.Empty, error.Mensaje);
        var modelo = await ModeloDetalleAsync(sorteo, anuncio ?? new AnuncioFormulario { DesdeAnuncio = sorteos.Hoy }, textoAviso, false, ct);
        return View(nameof(Detalle), modelo with { TextoNumeros = numeros });
    }

    private async Task<SorteoDetalleModelo> ModeloDetalleAsync(Domain.Sorteos.Sorteo sorteo, AnuncioFormulario anuncio, string? textoAviso, bool revelar, CancellationToken ct)
    {
        // Secuencial: el contexto de base de datos no admite consultas simultáneas
        var aviso = await sorteos.AvisoAsync(sorteo, ct);
        OpcionesPremio? opciones = null;
        if (sorteo.Abierto)
        {
            var productosSorteoActivos = await productosSorteo.ActivosAsync(ct);
            var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
            var tienda = await productos.ParaVentaAsync(ct);
            opciones = new OpcionesPremio(productosSorteoActivos, servicios, tienda);
        }

        return new SorteoDetalleModelo(sorteo, sorteos.Hoy, aviso, sorteos.CorreoConfigurado, anuncio, opciones, textoAviso, revelar);
    }
}
