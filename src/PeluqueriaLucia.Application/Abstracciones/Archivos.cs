using PeluqueriaLucia.Domain.Gastos;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Application.Abstracciones;

/// <summary>Una imagen recibida desde un formulario, sin depender de ASP.NET.</summary>
public sealed record ArchivoImagen(Stream Contenido, string NombreOriginal, long Tamano);

/// <summary>Guarda las fotos del catálogo y devuelve la ruta pública con la que se sirven.</summary>
public interface IAlmacenImagenes
{
    Task<string> GuardarAsync(Stream contenido, string extension, CancellationToken ct = default);

    /// <summary>Borra la imagen si existe. No falla si ya no está.</summary>
    void Eliminar(string? rutaPublica);
}

/// <summary>Un fichero generado para descargar.</summary>
public sealed record ArchivoGenerado(byte[] Contenido, string TipoContenido, string NombreArchivo);

/// <summary>Las cuentas de un periodo en Excel para el asesor: las visitas (cobros) y, en su propia hoja, los gastos (pagos).</summary>
public interface IExportadorVisitas
{
    ArchivoGenerado Exportar(IReadOnlyList<Visita> visitas, IReadOnlyList<Gasto> gastos, string descripcionPeriodo);
}
