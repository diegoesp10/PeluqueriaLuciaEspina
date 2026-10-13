using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PeluqueriaLucia.Web.Publico;

/// <summary>
/// Todos los formularios de la web pública envían el idioma de la página en el campo <c>language</c> (es/en), para que el
/// servidor responda los mensajes en ese idioma. El panel de administración no lo lleva.
/// </summary>
[HtmlTargetElement("form")]
public sealed class IdiomaFormularioTagHelper : TagHelper
{
    [HtmlAttributeNotBound, ViewContext]
    public ViewContext ViewContext { get; set; } = default!;

    public override int Order => 1000;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (ViewContext.RouteData.Values["area"] is string area && area.Length > 0)
            return;

        var idioma = Idioma.Actual(ViewContext.HttpContext);
        output.PostContent.AppendHtml($"<input type=\"hidden\" name=\"{Idioma.Parametro}\" value=\"{idioma}\" />");
    }
}
