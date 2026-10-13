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

    public string? Apodo { get; private set; }
    public bool PendienteCompletar { get; private set; }
    public ICollection<FaltaAsistencia> Faltas { get; private set; } = new List<FaltaAsistencia>();
    public int NumeroFaltas => Faltas.Count(x => x.AnuladaUtc is null);

    public void CambiarApodo(string? apodo)
    {
        if (apodo?.Length > 80) throw new ReglaDeNegocioException("El apodo admite hasta 80 caracteres.", nameof(Apodo));
        Apodo = Texto.Opcional(apodo);
    }

    public static Cliente CrearDesdeReserva(string apodo, string email, string telefono, DateTime ahora, string? apellido = null, Sexo? sexo = null) => new()
    { Nombre = Texto.Obligatorio(apodo, LongitudNombre, "El nombre", nameof(Nombre)), Apodo = apodo.Trim().Length <= 80 ? apodo.Trim() : null, Email = email, Telefono = telefono, Apellido = Texto.Opcional(apellido) ?? "", Sexo = sexo is { } valor && Enum.IsDefined(valor) ? valor : Sexo.Otro, FechaCreacion = ahora, PendienteCompletar = true };

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
        PendienteCompletar = false;
        Email = Texto.Opcional(email);
        Telefono = Texto.Opcional(telefono);
        Sexo = Enum.IsDefined(sexo) ? sexo : throw new ReglaDeNegocioException("El sexo no es válido.", nameof(Sexo));
    }
}
