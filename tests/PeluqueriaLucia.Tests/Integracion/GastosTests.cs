using ClosedXML.Excel;
using PeluqueriaLucia.Application.Gastos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Gastos;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>
/// Gastos del salón contra SQL Server real: se anotan, se corrigen y se borran, salen en el listado de visitas mezclados
/// con ellas por fecha y en el Excel en su propia hoja, con un resumen que separa lo facturado, lo cobrado y lo pagado. Sus
/// tipos los edita el salón; la migración los crea con los de antes y los mismos números.
/// </summary>
public sealed class GastosTests : IAsyncLifetime
{
    // Los tipos que crea la migración, con los números de antes
    private const int Personal = 1, CuotaAutonomos = 2, Dietas = 3, Viajes = 4, Otros = 99;

    private static readonly DateOnly Dia = new(2026, 10, 10);
    private BaseDeDatosPrueba _bd = null!;

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync() => await _bd.DisposeAsync();

    private static DatosGasto Datos(DateTime fecha, decimal importe = 25.5m, int tipo = Dietas,
        MedioPagoGasto medio = MedioPagoGasto.Efectivo, string? concepto = "Comida en la feria", string? nota = null) =>
        new(fecha, tipo, concepto, importe, medio, nota);

    [Fact]
    public async Task La_migracion_crea_los_tipos_de_antes_con_sus_numeros_y_otros_al_final()
    {
        var tipos = await Gastos().TiposAsync();

        Assert.Equal(15, tipos.Count);
        Assert.All(tipos, t => Assert.True(t.Activo));
        Assert.Equal(("Personal (nóminas y Seguridad Social)", "Cuota de autónomos", "Dietas", "Viajes y desplazamientos"),
            (tipos.Single(t => t.Id == Personal).Nombre, tipos.Single(t => t.Id == CuotaAutonomos).Nombre, tipos.Single(t => t.Id == Dietas).Nombre,
                tipos.Single(t => t.Id == Viajes).Nombre));
        Assert.Equal((Otros, "Otros", GastosService.OrdenUltimo), (tipos[^1].Id, tipos[^1].Nombre, tipos[^1].Orden));
        Assert.Equal(Personal, tipos[0].Id);
    }

    [Fact]
    public async Task Un_gasto_se_anota_se_corrige_y_se_borra()
    {
        var creado = await Gastos().CrearAsync(Datos(new DateTime(2026, 10, 5, 13, 30, 45), nota: "  Factura 12  "));
        Assert.True(creado.Exito, creado.Error?.Mensaje);
        var gasto = (await Gastos().ObtenerAsync(creado.Valor))!;
        Assert.Equal((new DateTime(2026, 10, 5, 13, 30, 0), "Comida en la feria", 25.5m, "Factura 12", Dietas, "Dietas", MedioPagoGasto.Efectivo),
            (gasto.Fecha, gasto.Concepto, gasto.Importe, gasto.Nota, gasto.TipoId, gasto.NombreTipo, gasto.MedioPago));

        var editado = await Gastos().EditarAsync(creado.Valor, Datos(new DateTime(2026, 10, 6, 9, 0, 0), 30m, Viajes, MedioPagoGasto.Tarjeta, "Tren a Madrid"));
        Assert.True(editado.Exito, editado.Error?.Mensaje);
        gasto = (await Gastos().ObtenerAsync(creado.Valor))!;
        Assert.Equal((Viajes, MedioPagoGasto.Tarjeta, 30m, "Tren a Madrid", (string?)null), (gasto.TipoId, gasto.MedioPago, gasto.Importe, gasto.Concepto, gasto.Nota));

        Assert.True((await Gastos().EliminarAsync(creado.Valor)).Exito);
        Assert.Null(await Gastos().ObtenerAsync(creado.Valor));
        Assert.False((await Gastos().EliminarAsync(creado.Valor)).Exito);
        Assert.False((await Gastos().EditarAsync(creado.Valor, Datos(DateTime.Today))).Exito);
    }

    [Theory]
    [InlineData(0, "Importe")]
    [InlineData(-3, "Importe")]
    [InlineData(10.555, "Importe")]
    [InlineData(1000000, "Importe")]
    public async Task El_importe_tiene_que_ser_positivo_y_en_centimos(double importe, string campo)
    {
        var resultado = await Gastos().CrearAsync(Datos(DateTime.Today, (decimal)importe));

        Assert.Equal(campo, resultado.Error?.Campo);
        Assert.Equal(0, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM Gastos"));
    }

    [Fact]
    public async Task Hacen_falta_el_tipo_el_concepto_la_forma_de_pago_y_la_fecha()
    {
        Assert.Equal("TipoId", (await Gastos().CrearAsync(Datos(DateTime.Today, tipo: 50))).Error?.Campo);
        Assert.Equal("TipoId", (await Gastos().CrearAsync(Datos(DateTime.Today, tipo: 0))).Error?.Campo);
        Assert.Equal("Concepto", (await Gastos().CrearAsync(Datos(DateTime.Today, concepto: "  "))).Error?.Campo);
        Assert.Equal("Concepto", (await Gastos().CrearAsync(Datos(DateTime.Today, concepto: new string('x', Gasto.LongitudConcepto + 1)))).Error?.Campo);
        Assert.Equal("MedioPago", (await Gastos().CrearAsync(Datos(DateTime.Today, medio: (MedioPagoGasto)9))).Error?.Campo);
        Assert.Equal("Fecha", (await Gastos().CrearAsync(Datos(default))).Error?.Campo);
        Assert.Equal("Nota", (await Gastos().CrearAsync(Datos(DateTime.Today, nota: new string('x', Gasto.LongitudNota + 1)))).Error?.Campo);
    }

    [Fact]
    public async Task Los_tipos_se_añaden_se_renombran_y_los_gastos_cambian_de_nombre_con_ellos()
    {
        var creado = await Gastos().CrearTipoAsync("  Limpieza  ");
        Assert.True(creado.Exito, creado.Error?.Mensaje);
        var limpieza = creado.Valor;
        var tipos = await Gastos().TiposAsync();
        // Los nuevos van antes de «Otros», que sigue el último
        Assert.Equal(["Limpieza", "Otros"], tipos.TakeLast(2).Select(t => t.Nombre));

        // Sin repetir nombres (sin distinguir mayúsculas) y con nombre
        Assert.Contains("Ya hay un tipo «Dietas»", (await Gastos().CrearTipoAsync("dietas ")).Error?.Mensaje);
        Assert.Equal("Nombre", (await Gastos().CrearTipoAsync("   ")).Error?.Campo);
        Assert.Equal("Nombre", (await Gastos().CrearTipoAsync(new string('x', TipoGasto.LongitudNombre + 1))).Error?.Campo);

        var gasto = (await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(9, 0)), tipo: limpieza, concepto: "Productos de limpieza"))).Valor;
        Assert.True((await Gastos().RenombrarTipoAsync(limpieza, "Limpieza del local")).Exito);
        Assert.Equal("Limpieza del local", (await Gastos().ObtenerAsync(gasto))!.NombreTipo);
        Assert.Contains("Ya hay otro tipo «Dietas»", (await Gastos().RenombrarTipoAsync(limpieza, "DIETAS")).Error?.Mensaje);
        // Cambiarle solo las mayúsculas a uno mismo sí se puede
        Assert.True((await Gastos().RenombrarTipoAsync(Dietas, "dietas")).Exito);
        Assert.False((await Gastos().RenombrarTipoAsync(12345, "Nada")).Exito);
    }

    [Fact]
    public async Task Un_tipo_archivado_no_sirve_para_gastos_nuevos_pero_los_que_ya_lo_llevan_lo_conservan()
    {
        var gasto = (await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(9, 0)), tipo: Viajes))).Valor;

        Assert.True((await Gastos().ArchivarTipoAsync(Viajes, archivar: true)).Exito);
        var nuevo = await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(10, 0)), tipo: Viajes));
        Assert.Equal("TipoId", nuevo.Error?.Campo);
        Assert.Contains("está archivado", nuevo.Error?.Mensaje);
        // Corregir el gasto sin cambiarle el tipo, sí
        Assert.True((await Gastos().EditarAsync(gasto, Datos(Dia.ToDateTime(new TimeOnly(9, 0)), 31m, Viajes))).Exito);
        Assert.Equal((Viajes, 31m), ((await Gastos().ObtenerAsync(gasto))!.TipoId, (await Gastos().ObtenerAsync(gasto))!.Importe));
        // Los archivados van al final del listado de tipos
        var conUso = await Gastos().TiposConUsoAsync();
        Assert.Equal((Viajes, false, 1), (conUso[^1].Tipo.Id, conUso[^1].Tipo.Activo, conUso[^1].Gastos));

        Assert.True((await Gastos().ArchivarTipoAsync(Viajes, archivar: false)).Exito);
        Assert.True((await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(10, 0)), tipo: Viajes))).Exito);
    }

    [Fact]
    public async Task Solo_se_borran_los_tipos_sin_gastos_y_siempre_queda_uno_activo()
    {
        await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(9, 0)), tipo: Personal));

        var conGastos = await Gastos().EliminarTipoAsync(Personal);
        Assert.Contains("no se puede borrar, pero sí archivar", conGastos.Error?.Mensaje);
        Assert.True((await Gastos().EliminarTipoAsync(Seguros())).Exito);
        Assert.DoesNotContain(await Gastos().TiposAsync(), t => t.Id == Seguros());
        // La base de datos tampoco deja borrar un tipo con gastos
        await Assert.ThrowsAnyAsync<Exception>(() => _bd.EjecutarAsync($"DELETE FROM TiposGasto WHERE Id = {Personal}"));

        // Archivar todos menos uno: el último activo no se archiva ni se borra
        foreach (var tipo in (await Gastos().TiposAsync()).Where(t => t.Id != Otros))
            Assert.True((await Gastos().ArchivarTipoAsync(tipo.Id, archivar: true)).Exito);
        Assert.Contains("al menos un tipo activo", (await Gastos().ArchivarTipoAsync(Otros, archivar: true)).Error?.Mensaje);
        Assert.Contains("al menos un tipo activo", (await Gastos().EliminarTipoAsync(Otros)).Error?.Mensaje);

        static int Seguros() => 12;
    }

    [Fact]
    public async Task El_listado_mezcla_visitas_y_gastos_por_fecha_y_pagina_como_una_sola_lista()
    {
        var (ana, corte) = await ClienteYServicioAsync();
        await Visitas().RegistrarAsync(new NuevaVisita(ana, Dia.ToDateTime(new TimeOnly(10, 0)), FormaPago.Efectivo, [corte]));
        await Visitas().RegistrarAsync(new NuevaVisita(ana, Dia.ToDateTime(new TimeOnly(12, 0)), FormaPago.Efectivo, [corte]));
        await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(11, 0)), 40m, Personal, MedioPagoGasto.Transferencia, "Nómina"));
        await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(10, 0)), 5m));
        await Gastos().CrearAsync(Datos(Dia.AddDays(1).ToDateTime(new TimeOnly(9, 0)), 7m));

        var pagina = await Visitas().ListarApuntesAsync(FiltroVisitas.Dia(Dia), 1);
        // Lo más reciente primero; a la misma hora, la visita antes que el gasto
        Assert.Equal(4, pagina.TotalElementos);
        Assert.Equal(["v12:00", "g11:00", "v10:00", "g10:00"], pagina.Elementos.Select(Clave));
        // Cada gasto, con el nombre de su tipo
        Assert.Equal("Personal (nóminas y Seguridad Social)", pagina.Elementos[1].Gasto!.NombreTipo);

        await using (var db = _bd.Contexto())
        {
            var segunda = await new VisitaRepositorio(db).ListarApuntesAsync(FiltroVisitas.Dia(Dia), 2, 2, default);
            Assert.Equal((4, 2), (segunda.TotalElementos, segunda.TotalPaginas));
            Assert.Equal(["v10:00", "g10:00"], segunda.Elementos.Select(Clave));
            Assert.Equal("Ana Pérez", segunda.Elementos[0].Visita!.Cliente!.NombreCompleto);
            Assert.Single(segunda.Elementos[0].Visita!.Lineas);
        }

        // Los gastos no son de ningún cliente: en su ficha solo salen sus visitas
        var deAna = await Visitas().ListarApuntesAsync(FiltroVisitas.DeCliente(ana), 1);
        Assert.Equal(2, deAna.TotalElementos);
        Assert.All(deAna.Elementos, a => Assert.NotNull(a.Visita));

        Assert.Equal(new ResumenGastos(2, 45m, 5m), await Gastos().ResumirAsync(FiltroVisitas.Dia(Dia)));
        Assert.Equal(new ResumenGastos(3, 52m, 12m), await Gastos().ResumirAsync(FiltroVisitas.Mes(2026, 10)));
        Assert.Equal(ResumenGastos.Vacio, await Gastos().ResumirAsync(FiltroVisitas.Mes(2026, 9)));
        Assert.Equal(ResumenGastos.Vacio, await Gastos().ResumirAsync(FiltroVisitas.DeCliente(ana)));
        // Por tipo, de más a menos, para el panel
        Assert.Equal([new TotalPorTipo("Personal (nóminas y Seguridad Social)", 1, 40m), new TotalPorTipo("Dietas", 2, 12m)],
            await Gastos().TotalesPorTipoAsync(FiltroVisitas.Mes(2026, 10)));
    }

    [Fact]
    public async Task El_excel_separa_lo_facturado_lo_cobrado_y_los_gastos()
    {
        var (ana, corte) = await ClienteYServicioAsync();
        var cobrada = (await Visitas().RegistrarAsync(new NuevaVisita(ana, Dia.ToDateTime(new TimeOnly(10, 0)), FormaPago.Efectivo, [corte]))).Valor;
        await Visitas().RegistrarAsync(new NuevaVisita(ana, Dia.ToDateTime(new TimeOnly(12, 0)), FormaPago.Efectivo, [corte]));
        var cobro = await Visitas().CobrarAsync(cobrada, FormaPago.Efectivo, null, null);
        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(10, 0)), 5m, nota: "Ticket 7"));
        await Gastos().CrearAsync(Datos(Dia.ToDateTime(new TimeOnly(11, 0)), 40m, Personal, MedioPagoGasto.Transferencia, "Nómina"));
        await Gastos().CrearAsync(Datos(Dia.AddDays(1).ToDateTime(new TimeOnly(9, 0)), 7m));

        var archivo = await Visitas().ExportarAsync(FiltroVisitas.Mes(2026, 10));

        Assert.True(archivo.Exito, archivo.Error?.Mensaje);
        Assert.StartsWith("Visitas y gastos octubre de 2026", archivo.Valor!.NombreArchivo);
        using var libro = new XLWorkbook(new MemoryStream(archivo.Valor.Contenido));
        // El resumen, lo primero que se ve al abrirlo
        Assert.Equal(["Resumen", "Visitas", "Gastos"], libro.Worksheets.Select(h => h.Name));

        // Visitas: lo facturado, lo cobrado (en efectivo y con tarjeta) y lo pendiente, cada uno bajo su franja
        var visitas = libro.Worksheet("Visitas");
        Assert.Equal(["Fecha", "Hora", "Cliente", "Servicios", "Productos", "Facturado", "Estado", "Forma de pago", "Cobrado en efectivo",
            "Cobrado con tarjeta", "Total cobrado", "Pendiente de cobro"], Enumerable.Range(1, 12).Select(c => visitas.Cell(3, c).GetString()));
        Assert.Equal(("FACTURADO", "COBRADO", "PENDIENTE"), (visitas.Cell(2, 6).GetString(), visitas.Cell(2, 9).GetString(), visitas.Cell(2, 12).GetString()));
        var filas = visitas.Table("Visitas").DataRange.Rows().Select(f => (f.Cell(2).GetString(), f.Cell(6).GetValue<decimal>(), f.Cell(7).GetString(),
            f.Cell(9).GetValue<decimal>(), f.Cell(10).GetValue<decimal>(), f.Cell(11).GetValue<decimal>(), f.Cell(12).GetValue<decimal>())).ToList();
        Assert.Equal([("10:00", 20m, "Cobrada", 20m, 0m, 20m, 0m), ("12:00", 20m, "Pendiente", 0m, 0m, 0m, 20m)], filas);
        Assert.Equal(("Total", "SUM(F4:F5)", "SUM(K4:K5)", "SUM(L4:L5)"),
            (visitas.Cell(6, 4).GetString(), visitas.Cell(6, 6).FormulaA1, visitas.Cell(6, 11).FormulaA1, visitas.Cell(6, 12).FormulaA1));

        var gastos = libro.Worksheet("Gastos");
        Assert.Equal("GASTOS · lo que ha salido", gastos.Cell(2, 1).GetString());
        Assert.Equal(["Fecha", "Hora", "Tipo", "Concepto", "Forma de pago", "Importe", "Nota"], Enumerable.Range(1, 7).Select(c => gastos.Cell(3, c).GetString()));
        Assert.Equal(("10:00", "Dietas", "Comida en la feria", "Efectivo", 5m, "Ticket 7"),
            (gastos.Cell(4, 2).GetString(), gastos.Cell(4, 3).GetString(), gastos.Cell(4, 4).GetString(), gastos.Cell(4, 5).GetString(), gastos.Cell(4, 6).GetValue<decimal>(), gastos.Cell(4, 7).GetString()));
        Assert.Equal(("Personal (nóminas y Seguridad Social)", "Transferencia"), (gastos.Cell(5, 3).GetString(), gastos.Cell(5, 5).GetString()));
        Assert.Equal(("Total", "SUM(F4:F6)"), (gastos.Cell(7, 5).GetString(), gastos.Cell(7, 6).FormulaA1));

        var resumen = libro.Worksheet("Resumen");
        string[] Bloques() => [.. resumen.CellsUsed().Select(c => c.GetString()).Where(t => t is "FACTURADO" or "COBRADO" or "GASTOS" or "RESULTADO")];
        decimal[] Importes(string texto) => resumen.CellsUsed().Where(c => c.GetString() == texto).Select(c => c.CellRight().GetValue<decimal>()).ToArray();
        decimal Importe(string texto) => Assert.Single(Importes(texto));
        Assert.Equal(["FACTURADO", "COBRADO", "GASTOS", "RESULTADO"], Bloques());
        Assert.Equal((40m, 0m, 40m), (Importe("Servicios"), Importe("Productos vendidos"), Importe("Total facturado")));
        // «Efectivo» sale dos veces: lo cobrado en efectivo y lo pagado en efectivo
        Assert.Equal([20m, 12m], Importes("Efectivo"));
        Assert.Equal((0m, 20m, 20m), (Importe("Tarjeta"), Importe("Total cobrado"), Importe("Pendiente de cobro")));
        Assert.Equal((40m, 12m, 52m), (Importe("Personal (nóminas y Seguridad Social)"), Importe("Dietas"), Importe("Total gastos")));
        // Los tipos, en su orden (Personal antes que Dietas)
        var lineasGastos = resumen.CellsUsed().Select(c => c.GetString()).ToList();
        Assert.True(lineasGastos.IndexOf("Personal (nóminas y Seguridad Social)") < lineasGastos.IndexOf("Dietas"));
        Assert.Equal((40m, -12m, -32m, 8m), (Importe("Transferencia"), Importe("Facturado − gastos"), Importe("Cobrado − gastos"),
            Importe("Efectivo: cobrado − gastos pagados en efectivo")));

        // Un día solo con gastos también se exporta; uno sin nada, no
        var soloGastos = await Visitas().ExportarAsync(FiltroVisitas.Dia(Dia.AddDays(1)));
        Assert.True(soloGastos.Exito, soloGastos.Error?.Mensaje);
        using (var otro = new XLWorkbook(new MemoryStream(soloGastos.Valor!.Contenido)))
            Assert.Equal("No hubo visitas en este periodo.", otro.Worksheet("Visitas").Cell(3, 1).GetString());
        var nada = await Visitas().ExportarAsync(FiltroVisitas.Mes(2026, 9));
        Assert.Contains("No hay visitas ni gastos", nada.Error?.Mensaje);
        // Sin gastos, el resumen y las visitas
        var sinGastos = await Visitas().ExportarAsync(FiltroVisitas.DeCliente(ana));
        using (var deAna = new XLWorkbook(new MemoryStream(sinGastos.Valor!.Contenido)))
        {
            Assert.Equal(["Resumen", "Visitas"], deAna.Worksheets.Select(h => h.Name));
            Assert.Contains(deAna.Worksheet("Resumen").CellsUsed(), c => c.GetString() == "No hubo gastos en este periodo.");
        }
        Assert.StartsWith("Visitas todas las fechas", sinGastos.Valor.NombreArchivo);
    }

    private static string Clave(Apunte a) => (a.Visita is null ? "g" : "v") + a.Fecha.ToString("HH:mm");

    private GastosService Gastos()
    {
        var db = _bd.Contexto();
        return new GastosService(new GastoRepositorio(db), db, TimeProvider.System);
    }

    private VisitasService Visitas()
    {
        var db = _bd.Contexto();
        var reloj = TimeProvider.System;
        return new VisitasService(new VisitaRepositorio(db), new ClienteRepositorio(db), new ServicioRepositorio(db), new ProductoRepositorio(db),
            new ExportadorExcelVisitas(reloj), db, reloj, new GastoRepositorio(db));
    }

    private async Task<(int Cliente, int Servicio)> ClienteYServicioAsync()
    {
        await using var db = _bd.Contexto();
        var cliente = Cliente.Crear("Ana", "Pérez", null, null, Sexo.Mujer, DateTime.Now);
        var servicio = Servicio.Crear("Corte", 20m, Sexo.Mujer, DateTime.Now, "Cut");
        db.AddRange(cliente, servicio);
        await db.SaveChangesAsync();
        return (cliente.Id, servicio.Id);
    }
}
