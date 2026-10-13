using System.Globalization;

namespace PeluqueriaLucia.Web.Publico;

/// <summary>
/// Idioma de la web pública. El español vive en la raíz (<c>/citas</c>) y el inglés bajo <c>/en</c> (<c>/en/citas</c>).
/// El prefijo se convierte en <c>PathBase</c>: los enlaces que genera MVC (<c>asp-action</c>, <c>~/</c>) lo conservan solos.
/// El panel de administración solo existe en español.
/// </summary>
public static class Idioma
{
    public const string Espanol = "es";
    public const string Ingles = "en";

    /// <summary>Nombre del dato <c>language</c> que viaja en formularios, consultas y llamadas JSON (ver docs/IDIOMA.md).</summary>
    public const string Parametro = "language";

    public static readonly CultureInfo CulturaEspanol = CultureInfo.GetCultureInfo("es-ES");
    public static readonly CultureInfo CulturaIngles = CultureInfo.GetCultureInfo("en-GB");

    private const string Clave = "idioma";
    private const string ClaveBase = "idioma-base";
    private const string Prefijo = "/en";

    public static string Actual(HttpContext contexto) => contexto.Items[Clave] as string ?? Espanol;

    public static bool EsIngles(HttpContext contexto) => Actual(contexto) == Ingles;

    /// <summary>URL equivalente en el otro idioma, conservando ruta y consulta.</summary>
    public static string UrlEn(HttpContext contexto, string idioma)
    {
        var raiz = contexto.Items[ClaveBase] as string ?? contexto.Request.PathBase.Value ?? string.Empty;
        var ruta = contexto.Request.Path.Value is { Length: > 1 } p ? p : "/";
        return raiz + (idioma == Ingles ? Prefijo : string.Empty) + ruta + contexto.Request.QueryString;
    }

    /// <summary>Ruta fuera de la web traducida (el panel, por ejemplo), aunque la página actual esté en inglés.</summary>
    public static string SinIdioma(HttpContext contexto, string ruta) =>
        (contexto.Items[ClaveBase] as string ?? contexto.Request.PathBase.Value ?? string.Empty) + ruta;

    /// <summary>Precios en euros con el formato de cada idioma: «20,00 €» / «€20.00».</summary>
    public static NumberFormatInfo FormatoEuros(HttpContext contexto)
    {
        if (!EsIngles(contexto)) return CulturaEspanol.NumberFormat;
        var formato = (NumberFormatInfo)CulturaIngles.NumberFormat.Clone();
        formato.CurrencySymbol = "€";
        return formato;
    }

    public static IApplicationBuilder UsePrefijoDeIdioma(this IApplicationBuilder app) =>
        app.Use((contexto, siguiente) =>
        {
            contexto.Items[ClaveBase] = contexto.Request.PathBase.Value ?? string.Empty;
            if (contexto.Request.Path.StartsWithSegments(Prefijo, StringComparison.OrdinalIgnoreCase, out var resto))
            {
                // El panel no tiene versión en inglés y su cookie está limitada a /admin
                if (resto.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
                {
                    contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }

                contexto.Items[Clave] = Ingles;
                contexto.Request.PathBase = contexto.Request.PathBase.Add(Prefijo);
                contexto.Request.Path = resto.HasValue ? resto : "/";
            }
            else
            {
                contexto.Items[Clave] = Espanol;
            }

            return siguiente(contexto);
        });
}
