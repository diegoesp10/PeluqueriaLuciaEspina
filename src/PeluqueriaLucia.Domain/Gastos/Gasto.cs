using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Domain.Gastos;

/// <summary>Cómo se pagó un gasto. Los valores numéricos están guardados en la base de datos: no cambiarlos.</summary>
public enum MedioPagoGasto
{
    Efectivo = 1,
    Tarjeta = 2,
    Transferencia = 3,

    /// <summary>Recibo domiciliado en el banco.</summary>
    Domiciliacion = 4,

    Bizum = 5,
}

/// <summary>Lo que se escribe de un gasto.</summary>
public sealed record DatosGasto(DateTime Fecha, int TipoId, string? Concepto, decimal Importe, MedioPagoGasto MedioPago, string? Nota = null);

/// <summary>
/// Un pago del salón (no un cobro): el sueldo de una empleada, la cuota de autónomos, unas dietas, un viaje… Sale en el
/// listado de visitas, en su día, y en el Excel en su propia hoja, para mandárselo al asesor con el resto de las cuentas. Su
/// tipo es uno de los que edita el salón (<see cref="TipoGasto"/>).
/// </summary>
public sealed class Gasto
{
    public const int LongitudConcepto = 200;
    public const int LongitudNota = 500;
    public const decimal ImporteMaximo = 999_999.99m;

    private Gasto()
    {
    }

    public int Id { get; private set; }

    /// <summary>Cuándo se pagó (hora local del salón).</summary>
    public DateTime Fecha { get; private set; }

    public int TipoId { get; private set; }

    /// <summary>Su tipo, de los que edita el salón. Se carga al listar y al exportar.</summary>
    public TipoGasto? Tipo { get; private set; }

    /// <summary>El nombre de su tipo («Dietas», «Viajes y desplazamientos»…).</summary>
    public string NombreTipo => Tipo?.Nombre ?? "Sin tipo";

    /// <summary>Qué se pagó y a quién: «Nómina de octubre», «Billete de tren a Madrid (feria)».</summary>
    public string Concepto { get; private set; } = string.Empty;

    public decimal Importe { get; private set; }

    public MedioPagoGasto MedioPago { get; private set; }

    public string? Nota { get; private set; }

    public DateTime CreadoEn { get; private set; }

    public DateTime ActualizadoEn { get; private set; }

    /// <param name="tipo">El tipo elegido (<see cref="DatosGasto.TipoId"/>), o null si no existe.</param>
    public static Gasto Crear(DatosGasto datos, TipoGasto? tipo, DateTime ahora)
    {
        var gasto = new Gasto { CreadoEn = ahora };
        gasto.Editar(datos, tipo, ahora);
        return gasto;
    }

    /// <param name="tipo">El tipo elegido (<see cref="DatosGasto.TipoId"/>), o null si no existe.</param>
    public void Editar(DatosGasto datos, TipoGasto? tipo, DateTime ahora)
    {
        ArgumentNullException.ThrowIfNull(datos);
        if (datos.Fecha.Year < 2000 || datos.Fecha.Year > 9998)
            throw new ReglaDeNegocioException("Indica la fecha del gasto.", nameof(Fecha));
        if (tipo is null || tipo.Id != datos.TipoId)
            throw new ReglaDeNegocioException("Elige el tipo de gasto.", nameof(TipoId));
        // Un tipo archivado ya no se ofrece, pero los gastos que lo llevaban lo conservan al corregirlos
        if (!tipo.Activo && tipo.Id != TipoId)
            throw new ReglaDeNegocioException($"«{tipo.Nombre}» está archivado: elige otro tipo o vuelve a activarlo en «Tipos de gasto».", nameof(TipoId));
        var concepto = Texto.Obligatorio(datos.Concepto, LongitudConcepto, "El concepto", nameof(Concepto));
        if (datos.Importe <= 0)
            throw new ReglaDeNegocioException("El importe tiene que ser mayor que cero.", nameof(Importe));
        if (datos.Importe > ImporteMaximo)
            throw new ReglaDeNegocioException($"El importe no puede pasar de {ImporteMaximo:N2} €.", nameof(Importe));
        if (decimal.Round(datos.Importe, 2) != datos.Importe)
            throw new ReglaDeNegocioException("El importe admite como mucho dos decimales (céntimos).", nameof(Importe));
        if (!Enum.IsDefined(datos.MedioPago))
            throw new ReglaDeNegocioException("Elige cómo se pagó.", nameof(MedioPago));
        var nota = Texto.Opcional(datos.Nota);
        if (nota?.Length > LongitudNota)
            throw new ReglaDeNegocioException($"La nota admite hasta {LongitudNota} caracteres.", nameof(Nota));

        Fecha = datos.Fecha.AddSeconds(-datos.Fecha.Second).AddMilliseconds(-datos.Fecha.Millisecond).AddMicroseconds(-datos.Fecha.Microsecond);
        TipoId = tipo.Id;
        Tipo = tipo;
        Concepto = concepto;
        Importe = datos.Importe;
        MedioPago = datos.MedioPago;
        Nota = nota;
        ActualizadoEn = ahora;
    }

    public static string Nombre(MedioPagoGasto medio) => medio switch
    {
        MedioPagoGasto.Efectivo => "Efectivo",
        MedioPagoGasto.Tarjeta => "Tarjeta",
        MedioPagoGasto.Transferencia => "Transferencia",
        MedioPagoGasto.Domiciliacion => "Domiciliación",
        _ => "Bizum",
    };
}
