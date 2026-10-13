using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>
/// Lee importes escritos con coma o con punto («12,50», «12.50», «12,50 €»). El enlazador por defecto depende del idioma
/// del equipo y, en un Windows en inglés, convertiría «12,50» en 1250.
/// </summary>
internal sealed class DecimalFlexibleModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valor == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);
        var texto = valor.FirstValue?.Replace("€", string.Empty).Replace(" ", string.Empty).Trim();

        if (string.IsNullOrEmpty(texto))
        {
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (TryParse(texto, out var numero))
        {
            bindingContext.Result = ModelBindingResult.Success(numero);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Escribe un importe válido, por ejemplo 12,50.");
        }

        return Task.CompletedTask;
    }

    /// <summary>El último separador (coma o punto) es el decimal; cualquier otro se toma como separador de miles.</summary>
    internal static bool TryParse(string texto, out decimal numero)
    {
        var ultimo = texto.LastIndexOfAny([',', '.']);
        var normalizado = ultimo < 0
            ? texto
            : texto[..ultimo].Replace(",", string.Empty).Replace(".", string.Empty) + "." + texto[(ultimo + 1)..];
        return decimal.TryParse(normalizado, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out numero);
    }
}

internal sealed class DecimalFlexibleModelBinderProvider : IModelBinderProvider
{
    private static readonly DecimalFlexibleModelBinder Binder = new();

    public IModelBinder? GetBinder(ModelBinderProviderContext context) =>
        context.Metadata.UnderlyingOrModelType == typeof(decimal) ? Binder : null;
}
