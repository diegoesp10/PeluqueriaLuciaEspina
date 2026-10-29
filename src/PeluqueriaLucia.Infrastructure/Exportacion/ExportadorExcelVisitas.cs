using ClosedXML.Excel;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Exportacion;

/// <summary>
/// Libro de Excel con una fila por visita y una fila de totales, listo para la gestoría. Si hubo productos (vendidos o
/// usados en el servicio, que van incluidos a 0,00 €), una segunda hoja los detalla: una fila por producto y visita.
/// </summary>
internal sealed class ExportadorExcelVisitas(TimeProvider reloj) : IExportadorVisitas
{
    private const string FormatoEuros = "#,##0.00 €";

    private static readonly string[] Cabeceras =
        ["Fecha", "Hora", "Cliente", "Servicios", "Productos", "Total", "Estado", "Forma de pago", "Efectivo", "Tarjeta"];

    private static readonly string[] CabecerasProductos =
        ["Fecha", "Hora", "Cliente", "Referencia", "Producto", "Uds", "Precio", "Importe", "Estado", "Uso"];

    public ArchivoGenerado Exportar(IReadOnlyList<Visita> visitas, string descripcionPeriodo)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.Worksheets.Add("Visitas");

        hoja.Cell(1, 1).Value = $"Visitas · {descripcionPeriodo}";
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        const int filaCabecera = 3;
        for (var i = 0; i < Cabeceras.Length; i++)
            hoja.Cell(filaCabecera, i + 1).Value = Cabeceras[i];

        var fila = filaCabecera + 1;
        foreach (var v in visitas)
        {
            hoja.Cell(fila, 1).Value = v.Fecha.Date;
            hoja.Cell(fila, 2).Value = v.Fecha.ToString("HH:mm");
            hoja.Cell(fila, 3).Value = v.Cliente?.NombreCompleto ?? "—";
            hoja.Cell(fila, 4).Value = string.Join(", ", v.Lineas.Select(l => l.Nombre));
            hoja.Cell(fila, 5).Value = string.Join(", ", v.Productos.Select(p => (p.Cantidad == 1 ? p.Nombre : $"{p.Cantidad} × {p.Nombre}") + (p.Incluido ? " (incluido)" : "")));
            hoja.Cell(fila, 6).Value = v.Total;
            hoja.Cell(fila, 7).Value = v.EstaCobrada ? "Cobrada" : "Pendiente";
            hoja.Cell(fila, 8).Value = v.EstaCobrada ? v.FormaPago.ToString() : string.Empty;
            hoja.Cell(fila, 9).Value = v.EstaCobrada ? v.TotalEfectivo ?? 0 : 0;
            hoja.Cell(fila, 10).Value = v.EstaCobrada ? v.TotalTarjeta ?? 0 : 0;
            fila++;
        }

        int[] importes = [6, 9, 10];
        var ultimaFila = fila - 1;
        hoja.Cell(fila, 4).Value = "Total";
        foreach (var columna in importes)
            hoja.Cell(fila, columna).FormulaA1 = $"SUM({Letra(columna)}{filaCabecera + 1}:{Letra(columna)}{ultimaFila})";
        hoja.Row(fila).Style.Font.SetBold();
        hoja.Range(fila, 1, fila, Cabeceras.Length).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);

        hoja.Column(1).Style.DateFormat.Format = "dd/mm/yyyy";
        foreach (var columna in importes)
            hoja.Column(columna).Style.NumberFormat.Format = FormatoEuros;

        var tabla = hoja.Range(filaCabecera, 1, ultimaFila, Cabeceras.Length).CreateTable("Visitas");
        tabla.Theme = XLTableTheme.TableStyleLight9;
        tabla.ShowAutoFilter = true;

        hoja.SheetView.FreezeRows(filaCabecera);
        hoja.Columns().AdjustToContents();
        hoja.Column(4).Width = Math.Min(hoja.Column(4).Width, 60);
        hoja.Column(5).Width = Math.Min(hoja.Column(5).Width, 60);

        if (visitas.Any(v => v.Productos.Count > 0))
            HojaProductos(libro, visitas, descripcionPeriodo);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var nombre = $"Visitas {descripcionPeriodo.Replace('/', '-')} ({reloj.GetLocalNow():yyyy-MM-dd HHmm}).xlsx";
        return new ArchivoGenerado(memoria.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    private static void HojaProductos(XLWorkbook libro, IReadOnlyList<Visita> visitas, string descripcionPeriodo)
    {
        var hoja = libro.Worksheets.Add("Productos");
        hoja.Cell(1, 1).Value = $"Productos de las visitas · {descripcionPeriodo}";
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        const int filaCabecera = 3;
        for (var i = 0; i < CabecerasProductos.Length; i++)
            hoja.Cell(filaCabecera, i + 1).Value = CabecerasProductos[i];

        var fila = filaCabecera + 1;
        foreach (var v in visitas)
        {
            foreach (var p in v.Productos)
            {
                hoja.Cell(fila, 1).Value = v.Fecha.Date;
                hoja.Cell(fila, 2).Value = v.Fecha.ToString("HH:mm");
                hoja.Cell(fila, 3).Value = v.Cliente?.NombreCompleto ?? "—";
                hoja.Cell(fila, 4).Value = p.Referencia;
                hoja.Cell(fila, 5).Value = p.Nombre;
                hoja.Cell(fila, 6).Value = p.Cantidad;
                hoja.Cell(fila, 7).Value = p.PrecioUnitario;
                hoja.Cell(fila, 8).Value = p.Importe;
                hoja.Cell(fila, 9).Value = v.EstaCobrada ? "Cobrada" : "Pendiente";
                hoja.Cell(fila, 10).Value = p.Uso switch
                {
                    UsoProducto.ServicioTerminado => "En el servicio (terminado)",
                    UsoProducto.ServicioEnUso => "En el servicio (sigue en uso)",
                    _ => "Venta",
                };
                fila++;
            }
        }

        var ultimaFila = fila - 1;
        hoja.Cell(fila, 5).Value = "Total";
        foreach (var columna in new[] { 6, 8 })
            hoja.Cell(fila, columna).FormulaA1 = $"SUM({Letra(columna)}{filaCabecera + 1}:{Letra(columna)}{ultimaFila})";
        hoja.Row(fila).Style.Font.SetBold();
        hoja.Range(fila, 1, fila, CabecerasProductos.Length).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);

        hoja.Column(1).Style.DateFormat.Format = "dd/mm/yyyy";
        foreach (var columna in new[] { 7, 8 })
            hoja.Column(columna).Style.NumberFormat.Format = FormatoEuros;

        var tabla = hoja.Range(filaCabecera, 1, ultimaFila, CabecerasProductos.Length).CreateTable("Productos");
        tabla.Theme = XLTableTheme.TableStyleLight9;
        tabla.ShowAutoFilter = true;

        hoja.SheetView.FreezeRows(filaCabecera);
        hoja.Columns().AdjustToContents();
        hoja.Column(5).Width = Math.Min(hoja.Column(5).Width, 60);
    }

    private static string Letra(int columna) => XLHelper.GetColumnLetterFromNumber(columna);
}
