using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Web.Publico;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Controllers;

[AllowAnonymous]
[EnableRateLimiting(LimitesDePeticiones.Publico)]
public sealed class InicioController(IOptionsSnapshot<ContenidoPublico> contenido, PeluqueriaLucia.Application.Publico.ContenidoService catalogo, Textos textos) : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View(contenido.Value);

    [HttpGet("/servicios")]
    public async Task<IActionResult> Servicios(CancellationToken ct) => View(await CatalogoAsync(ct));

    [HttpGet("/api/tratamientos")]
    public async Task<IActionResult> Tratamientos(CancellationToken ct) => Json((await CatalogoAsync(ct)).Categorias);

    private async Task<ContenidoPublico> CatalogoAsync(CancellationToken ct)
    {
        var modelo = contenido.Value;
        var categorias = await catalogo.CategoriasAsync(ct);
        var tratamientos = await catalogo.TratamientosAsync(true, ct);
        modelo.Categorias = categorias.Select(c => new CategoriaServicios { Nombre = c.Nombre, Slug = c.Slug, Servicios = tratamientos.Where(x => x.CategoriaId == c.Id).Select(x => new ServicioPublico {
            Nombre = x.Nombre, Detalle = x.Descripcion, Destacado = x.Destacado,
            Precio = x.TipoPrecio == Domain.Publico.TipoPrecioPublico.Consultar ? textos["Consultar"] : (x.TipoPrecio == Domain.Publico.TipoPrecioPublico.Desde ? textos["desde"] + " " : "") + x.Precio!.Value.ToString("C", Idioma.FormatoEuros(HttpContext)),
            Duracion = (x.DuracionMinutos / 60 > 0 ? $"{x.DuracionMinutos / 60} h " : "") + (x.DuracionMinutos % 60 > 0 ? $"{x.DuracionMinutos % 60} min" : "")
        }).ToList() }).Where(c => c.Servicios.Count > 0).ToList();
        return modelo;
    }
}
