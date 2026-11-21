using ClosedXML.Excel;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Domain.Gastos;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Infrastructure.Exportacion;

/// <summary>
/// Libro de Excel para la gestoría. Empieza por un «Resumen» que separa, cada uno con su color, lo <b>facturado</b> (lo que
/// se ha hecho: servicios y productos, azul), lo <b>cobrado</b> (lo que ha entrado, en efectivo y con tarjeta, verde; lo
/// pendiente, naranja), los <b>gastos</b> (lo que ha salido, rojo) y el resultado. Después, la hoja «Visitas» (una fila por
/// visita, con las columnas de facturado, cobrado y pendiente con los mismos colores) y, si los hay, «Productos» (una fila
/// por producto y visita) y «Gastos» (una fila por gasto).
/// </summary>
internal sealed class ExportadorExcelVisitas(TimeProvider reloj) : IExportadorVisitas
{
    private const string FormatoEuros = "#,##0.00 €";

    /// <summary>Colores de cada bloque: el fuerte para las cabeceras y el suave para el fondo de sus filas.</summary>
    private static readonly (XLColor Fuerte, XLColor Suave) Facturado = (XLColor.FromHtml("#2F5FC4"), XLColor.FromHtml("#E8EFFC"));
    private static readonly (XLColor Fuerte, XLColor Suave) Cobrado = (XLColor.FromHtml("#1E7F52"), XLColor.FromHtml("#E5F4EC"));
    private static readonly (XLColor Fuerte, XLColor Suave) Pendiente = (XLColor.FromHtml("#B45F06"), XLColor.FromHtml("#FDF0E1"));
    private static readonly (XLColor Fuerte, XLColor Suave) Gastos = (XLColor.FromHtml("#B3261E"), XLColor.FromHtml("#FCE8E6"));
    private static readonly (XLColor Fuerte, XLColor Suave) Resultado = (XLColor.FromHtml("#5B3FA8"), XLColor.FromHtml("#EEE9F9"));

    private static readonly string[] Cabeceras =
        ["Fecha", "Hora", "Cliente", "Servicios", "Productos", "Facturado", "Estado", "Forma de pago", "Cobrado en efectivo", "Cobrado con tarjeta",
            "Total cobrado", "Pendiente de cobro"];

    private const int ColFacturado = 6, ColEfectivo = 9, ColTarjeta = 10, ColCobrado = 11, ColPendiente = 12;

    private static readonly string[] CabecerasProductos =
        ["Fecha", "Hora", "Cliente", "Referencia", "Producto", "Uds", "Precio", "Importe", "Estado", "Uso"];

    private static readonly string[] CabecerasGastos =
        ["Fecha", "Hora", "Tipo", "Concepto", "Forma de pago", "Importe", "Nota"];

    public ArchivoGenerado Exportar(IReadOnlyList<Visita> visitas, IReadOnlyList<Gasto> gastos, string descripcionPeriodo)
    {
        using var libro = new XLWorkbook();
        HojaResumen(libro, visitas, gastos, descripcionPeriodo);

        var hoja = libro.Worksheets.Add("Visitas");
        Titulo(hoja, $"Visitas · {descripcionPeriodo}");
        const int filaCabecera = 3;
        if (visitas.Count == 0)
            hoja.Cell(filaCabecera, 1).Value = "No hubo visitas en este periodo.";
        else
            HojaVisitas(hoja, visitas, filaCabecera);

        if (visitas.Any(v => v.Productos.Count > 0))
            HojaProductos(libro, visitas, descripcionPeriodo);
        if (gastos.Count > 0)
            HojaGastos(libro, gastos, descripcionPeriodo);

        using var memoria = new MemoryStream();
        libro.SaveAs(memoria);
        var nombre = $"{(gastos.Count > 0 ? "Visitas y gastos" : "Visitas")} {descripcionPeriodo.Replace('/', '-')} ({reloj.GetLocalNow():yyyy-MM-dd HHmm}).xlsx";
        return new ArchivoGenerado(memoria.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    private static void Titulo(IXLWorksheet hoja, string texto)
    {
        hoja.Cell(1, 1).Value = texto;
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
    }

    /// <summary>Una franja de color encima de las cabeceras («FACTURADO», «COBRADO»…) y el fondo suave en su cabecera.</summary>
    private static void Franja(IXLWorksheet hoja, int fila, int desde, int hasta, string texto, (XLColor Fuerte, XLColor Suave) color)
    {
        var franja = hoja.Range(fila, desde, fila, hasta);
        if (hasta > desde)
            franja.Merge();
        franja.FirstCell().Value = texto;
        franja.Style.Fill.SetBackgroundColor(color.Fuerte).Font.SetFontColor(XLColor.White).Font.SetBold()
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        hoja.Range(fila + 1, desde, fila + 1, hasta).Style.Fill.SetBackgroundColor(color.Suave).Font.SetFontColor(color.Fuerte).Font.SetBold();
    }

    private static void HojaVisitas(IXLWorksheet hoja, IReadOnlyList<Visita> visitas, int filaCabecera)
    {
        for (var i = 0; i < Cabeceras.Length; i++)
            hoja.Cell(filaCabecera, i + 1).Value = Cabeceras[i];

        var fila = filaCabecera + 1;
        foreach (var v in visitas)
        {
            var efectivo = v.EstaCobrada ? v.TotalEfectivo ?? 0 : 0;
            var tarjeta = v.EstaCobrada ? v.TotalTarjeta ?? 0 : 0;
            hoja.Cell(fila, 1).Value = v.Fecha.Date;
            hoja.Cell(fila, 2).Value = v.Fecha.ToString("HH:mm");
            hoja.Cell(fila, 3).Value = v.Cliente?.NombreCompleto ?? "—";
            hoja.Cell(fila, 4).Value = string.Join(", ", v.Lineas.Select(l => l.Nombre));
            hoja.Cell(fila, 5).Value = string.Join(", ", v.Productos.Select(p => (p.Cantidad == 1 ? p.Nombre : $"{p.Cantidad} × {p.Nombre}") + (p.Incluido ? " (incluido)" : "")));
            hoja.Cell(fila, ColFacturado).Value = v.Total;
            hoja.Cell(fila, 7).Value = v.EstaCobrada ? "Cobrada" : "Pendiente";
            hoja.Cell(fila, 8).Value = v.EstaCobrada ? v.FormaPago.ToString() : string.Empty;
            hoja.Cell(fila, ColEfectivo).Value = efectivo;
            hoja.Cell(fila, ColTarjeta).Value = tarjeta;
            hoja.Cell(fila, ColCobrado).Value = efectivo + tarjeta;
            hoja.Cell(fila, ColPendiente).Value = v.EstaCobrada ? 0 : v.Total;
            fila++;
        }

        int[] importes = [ColFacturado, ColEfectivo, ColTarjeta, ColCobrado, ColPendiente];
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
        tabla.Theme = XLTableTheme.TableStyleLight1;
        tabla.ShowAutoFilter = true;

        // Lo facturado (azul), lo cobrado (verde) y lo pendiente (naranja), cada uno con su franja y su color
        Franja(hoja, filaCabecera - 1, ColFacturado, ColFacturado, "FACTURADO", Facturado);
        Franja(hoja, filaCabecera - 1, ColEfectivo, ColCobrado, "COBRADO", Cobrado);
        Franja(hoja, filaCabecera - 1, ColPendiente, ColPendiente, "PENDIENTE", Pendiente);
        hoja.Range(filaCabecera + 1, ColFacturado, fila, ColFacturado).Style.Font.SetFontColor(Facturado.Fuerte);
        hoja.Range(filaCabecera + 1, ColEfectivo, fila, ColCobrado).Style.Font.SetFontColor(Cobrado.Fuerte);
        hoja.Range(filaCabecera + 1, ColPendiente, fila, ColPendiente).Style.Font.SetFontColor(Pendiente.Fuerte);
        hoja.Range(filaCabecera + 1, ColCobrado, fila, ColCobrado).Style.Font.SetBold();

        hoja.SheetView.FreezeRows(filaCabecera);
        hoja.Columns().AdjustToContents();
        hoja.Column(4).Width = Math.Min(hoja.Column(4).Width, 60);
        hoja.Column(5).Width = Math.Min(hoja.Column(5).Width, 60);
    }

    /// <summary>Una fila por gasto (pagos del salón), con su total: separados de las visitas, que son cobros.</summary>
    private static void HojaGastos(XLWorkbook libro, IReadOnlyList<Gasto> gastos, string descripcionPeriodo)
    {
        var hoja = libro.Worksheets.Add("Gastos");
        Titulo(hoja, $"Gastos · {descripcionPeriodo}");

        const int filaCabecera = 3;
        for (var i = 0; i < CabecerasGastos.Length; i++)
            hoja.Cell(filaCabecera, i + 1).Value = CabecerasGastos[i];

        var fila = filaCabecera + 1;
        foreach (var g in gastos)
        {
            hoja.Cell(fila, 1).Value = g.Fecha.Date;
            hoja.Cell(fila, 2).Value = g.Fecha.ToString("HH:mm");
            hoja.Cell(fila, 3).Value = g.NombreTipo;
            hoja.Cell(fila, 4).Value = g.Concepto;
            hoja.Cell(fila, 5).Value = Gasto.Nombre(g.MedioPago);
            hoja.Cell(fila, 6).Value = g.Importe;
            hoja.Cell(fila, 7).Value = g.Nota ?? string.Empty;
            fila++;
        }

        var ultimaFila = fila - 1;
        hoja.Cell(fila, 5).Value = "Total";
        hoja.Cell(fila, 6).FormulaA1 = $"SUM(F{filaCabecera + 1}:F{ultimaFila})";
        hoja.Row(fila).Style.Font.SetBold();
        hoja.Range(fila, 1, fila, CabecerasGastos.Length).Style.Border.SetTopBorder(XLBorderStyleValues.Thin);

        hoja.Column(1).Style.DateFormat.Format = "dd/mm/yyyy";
        hoja.Column(6).Style.NumberFormat.Format = FormatoEuros;

        var tabla = hoja.Range(filaCabecera, 1, ultimaFila, CabecerasGastos.Length).CreateTable("Gastos");
        tabla.Theme = XLTableTheme.TableStyleLight1;
        tabla.ShowAutoFilter = true;
        Franja(hoja, filaCabecera - 1, 1, CabecerasGastos.Length, "GASTOS · lo que ha salido", Gastos);
        hoja.Range(filaCabecera + 1, 6, fila, 6).Style.Font.SetFontColor(Gastos.Fuerte);

        hoja.SheetView.FreezeRows(filaCabecera);
        hoja.Columns().AdjustToContents();
        hoja.Column(4).Width = Math.Min(hoja.Column(4).Width, 60);
        hoja.Column(7).Width = Math.Min(hoja.Column(7).Width, 60);
    }

    /// <summary>
    /// Lo facturado (servicios y productos), lo cobrado (por forma de pago) y lo pendiente, los gastos (por tipo y por forma
    /// de pago) y el resultado, cada bloque con su color.
    /// </summary>
    private static void HojaResumen(XLWorkbook libro, IReadOnlyList<Visita> visitas, IReadOnlyList<Gasto> gastos, string descripcionPeriodo)
    {
        var hoja = libro.Worksheets.Add("Resumen");
        hoja.Cell(1, 1).Value = $"Resumen · {descripcionPeriodo}";
        hoja.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(16);
        hoja.Cell(2, 1).Value = "Facturado: lo que se ha hecho (servicios y productos). Cobrado: lo que ha entrado (efectivo y tarjeta). Gastos: lo que ha salido.";
        hoja.Cell(2, 1).Style.Font.SetItalic().Font.SetFontColor(XLColor.FromHtml("#6B6B6B"));

        var cobradas = visitas.Where(v => v.EstaCobrada).ToList();
        var pendientes = visitas.Where(v => !v.EstaCobrada).ToList();
        var servicios = visitas.Sum(v => v.TotalServicios);
        var productos = visitas.Sum(v => v.TotalProductos);
        var facturado = servicios + productos;
        var efectivo = cobradas.Sum(v => v.TotalEfectivo ?? 0);
        var tarjeta = cobradas.Sum(v => v.TotalTarjeta ?? 0);
        var pendiente = pendientes.Sum(v => v.Total);
        var totalGastos = gastos.Sum(g => g.Importe);
        var gastosEfectivo = gastos.Where(g => g.MedioPago == MedioPagoGasto.Efectivo).Sum(g => g.Importe);

        var fila = 4;
        (XLColor Fuerte, XLColor Suave) color = default;
        void Bloque(string titulo, string subtitulo, (XLColor Fuerte, XLColor Suave) colorBloque)
        {
            color = colorBloque;
            var cabecera = hoja.Range(fila, 1, fila, 3);
            cabecera.Style.Fill.SetBackgroundColor(color.Fuerte).Font.SetFontColor(XLColor.White).Font.SetBold();
            hoja.Cell(fila, 1).Value = titulo;
            hoja.Cell(fila, 2).Value = subtitulo;
            hoja.Cell(fila, 2).Style.Font.SetBold(false).Font.SetItalic();
            fila++;
        }

        void Linea(string texto, decimal importe, string? nota = null, bool total = false, XLColor? letra = null)
        {
            var filaRango = hoja.Range(fila, 1, fila, 3);
            filaRango.Style.Fill.SetBackgroundColor(color.Suave);
            hoja.Cell(fila, 1).Value = texto;
            hoja.Cell(fila, 2).Value = importe;
            hoja.Cell(fila, 2).Style.Font.SetFontColor(letra ?? color.Fuerte);
            if (nota is not null)
            {
                hoja.Cell(fila, 3).Value = nota;
                hoja.Cell(fila, 3).Style.Font.SetFontColor(XLColor.FromHtml("#6B6B6B"));
            }
            if (total)
            {
                filaRango.Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin).Border.SetTopBorderColor(color.Fuerte);
                hoja.Cell(fila, 3).Style.Font.SetBold(false);
            }
            fila++;
        }

        void Subtitulo(string texto)
        {
            hoja.Range(fila, 1, fila, 3).Style.Fill.SetBackgroundColor(color.Suave);
            hoja.Cell(fila, 1).Value = texto;
            hoja.Cell(fila, 1).Style.Font.SetItalic().Font.SetFontColor(color.Fuerte);
            fila++;
        }

        string Visitas(int n) => n == 1 ? "1 visita" : $"{n} visitas";

        Bloque("FACTURADO", "lo que se ha hecho", Facturado);
        Linea("Servicios", servicios);
        Linea("Productos vendidos", productos);
        Linea("Total facturado", facturado, Visitas(visitas.Count), total: true);
        fila++;

        Bloque("COBRADO", "lo que ha entrado", Cobrado);
        Linea("Efectivo", efectivo);
        Linea("Tarjeta", tarjeta);
        Linea("Total cobrado", efectivo + tarjeta, Visitas(cobradas.Count), total: true);
        Linea("Pendiente de cobro", pendiente, pendientes.Count == 0 ? "nada pendiente" : Visitas(pendientes.Count), letra: Pendiente.Fuerte);
        fila++;

        Bloque("GASTOS", "lo que ha salido", Gastos);
        if (gastos.Count == 0)
            Subtitulo("No hubo gastos en este periodo.");
        foreach (var grupo in gastos.GroupBy(g => g.TipoId).OrderBy(g => g.First().Tipo?.Orden ?? int.MaxValue).ThenBy(g => g.First().NombreTipo))
            Linea(grupo.First().NombreTipo, grupo.Sum(g => g.Importe), grupo.Count() == 1 ? "1 gasto" : $"{grupo.Count()} gastos");
        Linea("Total gastos", totalGastos, gastos.Count == 1 ? "1 gasto" : $"{gastos.Count} gastos", total: true);
        if (gastos.Count > 0)
        {
            Subtitulo("Por forma de pago");
            foreach (var grupo in gastos.GroupBy(g => g.MedioPago).OrderBy(g => g.Key))
                Linea(Gasto.Nombre(grupo.Key), grupo.Sum(g => g.Importe));
        }
        fila++;

        Bloque("RESULTADO", "lo que queda", Resultado);
        Linea("Facturado − gastos", facturado - totalGastos);
        Linea("Cobrado − gastos", efectivo + tarjeta - totalGastos, "lo que ha entrado menos lo que ha salido", total: true);
        Linea("Efectivo: cobrado − gastos pagados en efectivo", efectivo - gastosEfectivo, "lo que debería cuadrar con la caja");

        hoja.Column(2).Style.NumberFormat.Format = FormatoEuros;
        hoja.Column(1).Width = 46;
        hoja.Column(2).Width = 16;
        hoja.Column(3).Width = 40;
        hoja.Cell(2, 1).Style.Alignment.SetWrapText(false);
        hoja.SheetView.FreezeRows(2);
    }

    private static void HojaProductos(XLWorkbook libro, IReadOnlyList<Visita> visitas, string descripcionPeriodo)
    {
        var hoja = libro.Worksheets.Add("Productos");
        Titulo(hoja, $"Productos de las visitas · {descripcionPeriodo}");

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
