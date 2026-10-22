using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Web.Areas.Admin.Models;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Web.Areas.Admin.Controllers;

/// <summary>Inventario de la tienda: fichas con referencia y fotos, stock y su historial.</summary>
public sealed class ProductosController(ProductosService productos) : AdminController
{
    /// <summary>Hasta seis fotos de 10 MB en un envío, con margen para el resto del formulario.</summary>
    private const long TamanoMaximoEnvio = Producto.MaximoFotos * 10L * 1024 * 1024 + 1024 * 1024;

    /// <summary>
    /// Tras un conflicto de concurrencia se redirige: los datos se vuelven a leer en otra petición, porque los de esta
    /// tienen en memoria el cambio que no se ha guardado.
    /// </summary>
    private const string AvisoConcurrencia = "El producto acaba de cambiar (por ejemplo, con un cobro) y no se ha guardado nada. Revisa los datos actuales y vuelve a intentarlo.";

    [HttpGet]
    public async Task<IActionResult> Index(string? q, EstadoInventario estado = EstadoInventario.Activos, string? categoria = null, int pagina = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroProductos(q?.Trim(), Enum.IsDefined(estado) ? estado : EstadoInventario.Activos, categoria?.Trim());

        // Secuencial a propósito: el contexto de base de datos no admite consultas simultáneas
        var listado = await productos.BuscarAsync(filtro, pagina, ct);
        var resumen = await productos.ResumirAsync(ct);
        var (categorias, _) = await productos.SugerenciasAsync(ct);
        return View(new ProductosIndiceModelo(filtro, listado, resumen, categorias));
    }

    [HttpGet]
    public async Task<IActionResult> Nuevo(CancellationToken ct) => View("Edicion", await ModeloEdicionAsync(null, new ProductoFormulario(), null, ct));

    [HttpPost, RequestSizeLimit(TamanoMaximoEnvio), RequestFormLimits(MultipartBodyLengthLimit = TamanoMaximoEnvio)]
    public async Task<IActionResult> Nuevo(ProductoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var resultado = await productos.CrearAsync(new DatosProducto(formulario.AFicha(), formulario.StockInicial), formulario.AArchivos(), AdministradorId, ct);
            if (resultado.Exito)
            {
                this.AvisarExito($"«{formulario.Nombre.Trim()}» añadido al inventario.");
                return RedirectToAction(nameof(Ficha), new { id = resultado.Valor });
            }

            ModelState.AgregarError(resultado.Error!);
        }

        return View("Edicion", await ModeloEdicionAsync(null, formulario, null, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        return producto is null ? NotFound() : View("Edicion", await ModeloEdicionAsync(id, ProductoFormulario.Desde(producto), producto, ct));
    }

    [HttpPost, RequestSizeLimit(TamanoMaximoEnvio), RequestFormLimits(MultipartBodyLengthLimit = TamanoMaximoEnvio)]
    public async Task<IActionResult> Editar(int id, ProductoFormulario formulario, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var resultado = await productos.EditarAsync(id, formulario.AFicha(), formulario.ACambios(), ct);
                if (resultado.Exito)
                {
                    this.AvisarExito("Cambios guardados. Las visitas anteriores conservan el nombre y el precio que tenían.");
                    return RedirectToAction(nameof(Ficha), new { id });
                }

                ModelState.AgregarError(resultado.Error!);
            }
            catch (DbUpdateConcurrencyException)
            {
                this.AvisarError(AvisoConcurrencia);
                return RedirectToAction(nameof(Editar), new { id });
            }
        }

        var producto = await productos.ObtenerAsync(id, ct);
        return producto is null ? NotFound() : View("Edicion", await ModeloEdicionAsync(id, formulario, producto, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Ficha(int id, int pagina = 1, CancellationToken ct = default)
    {
        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return NotFound();

        return View(new ProductoFichaModelo(producto, await productos.MovimientosAsync(id, pagina, ct), new MovimientoFormulario()));
    }

    /// <summary>Entrada de mercancía, uso en el salón o recuento.</summary>
    [HttpPost]
    public async Task<IActionResult> Movimiento(int id, MovimientoFormulario movimiento, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var resultado = await productos.RegistrarMovimientoAsync(id, movimiento.Tipo, movimiento.Cantidad!.Value, movimiento.Nota, AdministradorId, ct);
                if (resultado.Exito)
                {
                    this.AvisarExito($"{TextoMovimiento(movimiento.Tipo)} registrado. Stock actual: {FormatoProductos.Unidades(resultado.Valor)}.");
                    return RedirectToAction(nameof(Ficha), new { id });
                }

                ModelState.AgregarError(resultado.Error!);
            }
            catch (DbUpdateConcurrencyException)
            {
                this.AvisarError(AvisoConcurrencia);
                return RedirectToAction(nameof(Ficha), new { id });
            }
        }

        var producto = await productos.ObtenerAsync(id, ct);
        if (producto is null)
            return NotFound();
        return View(nameof(Ficha), new ProductoFichaModelo(producto, await productos.MovimientosAsync(id, 1, ct), movimiento));
    }

    [HttpPost]
    public async Task<IActionResult> Archivar(int id, CancellationToken ct) => await CambiarActivoAsync(id, archivar: true, ct);

    [HttpPost]
    public async Task<IActionResult> Reactivar(int id, CancellationToken ct) => await CambiarActivoAsync(id, archivar: false, ct);

    [HttpPost]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
    {
        try
        {
            var resultado = await productos.EliminarAsync(id, ct);
            if (!resultado.Exito)
            {
                this.AvisarError(resultado.Error!.Mensaje);
                return RedirectToAction(nameof(Index));
            }

            if (resultado.Valor == Eliminacion.Archivado)
            {
                this.AvisarExito("El producto aparece en visitas, así que se ha archivado en lugar de borrarse: sus tickets no cambian.");
                return RedirectToAction(nameof(Ficha), new { id });
            }

            this.AvisarExito("Producto eliminado, con sus fotos y su historial.");
        }
        catch (DbUpdateConcurrencyException)
        {
            this.AvisarError(AvisoConcurrencia);
            return RedirectToAction(nameof(Ficha), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Las fotos están en una carpeta privada: solo se ven con sesión en el panel.</summary>
    [HttpGet("/admin/productos/foto/{id:guid}")]
    public IActionResult Foto(Guid id, bool mini = false) =>
        productos.RutaFoto(id, mini) is { } ruta ? PhysicalFile(ruta, "image/jpeg") : NotFound();

    private async Task<IActionResult> CambiarActivoAsync(int id, bool archivar, CancellationToken ct)
    {
        try
        {
            var resultado = await productos.ArchivarAsync(id, archivar, ct);
            if (!resultado.Exito)
                this.AvisarError(resultado.Error!.Mensaje);
            else
                this.AvisarExito(archivar ? "Producto archivado: ya no se ofrece en visitas nuevas." : "El producto vuelve a estar a la venta.");
        }
        catch (DbUpdateConcurrencyException)
        {
            this.AvisarError(AvisoConcurrencia);
        }

        return RedirectToAction(nameof(Ficha), new { id });
    }

    private async Task<ProductoEdicionModelo> ModeloEdicionAsync(int? id, ProductoFormulario formulario, Producto? producto, CancellationToken ct)
    {
        var (categorias, marcas) = await productos.SugerenciasAsync(ct);
        return new ProductoEdicionModelo(id, formulario, producto, categorias, marcas);
    }

    private static string TextoMovimiento(MovimientoManual tipo) => tipo switch
    {
        MovimientoManual.Entrada => "Entrada",
        MovimientoManual.UsoInterno => "Uso en el salón",
        _ => "Recuento",
    };
}
