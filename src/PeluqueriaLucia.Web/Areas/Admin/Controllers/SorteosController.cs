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
/// Sorteos de servicios y productos entre clientes: participantes, extracción al azar (o elección del salón), aviso al
/// ganador por correo o WhatsApp, anuncios en la web e historial.
/// </summary>
public sealed class SorteosController(SorteosService sorteos, CatalogoService catalogo, ProductosService productos, SorteosPublicos publicos) : AdminController
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
    public async Task<IActionResult> Nuevo(CancellationToken ct)
    {
        // Por defecto, dentro de una semana a las 18:00
        var formulario = new SorteoFormulario { FechaSorteo = sorteos.Hoy.AddDays(7).ToDateTime(new TimeOnly(18, 0)) };
        return View("Edicion", await ModeloEdicionAsync(null, formulario, null, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(SorteoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await sorteos.CrearAsync(formulario.Datos(), formulario.PremioElegido(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Sorteo creado. Añade los participantes y, si quieres, un anuncio para la web.");
                return RedirectToAction(nameof(Detalle), new { id = resultado.Valor });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", await ModeloEdicionAsync(null, formulario, null, ct));
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

        return View("Edicion", await ModeloEdicionAsync(id, SorteoFormulario.Desde(sorteo), sorteo.Premio, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, SorteoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await ProtegerAsync(() => sorteos.EditarAsync(id, formulario.Datos(), formulario.PremioElegido(), ct));
            if (resultado.Exito)
            {
                publicos.Invalidar();
                this.AvisarExito("Cambios guardados.");
                return RedirectToAction(nameof(Detalle), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        var sorteo = await sorteos.ObtenerAsync(id, ct);
        return sorteo is null ? NotFound() : View("Edicion", await ModeloEdicionAsync(id, formulario, sorteo.Premio, ct));
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

    /// <summary>Un cliente elegido con el buscador.</summary>
    [HttpPost]
    public async Task<IActionResult> AgregarParticipante(int id, int? clienteId, CancellationToken ct)
    {
        var resultado = await ProtegerAsync(async () => await sorteos.AgregarParticipantesAsync(id, clienteId is { } cliente ? [cliente] : [], ct));
        return Volver(id, resultado, resultado is Resultado<int> { Valor: 0 } ? "Ese cliente ya participaba." : "Participante añadido.", "participantes");
    }

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
    public async Task<IActionResult> QuitarParticipante(int id, int clienteId, CancellationToken ct) =>
        Volver(id, await ProtegerAsync(() => sorteos.QuitarParticipanteAsync(id, clienteId, ct)), "Participante quitado.", "participantes");

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

    [HttpPost]
    public async Task<IActionResult> ElegirGanador(int id, int? clienteId, string? motivo, CancellationToken ct)
    {
        var resultado = clienteId is { } cliente
            ? await ProtegerAsync(() => sorteos.ElegirGanadorAsync(id, cliente, motivo, AdministradorId, ct))
            : new Error("Elige a uno de los participantes.");
        if (resultado.Exito)
            publicos.Invalidar();
        return Volver(id, resultado, "Ganador elegido. Ya puedes avisarle.", "ganador");
    }

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
        return Volver(id, resultado, "Anuncio quitado de la web.", "anuncios");
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

    private async Task<IActionResult> DetalleConErrorAsync(int id, Error error, AnuncioFormulario? anuncio, string? textoAviso, CancellationToken ct)
    {
        var sorteo = await sorteos.ObtenerAsync(id, ct);
        if (sorteo is null)
            return NotFound();
        ModelState.Clear();
        ModelState.AddModelError(error.Campo ?? string.Empty, error.Mensaje);
        return View(nameof(Detalle), await ModeloDetalleAsync(sorteo, anuncio ?? new AnuncioFormulario { DesdeAnuncio = sorteos.Hoy }, textoAviso, false, ct));
    }

    private async Task<SorteoDetalleModelo> ModeloDetalleAsync(Domain.Sorteos.Sorteo sorteo, AnuncioFormulario anuncio, string? textoAviso, bool revelar, CancellationToken ct) =>
        new(sorteo, sorteos.Hoy, await sorteos.AvisoAsync(sorteo, ct), sorteos.CorreoConfigurado, anuncio, textoAviso, revelar);

    private async Task<SorteoEdicionModelo> ModeloEdicionAsync(int? id, SorteoFormulario formulario, string? premioActual, CancellationToken ct)
    {
        // Secuencial: el contexto de base de datos no admite consultas simultáneas
        var servicios = await catalogo.ListarAsync(null, incluirArchivados: false, ct);
        var tienda = await productos.ParaVentaAsync(ct);
        return new SorteoEdicionModelo(id, formulario, servicios, tienda, premioActual);
    }
}
