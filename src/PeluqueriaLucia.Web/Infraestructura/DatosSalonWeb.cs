using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Agenda;
using PeluqueriaLucia.Web.Publico;

namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>
/// Datos del salón para los avisos a clientes: nombre y dirección de contenido-publico.json, los teléfonos de «Contacto
/// de la web» y el logo de wwwroot (Img/Icono.jpg, unos 10 KB), que el correo lleva incrustado.
/// </summary>
public sealed class DatosSalonWeb(IOptions<ContenidoPublico> contenido, TelefonosPublicos telefonos, IWebHostEnvironment entorno) : IDatosSalon
{
    private static byte[]? logo;

    public async Task<DatosSalon> ObtenerAsync(CancellationToken ct = default)
    {
        var c = contenido.Value;
        logo ??= File.Exists(Path.Combine(entorno.WebRootPath, "Img", "Icono.jpg")) ? await File.ReadAllBytesAsync(Path.Combine(entorno.WebRootPath, "Img", "Icono.jpg"), ct) : null;
        var numeros = (await telefonos.ListarAsync(ct)).Select(t => t.Numero).ToList();
        return new DatosSalon(c.Nombre, numeros, c.Direccion, c.Ciudad, string.IsNullOrWhiteSpace(c.Mapa) ? null : c.Mapa, logo);
    }
}
