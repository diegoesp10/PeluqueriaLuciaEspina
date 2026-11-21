using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Gastos;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>
/// Gastos del salón (personal, cuota de autónomos, dietas, viajes…): se anotan, se corrigen y se borran desde el listado de
/// visitas, donde salen en su día junto a los cobros. Sus tipos los edita el salón en «Tipos de gasto».
/// </summary>
public sealed class GastosController(GastosService gastos) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Nuevo(DateOnly? fecha, string? volver, CancellationToken ct)
    {
        var ahora = gastos.Ahora;
        var hora = new TimeOnly(ahora.Hour, ahora.Minute);
        var formulario = new GastoFormulario { Fecha = (fecha ?? DateOnly.FromDateTime(ahora)).ToDateTime(hora) };
        return View("Edicion", await ModeloAsync(null, formulario, volver, ct));
    }

    [HttpPost]
    public async Task<IActionResult> Nuevo(GastoFormulario formulario, string? volver, string? accion, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await gastos.CrearAsync(formulario.Datos(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito($"Gasto anotado: {formulario.Concepto?.Trim()}, {Formato.Euros(formulario.Importe ?? 0)}.");
                // «Guardar y anotar otro»: el mismo día y la misma forma de pago, para seguir con el siguiente
                if (accion == "otro")
                    return RedirectToAction(nameof(Nuevo), new { fecha = DateOnly.FromDateTime(formulario.Fecha!.Value).ToString("yyyy-MM-dd"), volver = Local(volver) });
                return Volver(volver, formulario.Fecha!.Value);
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", await ModeloAsync(null, formulario, volver, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, string? volver, CancellationToken ct) =>
        await gastos.ObtenerAsync(id, ct) is { } gasto
            ? View("Edicion", await ModeloAsync(id, GastoFormulario.Desde(gasto), volver, ct))
            : NotFound();

    [HttpPost]
    public async Task<IActionResult> Editar(int id, GastoFormulario formulario, string? volver, CancellationToken ct)
    {
        if (await gastos.ObtenerAsync(id, ct) is not { } gasto)
            return NotFound();
        if (ModelState.IsValid)
        {
            var resultado = await gastos.EditarAsync(id, formulario.Datos(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Gasto guardado.");
                return Volver(volver, formulario.Fecha!.Value);
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", await ModeloAsync(id, formulario, volver, ct, gasto.TipoId));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, string? volver, CancellationToken ct)
    {
        var gasto = await gastos.ObtenerAsync(id, ct);
        if (gasto is null)
            return NotFound();
        var fecha = gasto.Fecha;
        var resultado = await gastos.EliminarAsync(id, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Editar), new { id, volver = Local(volver) });
        }

        this.AvisarExito("Gasto borrado.");
        return Volver(volver, fecha);
    }

    // ── Tipos de gasto ──────────────────────────────────────────────────────

    /// <summary>Los tipos del desplegable: añadir, renombrar, archivar (dejan de salir) y borrar los que no tienen gastos.</summary>
    [HttpGet]
    public async Task<IActionResult> Tipos(int? editar, string? volver, CancellationToken ct) =>
        View(new TiposGastoModelo(await gastos.TiposConUsoAsync(ct), new TipoGastoFormulario(), editar, Local(volver)));

    [HttpPost]
    public async Task<IActionResult> CrearTipo([Bind(Prefix = "Nuevo")] TipoGastoFormulario nuevo, string? volver, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await gastos.CrearTipoAsync(nuevo.Nombre, ct);
            if (resultado.Exito)
            {
                this.AvisarExito($"Tipo «{nuevo.Nombre.Trim()}» añadido. Ya puedes elegirlo al anotar un gasto.");
                return RedirectToAction(nameof(Tipos), new { volver = Local(volver) });
            }

            ModelState.AgregarError(resultado.Error!, "Nuevo.");
        }

        return View(nameof(Tipos), new TiposGastoModelo(await gastos.TiposConUsoAsync(ct), nuevo, null, Local(volver)));
    }

    [HttpPost]
    public async Task<IActionResult> RenombrarTipo(int id, string? nombre, string? volver, CancellationToken ct)
    {
        var resultado = await gastos.RenombrarTipoAsync(id, nombre, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Tipos), null, new { editar = id, volver = Local(volver) }, $"tipo-{id}");
        }

        this.AvisarExito("Tipo renombrado. Los gastos que ya lo llevaban salen con el nombre nuevo, también en el Excel.");
        return RedirectToAction(nameof(Tipos), null, new { volver = Local(volver) }, $"tipo-{id}");
    }

    [HttpPost]
    public async Task<IActionResult> ArchivarTipo(int id, bool archivar, string? volver, CancellationToken ct)
    {
        var resultado = await gastos.ArchivarTipoAsync(id, archivar, ct);
        if (!resultado.Exito)
            this.AvisarError(resultado.Error!.Mensaje);
        else
            this.AvisarExito(archivar
                ? "Tipo archivado: ya no sale al anotar un gasto. Los gastos que lo llevaban lo conservan."
                : "Tipo activado: vuelve a salir al anotar un gasto.");
        return RedirectToAction(nameof(Tipos), null, new { volver = Local(volver) }, $"tipo-{id}");
    }

    [HttpPost]
    public async Task<IActionResult> EliminarTipo(int id, string? volver, CancellationToken ct)
    {
        var resultado = await gastos.EliminarTipoAsync(id, ct);
        if (!resultado.Exito)
            this.AvisarError(resultado.Error!.Mensaje);
        else
            this.AvisarExito("Tipo borrado.");
        return RedirectToAction(nameof(Tipos), new { volver = Local(volver) });
    }

    private async Task<GastoEdicionModelo> ModeloAsync(int? id, GastoFormulario formulario, string? volver, CancellationToken ct, int? tipoGuardado = null) =>
        new(id, formulario, Local(volver), GastoEdicionModelo.Desplegable(await gastos.TiposAsync(ct), tipoGuardado ?? formulario.TipoId));

    /// <summary>A la pantalla de la que se vino o, si no, al día del gasto en el listado de visitas.</summary>
    private IActionResult Volver(string? volver, DateTime fecha) =>
        Local(volver) is { } local
            ? LocalRedirect(local)
            : RedirectToAction("Index", "Visitas", new { periodo = Periodo.Dia, fecha = DateOnly.FromDateTime(fecha).ToString("yyyy-MM-dd") });

    /// <summary>Solo direcciones de la propia aplicación (nunca a otra web).</summary>
    private string? Local(string? volver) => volver is { Length: > 0 } && Url.IsLocalUrl(volver) ? volver : null;
}
