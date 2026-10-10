using System.Text.Json;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Seguridad;

namespace PeluqueriaLucia.Web.Seguridad;

public sealed record DesafioCookieDatos(Guid Id, string Token, string? Volver);
public sealed class DesafioCookie(IProtectorSecretos protector, IOptions<OpcionesSeguridad> opciones)
{
    private const string Nombre = "PeluqueriaLucia.Verificacion";
    private CookieOptions Opciones(HttpContext c) => new() { HttpOnly = true, SameSite = SameSiteMode.Strict,
        Secure = opciones.Value.RequerirHttps || c.Request.IsHttps, Path = "/admin/acceso", MaxAge = TimeSpan.FromMinutes(5), IsEssential = true };
    public void Guardar(HttpContext c, DesafioIniciado desafio, string? volver) => c.Response.Cookies.Append(Nombre,
        protector.Proteger(JsonSerializer.Serialize(new DesafioCookieDatos(desafio.Id, desafio.Token, volver)), "desafio-mfa"), Opciones(c));
    public DesafioCookieDatos? Leer(HttpContext c)
    {
        if (!c.Request.Cookies.TryGetValue(Nombre, out var valor)) return null;
        var json = protector.Leer(valor, "desafio-mfa");
        try { return json is null ? null : JsonSerializer.Deserialize<DesafioCookieDatos>(json); }
        catch (JsonException) { return null; }
    }
    public void Borrar(HttpContext c) => c.Response.Cookies.Delete(Nombre, Opciones(c));
}
