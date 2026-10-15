namespace PeluqueriaLucia.Application.Agenda;

/// <summary>
/// Quién escribe a los clientes: nombre, contacto y logo del salón. Lo da la web (contenido público y wwwroot); los
/// avisos lo usan para hablar en nombre de la peluquería y dar su teléfono.
/// </summary>
public sealed record DatosSalon(string Nombre, IReadOnlyList<string> Telefonos, string Direccion, string Ciudad, string? Mapa = null, byte[]? Logo = null, string TipoLogo = "image/jpeg")
{
    public string DireccionCompleta => string.Join(", ", new[] { Direccion, Ciudad }.Where(x => !string.IsNullOrWhiteSpace(x)));
    /// <summary>Para escribir tras «llámanos al»: «659 306 659» o «659 306 659 o al 985 757 692».</summary>
    public string TelefonosEnTexto => Telefonos.Count <= 1 ? Telefonos.FirstOrDefault() ?? "" : string.Join(", ", Telefonos.Take(Telefonos.Count - 1)) + " o al " + Telefonos[^1];
}

public interface IDatosSalon
{
    Task<DatosSalon> ObtenerAsync(CancellationToken ct = default);
}
