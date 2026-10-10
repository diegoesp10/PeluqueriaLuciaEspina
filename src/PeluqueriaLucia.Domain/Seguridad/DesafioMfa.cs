namespace PeluqueriaLucia.Domain.Seguridad;

public sealed class DesafioMfa
{
    private DesafioMfa() { }
    public Guid Id { get; private set; }
    public int AdministradorId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public string Ip { get; private set; } = "";
    public string? NuevoSecretoProtegido { get; private set; }
    public DateTime ContrasenaEn { get; private set; }
    public int VersionSeguridad { get; private set; }
    public DateTime ExpiraEn { get; private set; }
    public DateTime? ConsumidoEn { get; private set; }
    public int Intentos { get; private set; }
    public static DesafioMfa Crear(int administrador, string hash, string ip, string? secreto, DateTime contrasena, int version, DateTime ahora) =>
        new() { Id = Guid.NewGuid(), AdministradorId = administrador, TokenHash = hash, Ip = ip,
            NuevoSecretoProtegido = secreto, ContrasenaEn = contrasena, VersionSeguridad = version, ExpiraEn = ahora.AddMinutes(5) };
    public bool Activo(DateTime ahora) => ConsumidoEn is null && Intentos < 5 && ahora < ExpiraEn;
    public void Fallar() => Intentos++;
    public void Consumir(DateTime ahora) => ConsumidoEn = ahora;
}

public sealed class RecuperacionMfa
{
    private RecuperacionMfa() { }
    public Guid Id { get; private set; }
    public int AdministradorId { get; private set; }
    public string Hash { get; private set; } = "";
    public DateTime? UsadoEn { get; private set; }
    public static RecuperacionMfa Crear(int administrador, string hash) => new() { Id = Guid.NewGuid(), AdministradorId = administrador, Hash = hash };
    public void Consumir(DateTime ahora) => UsadoEn ??= ahora;
}
