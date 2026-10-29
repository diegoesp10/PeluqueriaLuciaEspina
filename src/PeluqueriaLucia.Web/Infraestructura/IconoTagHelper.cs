using System.Collections.Frozen;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>
/// Pinta un icono de línea en SVG en línea: <c>&lt;icono nombre="tijeras" /&gt;</c>. Los trazos siguen el estilo de
/// Lucide (licencia ISC, ver NOTICE.md). Al ir dentro de la página no hace falta conexión a internet ni fuentes de iconos.
/// </summary>
[HtmlTargetElement("icono", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconoTagHelper : TagHelper
{
    private static readonly FrozenDictionary<string, string> Trazos = new Dictionary<string, string>
    {
        ["casa"] = """<path d="m3 10 9-7 9 7v10H3Z"/><path d="M9 20v-7h6v7"/>""",
        ["hoja"] = """<path d="M20 3c-8-1-15 3-15 10a6 6 0 0 0 6 6c7 0 11-7 9-16Z"/><path d="M3 21 15 9"/>""",
        ["calendario"] = """<rect width="18" height="18" x="3" y="4" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/>""",
        ["clientes"] = """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75"/>""",
        ["cliente"] = """<path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>""",
        ["cliente-nuevo"] = """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M19 8v6M22 11h-6"/>""",
        ["tijeras"] = """<circle cx="6" cy="6" r="3"/><path d="M8.12 8.12 12 12M20 4 8.12 15.88"/><circle cx="6" cy="18" r="3"/><path d="M14.8 14.8 20 20"/>""",
        ["mas"] = """<path d="M5 12h14M12 5v14"/>""",
        ["anterior"] = """<path d="m15 18-6-6 6-6"/>""",
        ["siguiente"] = """<path d="m9 18 6-6-6-6"/>""",
        ["arriba"] = """<path d="m18 15-6-6-6 6"/>""",
        ["abajo"] = """<path d="m6 9 6 6 6-6"/>""",
        ["buscar"] = """<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>""",
        ["editar"] = """<path d="M17 3a2.85 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"/><path d="m15 5 4 4"/>""",
        ["papelera"] = """<path d="M3 6h18M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"/>""",
        ["ticket"] = """<path d="M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1 2 1V2l-2 1-2-1-2 1-2-1-2 1-2-1-2 1Z"/><path d="M8 7h8M8 11h8M8 15h5"/>""",
        ["imprimir"] = """<path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><path d="M6 9V3a1 1 0 0 1 1-1h10a1 1 0 0 1 1 1v6"/><rect x="6" y="14" width="12" height="8" rx="1"/>""",
        ["descargar"] = """<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3"/>""",
        ["check"] = """<path d="M20 6 9 17l-5-5"/>""",
        ["check-circulo"] = """<circle cx="12" cy="12" r="10"/><path d="m9 12 2 2 4-4"/>""",
        ["alerta"] = """<circle cx="12" cy="12" r="10"/><path d="M12 8v4M12 16h.01"/>""",
        ["cerrar"] = """<path d="M18 6 6 18M6 6l12 12"/>""",
        ["menu"] = """<path d="M4 7h16M4 12h16M4 17h16"/>""",
        // Flechas y adornos que antes eran caracteres de texto (↗ → ✳ ★): como SVG se ven igual en todos los sistemas
        ["flecha"] = """<path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>""",
        ["flecha-salir"] = """<path d="M7 7h10v10"/><path d="M7 17 17 7"/>""",
        ["asterisco"] = """<path d="M12 5v14"/><path d="m17.2 8.5-10.4 7"/><path d="m6.8 8.5 10.4 7"/>""",
        ["estrella"] = """<polygon fill="currentColor" points="12 2.5 15 8.6 21.7 9.6 16.9 14.3 18 21 12 17.8 6 21 7.1 14.3 2.3 9.6 9 8.6"/>""",
        ["estrella-vacia"] = """<polygon points="12 2.5 15 8.6 21.7 9.6 16.9 14.3 18 21 12 17.8 6 21 7.1 14.3 2.3 9.6 9 8.6"/>""",
        ["ajustes"] = """<path d="M12.2 2h-.4a2 2 0 0 0-2 2v.2a2 2 0 0 1-1 1.7l-.4.3a2 2 0 0 1-2 0l-.2-.1a2 2 0 0 0-2.7.7l-.2.4a2 2 0 0 0 .7 2.7l.2.1a2 2 0 0 1 1 1.8v.4a2 2 0 0 1-1 1.8l-.2.1a2 2 0 0 0-.7 2.7l.2.4a2 2 0 0 0 2.7.7l.2-.1a2 2 0 0 1 2 0l.4.3a2 2 0 0 1 1 1.7v.2a2 2 0 0 0 2 2h.4a2 2 0 0 0 2-2v-.2a2 2 0 0 1 1-1.7l.4-.3a2 2 0 0 1 2 0l.2.1a2 2 0 0 0 2.7-.7l.2-.4a2 2 0 0 0-.7-2.7l-.2-.1a2 2 0 0 1-1-1.8v-.4a2 2 0 0 1 1-1.8l.2-.1a2 2 0 0 0 .7-2.7l-.2-.4a2 2 0 0 0-2.7-.7l-.2.1a2 2 0 0 1-2 0l-.4-.3a2 2 0 0 1-1-1.7V4a2 2 0 0 0-2-2Z"/><circle cx="12" cy="12" r="3"/>""",
        ["euro"] = """<path d="M4 10h12M4 14h9M19 6a7.7 7.7 0 0 0-5.2-2A7.9 7.9 0 0 0 6 12c0 4.4 3.5 8 7.8 8 2 0 3.8-.8 5.2-2"/>""",
        ["tarjeta"] = """<rect width="20" height="14" x="2" y="5" rx="2"/><path d="M2 10h20"/>""",
        ["efectivo"] = """<rect width="20" height="12" x="2" y="6" rx="2"/><circle cx="12" cy="12" r="2"/><path d="M6 12h.01M18 12h.01"/>""",
        ["mixto"] = """<path d="M16 3h5v5M8 3H3v5M21 3l-7 7M3 3l7 7M12 22v-8"/>""",
        ["reloj"] = """<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>""",
        ["archivar"] = """<rect width="20" height="5" x="2" y="3" rx="1"/><path d="M4 8v11a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8M10 12h4"/>""",
        ["recuperar"] = """<path d="M3 12a9 9 0 1 0 9-9 9.75 9.75 0 0 0-6.74 2.74L3 8"/><path d="M3 3v5h5"/>""",
        ["imagen"] = """<rect width="18" height="18" x="3" y="3" rx="2"/><circle cx="9" cy="9" r="2"/><path d="m21 15-3.09-3.09a2 2 0 0 0-2.82 0L6 21"/>""",
        ["telefono"] = """<path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.36 1.9.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.91.34 1.85.57 2.81.7A2 2 0 0 1 22 16.92Z"/>""",
        ["email"] = """<rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/>""",
        ["producto"] = """<path d="M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73Z"/><path d="M12 22V12"/><path d="m3.3 7 7.7 4.73a2 2 0 0 0 2 0L20.7 7"/><path d="m7.5 4.27 9 5.15"/>""",
        ["codigo-barras"] = """<path d="M3 7V5a2 2 0 0 1 2-2h2M17 3h2a2 2 0 0 1 2 2v2M21 17v2a2 2 0 0 1-2 2h-2M7 21H5a2 2 0 0 1-2-2v-2"/><path d="M8 7v10M12 7v10M17 7v10"/>""",
        ["etiqueta"] = """<path d="M12.59 2.59A2 2 0 0 0 11.17 2H4a2 2 0 0 0-2 2v7.17a2 2 0 0 0 .59 1.42l8.7 8.7a2.43 2.43 0 0 0 3.42 0l6.58-6.58a2.43 2.43 0 0 0 0-3.42Z"/><circle cx="7.5" cy="7.5" r="1.5"/>""",
        ["excel"] = """<path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4M8 13h2M14 13h2M8 17h2M14 17h2"/>""",
        ["volver"] = """<path d="m12 19-7-7 7-7M19 12H5"/>""",
        ["hoy"] = """<rect width="18" height="18" x="3" y="4" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/><circle cx="12" cy="15" r="1.5"/>""",
        ["sol"] = """<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"/>""",
        ["luna"] = """<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>""",
        ["auto"] = """<circle cx="12" cy="12" r="9"/><path d="M12 3a9 9 0 0 1 0 18Z" fill="currentColor"/>""",
        ["regalo"] = """<rect width="18" height="4" x="3" y="8" rx="1"/><path d="M12 8v13"/><path d="M19 12v7a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2v-7"/><path d="M7.5 8a2.5 2.5 0 0 1 0-5A4.8 8 0 0 1 12 8a4.8 8 0 0 1 4.5-5 2.5 2.5 0 0 1 0 5"/>""",
        ["vacio"] = """<path d="M22 12h-6l-2 3h-4l-2-3H2"/><path d="M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11Z"/>""",
    }.ToFrozenDictionary();

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Lado del icono en píxeles.</summary>
    public int Tamano { get; set; } = 20;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (!Trazos.TryGetValue(Nombre, out var trazo))
            throw new InvalidOperationException($"No existe el icono «{Nombre}».");

        output.TagName = "svg";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("xmlns", "http://www.w3.org/2000/svg");
        output.Attributes.SetAttribute("width", Tamano);
        output.Attributes.SetAttribute("height", Tamano);
        output.Attributes.SetAttribute("viewBox", "0 0 24 24");
        output.Attributes.SetAttribute("fill", "none");
        output.Attributes.SetAttribute("stroke", "currentColor");
        output.Attributes.SetAttribute("stroke-width", "2");
        output.Attributes.SetAttribute("stroke-linecap", "round");
        output.Attributes.SetAttribute("stroke-linejoin", "round");
        output.Attributes.SetAttribute("aria-hidden", "true");
        output.Attributes.SetAttribute("focusable", "false");
        output.Content.SetHtmlContent(trazo);
    }
}
