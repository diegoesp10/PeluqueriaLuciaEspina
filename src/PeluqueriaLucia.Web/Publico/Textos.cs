using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace PeluqueriaLucia.Web.Publico;

/// <summary>
/// Traducciones de la web pública. La clave es el propio texto en español, así las vistas se leen igual que antes y lo que
/// aún no tenga traducción sale en español en lugar de romperse. Las traducciones están en <c>Publico/textos.en.json</c>.
/// <code>@T["Reservar cita"]   @T.F("{0} opiniones publicadas", total)</code>
/// </summary>
public sealed class Textos : IStringLocalizer
{
    private readonly IReadOnlyDictionary<string, string> ingles = Cargar();

    public static bool EnIngles => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == Idioma.Ingles;

    public string this[string espanol] => EnIngles && ingles.TryGetValue(espanol, out var traducido) ? traducido : espanol;

    /// <summary>Texto con huecos <c>{0}</c>, formateado con la cultura de la petición.</summary>
    public string F(string espanol, params object[] valores) => string.Format(CultureInfo.CurrentCulture, this[espanol], valores);

    /// <summary>Claves sin traducción (lo comprueban las pruebas).</summary>
    public bool Traducido(string espanol) => ingles.ContainsKey(espanol);

    internal static IReadOnlyDictionary<string, string> Cargar()
    {
        using var flujo = typeof(Textos).Assembly.GetManifestResourceStream("PeluqueriaLucia.Web.Publico.textos.en.json")
            ?? throw new InvalidOperationException("Falta el recurso textos.en.json.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(flujo, new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip })!;
    }

    LocalizedString IStringLocalizer.this[string name] => new(name, this[name], !EnIngles || ingles.ContainsKey(name));

    LocalizedString IStringLocalizer.this[string name, params object[] arguments] => new(name, F(name, arguments), !EnIngles || ingles.ContainsKey(name));

    IEnumerable<LocalizedString> IStringLocalizer.GetAllStrings(bool includeParentCultures) =>
        ingles.Select(p => new LocalizedString(p.Key, EnIngles ? p.Value : p.Key));
}
