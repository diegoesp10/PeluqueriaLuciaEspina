using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PeluqueriaLucia.Application.Comun;

namespace PeluqueriaLucia.Web.Infraestructura;

/// <summary>Avisos que sobreviven a la redirección (patrón POST → redirección → GET) y se muestran como notificación.</summary>
public static class Avisos
{
    public const string ClaveExito = "Aviso.Exito";
    public const string ClaveError = "Aviso.Error";

    public static void AvisarExito(this Controller controller, string mensaje) => controller.TempData[ClaveExito] = mensaje;

    public static void AvisarError(this Controller controller, string mensaje) => controller.TempData[ClaveError] = mensaje;

    /// <summary>Lleva el error de un caso de uso a su campo del formulario (o al resumen, si es general).</summary>
    public static void AgregarError(this ModelStateDictionary modelState, Error error, string prefijo = "") =>
        modelState.AddModelError(error.Campo is null ? string.Empty : prefijo + error.Campo, error.Mensaje);
}
