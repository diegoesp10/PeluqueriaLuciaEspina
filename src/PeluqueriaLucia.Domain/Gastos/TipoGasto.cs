using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Gastos;

/// <summary>
/// De qué es un gasto, como lo agrupa el asesor: «Personal», «Dietas», «Viajes y desplazamientos»… Los edita el salón: se
/// añaden, se renombran (los gastos ya anotados cambian de nombre con ellos) y se archivan cuando ya no se usan, para que no
/// salgan al anotar uno nuevo. Un tipo con gastos no se borra; uno sin gastos, sí.
/// </summary>
public sealed class TipoGasto
{
    public const int LongitudNombre = 80;

    /// <summary>Lo más que se puede tener, entre activos y archivados.</summary>
    public const int Maximo = 100;

    private TipoGasto()
    {
    }

    public int Id { get; private set; }

    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Si sale al anotar un gasto. Los archivados los conservan los gastos que ya los llevaban.</summary>
    public bool Activo { get; private set; }

    /// <summary>Orden en el desplegable y en el resumen del Excel («Otros», al final).</summary>
    public int Orden { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    public static TipoGasto Crear(string? nombre, int orden, DateTime ahora) =>
        new() { Nombre = Validar(nombre), Activo = true, Orden = orden, CreadoEn = ahora, ActualizadoEn = ahora };

    public void Renombrar(string? nombre, DateTime ahora)
    {
        Nombre = Validar(nombre);
        ActualizadoEn = ahora;
    }

    public void Archivar(DateTime ahora)
    {
        Activo = false;
        ActualizadoEn = ahora;
    }

    public void Activar(DateTime ahora)
    {
        Activo = true;
        ActualizadoEn = ahora;
    }

    private static string Validar(string? nombre) => Texto.Obligatorio(nombre, LongitudNombre, "El nombre del tipo", nameof(Nombre));
}
