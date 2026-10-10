using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Clientes;

public sealed class Cliente
{
    public const int LongitudNombre = 100;

    private Cliente()
    {
    }

    public int Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    public string Apellido { get; private set; } = string.Empty;

    public string? Email { get; private set; }

    public string? Telefono { get; private set; }

    public Sexo Sexo { get; private set; }

    public DateTime FechaCreacion { get; private set; }

    public string NombreCompleto => $"{Nombre} {Apellido}".Trim();

    public string Iniciales =>
        string.Concat(new[] { Nombre, Apellido }.Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0])));

    public static Cliente Crear(string nombre, string apellido, string? email, string? telefono, Sexo sexo, DateTime ahora)
    {
        var cliente = new Cliente { FechaCreacion = ahora };
        cliente.Actualizar(nombre, apellido, email, telefono, sexo);
        return cliente;
    }

    /// <summary>Cambia los datos de contacto. La fecha de alta no se toca nunca.</summary>
    public void Actualizar(string nombre, string apellido, string? email, string? telefono, Sexo sexo)
    {
        Nombre = Texto.Obligatorio(nombre, LongitudNombre, "El nombre", nameof(Nombre));
        Apellido = Texto.Obligatorio(apellido, LongitudNombre, "El apellido", nameof(Apellido));
        Email = Texto.Opcional(email);
        Telefono = Texto.Opcional(telefono);
        Sexo = Enum.IsDefined(sexo) ? sexo : throw new ReglaDeNegocioException("El sexo no es válido.", nameof(Sexo));
    }
}
