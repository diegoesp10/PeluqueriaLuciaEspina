using Microsoft.AspNetCore.Razor.TagHelpers;

namespace PeluqueriaLucia.Web.TagHelpers;

// El generador de MVC oculta su marcador vacío mediante un estilo en línea.
// hidden conserva ese comportamiento sin debilitar style-src 'self'.
[HtmlTargetElement("div", Attributes = "asp-validation-summary")]
public sealed class ResumenValidacionSeguroTagHelper : TagHelper
{
    public override int Order => int.MaxValue;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        SustituirMarcador(output.PreContent);
        SustituirMarcador(output.Content);
        SustituirMarcador(output.PostContent);
    }

    private static void SustituirMarcador(TagHelperContent contenido)
    {
        var html = contenido.GetContent();
        if (html.Contains("<li style=\"display:none\">", StringComparison.Ordinal))
            contenido.SetHtmlContent(html.Replace(
                "<li style=\"display:none\">", "<li hidden>", StringComparison.Ordinal));
    }
}
