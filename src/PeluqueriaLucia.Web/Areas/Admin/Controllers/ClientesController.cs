using Microsoft.AspNetCore.Mvc;
using PeluqueriaLucia.Application.Clientes;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Web.Infraestructura;
using PeluqueriaLucia.Web.Areas.Admin.Models;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

public sealed class ClientesController(ClientesService clientes, VisitasService visitas, TimeProvider reloj) : AdminController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? q, Sexo? sexo, int pagina = 1, CancellationToken ct = default)
    {
        var resultado = await clientes.BuscarAsync(q, sexo, pagina, ct);
        return View(new ClientesIndiceModelo(resultado, q, sexo));
    }

    /// <summary>Buscador de clientes de la pantalla «Nueva visita».</summary>
    [HttpGet]
    public async Task<IActionResult> Buscar(string? q, CancellationToken ct)
    {
        var encontrados = await clientes.SugerirAsync(q, ct);
        return Json(encontrados.Select(c => new ClienteSugerencia(
            c.Id, c.NombreCompleto, c.Telefono ?? c.Email, c.Iniciales, (int)c.Sexo)));
    }

    [HttpGet]
    public async Task<IActionResult> Detalle(int id, int pagina = 1, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
            return NotFound();

        var filtro = FiltroVisitas.DeCliente(id);
        var historial = await visitas.ListarAsync(filtro, pagina, ct);
        var resumen = await visitas.ResumirAsync(filtro, ct);
        return View(new ClienteDetalleModelo(cliente, resumen, historial, DateOnly.FromDateTime(reloj.GetLocalNow().DateTime), await clientes.TieneCitasAsync(id,ct)));
    }

    [HttpPost, RequestSizeLimit(4096)]
    public async Task<IActionResult> Reconocer(string? telefono, string? email, CancellationToken ct) =>
        Json(await clientes.ReconocerAsync(telefono,email,ct));

    [HttpGet]
    public IActionResult Nuevo(bool visita = false) =>
        View("Edicion", new ClienteEdicionModelo(null, new ClienteFormulario(), visita));

    [HttpPost]
    public async Task<IActionResult> Nuevo(ClienteFormulario formulario, bool visita = false, CancellationToken ct = default)
    {
        if (ModelState.IsValid)
        {
            var reconocida = await clientes.ReconocerAsync(formulario.Telefono, formulario.Email, ct);
            var resultado = reconocida is null ? await clientes.CrearAsync(formulario.ADatos(), ct)
                : await clientes.ActualizarAsync(reconocida.Id, formulario.ADatos(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito($"{formulario.Nombre} ya está en tus clientes.");
                return visita
                    ? RedirectToAction("Nueva", "Visitas", new { clienteId = resultado.Valor })
                    : RedirectToAction(nameof(Detalle), new { id = resultado.Valor });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", new ClienteEdicionModelo(null, formulario, visita));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        return cliente is null ? NotFound() : View("Edicion", new ClienteEdicionModelo(id, ClienteFormulario.Desde(cliente)));
    }

    [HttpPost]
    public async Task<IActionResult> Editar(int id, ClienteFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await clientes.ActualizarAsync(id, formulario.ADatos(), ct);
            if (resultado.Exito)
            {
                this.AvisarExito("Cambios guardados.");
                return RedirectToAction(nameof(Detalle), new { id });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", new ClienteEdicionModelo(id, formulario));
    }

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        var resultado = await clientes.EliminarAsync(id, ct);
        if (!resultado.Exito)
        {
            this.AvisarError(resultado.Error!.Mensaje);
            return RedirectToAction(nameof(Detalle), new { id });
        }

        this.AvisarExito("Cliente eliminado.");
        return RedirectToAction(nameof(Index));
    }
}
