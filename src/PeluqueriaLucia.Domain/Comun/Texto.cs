namespace PeluqueriaLucia.Domain.Comun;

internal static class Texto
{
    /// <summary>Recorta espacios y convierte el texto vacío en <c>null</c>.</summary>
    public static string? Opcional(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    public static string Obligatorio(string? valor, int longitudMaxima, string nombreCampo, string campo)
    {
        var limpio = Opcional(valor) ?? throw new ReglaDeNegocioException($"{nombreCampo} es obligatorio.", campo);
        if (limpio.Length > longitudMaxima)
            throw new ReglaDeNegocioException($"{nombreCampo} no puede superar los {longitudMaxima} caracteres.", campo);
        return limpio;
    }
}
