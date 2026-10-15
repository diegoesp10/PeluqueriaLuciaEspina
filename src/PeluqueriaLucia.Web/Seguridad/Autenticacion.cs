using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PeluqueriaLucia.Application.Seguridad;

namespace PeluqueriaLucia.Web.Seguridad;

/// <summary>
/// Inicio de sesión del panel con cookie cifrada. La cookie lleva el identificador de la sesión y su token; en cada
/// petición se comprueba contra la base de datos, de modo que cerrar la sesión (o cambiar la contraseña) la anula al
/// momento aunque alguien hubiera copiado la cookie.
/// </summary>
public static class Autenticacion
{
    public const string Esquema = "Administracion";
    public const string PoliticaAdministrador = "Administrador";
    public const string ClaimSesion = "sid";
    public const string ClaimToken = "tok";
    public const string ClaimAdministrador = "aid";
    public const string RutaEstado = "/admin/acceso/estado";
    public const string AvisoSesiones = "sesiones-cambiadas";

    public static IServiceCollection AddAutenticacionAdministrador(this IServiceCollection services)
    {
        services
            .AddAuthentication(Esquema)
            .AddCookie(Esquema, o =>
            {
                o.Cookie.Name = "PeluqueriaLucia.Panel";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.Path = "/admin";
                o.LoginPath = "/admin/acceso";
                o.LogoutPath = "/admin/acceso/salir";
                o.AccessDeniedPath = "/admin/acceso";
                o.ReturnUrlParameter = "volver";
                o.ExpireTimeSpan = Domain.Seguridad.SesionAdministrador.DuracionMaxima;
                o.SlidingExpiration = false;
                o.Events.OnValidatePrincipal = ValidarSesionAsync;
                o.Events.OnRedirectToLogin = contexto =>
                {
                    if (contexto.Request.Headers.Accept.Any(a => a?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
                        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    else contexto.Response.Redirect(contexto.RedirectUri);
                    return Task.CompletedTask;
                };
            });
        services.AddOptions<CookieAuthenticationOptions>(Esquema).Configure<Microsoft.Extensions.Options.IOptions<OpcionesSeguridad>>(
            (o, seguridad) => o.Cookie.SecurePolicy = seguridad.Value.RequerirHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest);

        services.AddAuthorizationBuilder()
            // Todo endpoint sin [AllowAnonymous] exige sesión. Una URL que no existe no tiene endpoint: debe acabar en el
            // 404 del sitio, no en el login del panel (antes, cualquier enlace roto de la web llevaba a /admin/acceso).
            .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(Esquema)
                .RequireAssertion(c => c.User.Identity?.IsAuthenticated == true || (c.Resource is HttpContext h && h.GetEndpoint() is null))
                .Build())
            .AddPolicy(PoliticaAdministrador, p => p
                .AddAuthenticationSchemes(Esquema)
                .RequireAuthenticatedUser()
                .RequireClaim(ClaimSesion)
                .RequireClaim(ClaimAdministrador)
                .RequireClaim(ClaimToken)
                .RequireClaim("amr", "mfa"));

        return services;
    }

    /// <summary>
    /// NameIdentifier identifica a la persona, no a la sesión: los tokens antifalsificación de los formularios se atan a
    /// él, así que un formulario abierto sigue valiendo si la sesión caduca y se vuelve a entrar con la misma cuenta.
    /// Sin ese claim, el token se calculaba con todos los claims (sesión y token incluidos) y cualquier reinicio de sesión
    /// convertía el envío en un 400.
    /// </summary>
    public static ClaimsPrincipal CrearPrincipal(SesionIniciada sesion) => new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, sesion.Usuario),
            new Claim(ClaimTypes.NameIdentifier, sesion.AdministradorId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimAdministrador, sesion.AdministradorId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(ClaimSesion, sesion.SesionId.ToString()),
            new Claim(ClaimToken, sesion.Token),
            new Claim("amr", "mfa"),
        ],
        Esquema));

    /// <summary>Se ejecuta en cada petición con cookie: el token tiene que corresponder a una sesión abierta.</summary>
    private static async Task ValidarSesionAsync(CookieValidatePrincipalContext contexto)
    {
        var principal = contexto.Principal;
        var acceso = contexto.HttpContext.RequestServices.GetRequiredService<AccesoService>();
        var valida = principal?.FindFirst(ClaimSesion)?.Value is { } sid && Guid.TryParse(sid, out var sesionId)
            ? await acceso.ValidarSesionAsync(sesionId, principal.FindFirst(ClaimToken)?.Value, IpCliente.De(contexto.HttpContext),
                contexto.HttpContext.RequestAborted, registrarUso: !contexto.Request.Path.Equals(RutaEstado))
            : null;

        if (valida is null || principal?.FindFirst("amr")?.Value != "mfa" || principal?.FindFirst(ClaimAdministrador)?.Value != valida.AdministradorId.ToString(System.Globalization.CultureInfo.InvariantCulture) ||
            principal.Identity?.Name != valida.Usuario)
        {
            contexto.RejectPrincipal();
            await contexto.HttpContext.SignOutAsync(Esquema);
        }
    }
}
