using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Tests.Dominio;

/// <summary>Productos vendidos en visitas: se comprueba el stock al añadirlos y se descuenta al cobrar.</summary>
public sealed class VisitaProductosTests
{
    private static readonly DateTime Ahora = new(2026, 10, 17, 11, 0, 0);

    private static Cliente Ana() => Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, Ahora);

    private static Servicio Corte() => Servicio.Crear("Corte", 18m, Sexo.Mujer, Ahora);

    [Fact]
    public void Una_visita_puede_ser_solo_de_productos_y_el_total_suma_servicios_y_productos()
    {
        var champu = ProductoTests.Guardado(1, stock: 5, precio: 14.90m);
        var mascarilla = ProductoTests.Guardado(2, stock: 5, nombre: "Mascarilla", precio: 9.95m);

        var soloProductos = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [], productos: [new(champu, 2)]);
        var mixta = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(champu, 1, 12m), new(mascarilla, 1)]);

        Assert.Equal(29.80m, soloProductos.Total);
        Assert.Equal((18m, 21.95m, 39.95m, 2), (mixta.TotalServicios, mixta.TotalProductos, mixta.Total, mixta.UnidadesProductos));
        var linea = Assert.Single(soloProductos.Productos);
        Assert.Equal(("REF-1", "Champú de argán", 14.90m, 2, 29.80m), (linea.Referencia, linea.Nombre, linea.PrecioUnitario, linea.Cantidad, linea.Importe));
    }

    [Fact]
    public void Sin_servicios_ni_productos_no_se_registra()
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [], productos: []));

        Assert.Equal("Servicios", ex.Campo);
        Assert.Contains("servicio o un producto", ex.Message);
    }

    [Fact]
    public void El_mismo_producto_al_mismo_precio_suma_unidades_y_a_otro_precio_va_en_otra_linea()
    {
        var champu = ProductoTests.Guardado(1, stock: 10);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);

        visita.AgregarProducto(champu, 1);
        visita.AgregarProducto(champu, 2);
        visita.AgregarProducto(champu, 1, 10m);

        Assert.Equal([(3, 14.90m), (1, 10m)], visita.Productos.Select(p => (p.Cantidad, p.PrecioUnitario)));
        Assert.Equal(18m + 3 * 14.90m + 10m, visita.Total);
    }

    [Fact]
    public void Se_puede_pedir_mas_de_lo_que_hay_pero_no_un_producto_archivado()
    {
        var champu = ProductoTests.Guardado(1, stock: 2);
        var archivado = ProductoTests.Guardado(2, stock: 5, nombre: "Laca");
        archivado.Archivar(Ahora);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(champu, 2)]);

        visita.AgregarProducto(champu, 1);
        var deBaja = Assert.Throws<ReglaDeNegocioException>(() => visita.AgregarProducto(archivado, 1));

        Assert.Contains("archivado", deBaja.Message);
        Assert.Equal(3, visita.UnidadesProductos);
        Assert.Equal(2, champu.Stock); // pendiente de cobro: el stock aún no cambia
    }

    [Fact]
    public void Cobrar_descuenta_el_stock_con_un_movimiento_por_producto()
    {
        var champu = ProductoTests.Guardado(1, stock: 10);
        var mascarilla = ProductoTests.Guardado(2, stock: 3, nombre: "Mascarilla", precio: 9.95m);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()],
            productos: [new(champu, 2), new(champu, 1, 10m), new(mascarilla, 3)]);

        var movimientos = visita.Cobrar(FormaPago.Tarjeta, null, null, [champu, mascarilla], Ahora.AddMinutes(30), administradorId: 4);

        Assert.True(visita.EstaCobrada);
        Assert.Equal(visita.Total, visita.TotalTarjeta);
        Assert.Equal((7, 0), (champu.Stock, mascarilla.Stock));
        Assert.Equal([(-3, 7), (-3, 0)], movimientos.Select(m => (m.Cantidad, m.StockResultante)));
        Assert.All(movimientos, m => Assert.Equal((TipoMovimientoStock.Venta, Ahora.AddMinutes(30), 4), (m.Tipo, m.Fecha, m.AdministradorId)));
    }

    [Fact]
    public void Si_falta_stock_al_cobrar_se_cobra_y_el_stock_queda_en_negativo()
    {
        var champu = ProductoTests.Guardado(1, stock: 5);
        var mascarilla = ProductoTests.Guardado(2, stock: 2, nombre: "Mascarilla");
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [], productos: [new(champu, 1), new(mascarilla, 2)]);
        mascarilla.RegistrarUsoInterno(1, "Se gastó en el salón", Ahora, null);

        var movimientos = visita.Cobrar(FormaPago.Efectivo, null, null, [champu, mascarilla]);

        Assert.True(visita.EstaCobrada);
        Assert.Equal((4, -1), (champu.Stock, mascarilla.Stock));
        Assert.Equal(-1, movimientos.Single(m => m.Producto == mascarilla).StockResultante);
    }

    [Fact]
    public void Los_productos_usados_en_el_servicio_salen_del_stock_al_cobrar_sin_cobrarse()
    {
        var tinte = ProductoTests.Guardado(1, stock: 1, nombre: "Tinte 7.3", precio: 12m);
        var oxidante = ProductoTests.Guardado(2, stock: 10, nombre: "Oxidante 20 vol", precio: 8m);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);

        var movimientos = visita.Cobrar(FormaPago.Tarjeta, null, null, [tinte, oxidante], Ahora.AddHours(1), 3,
            [new(tinte, 1), new(oxidante, 1), new(tinte, 1)]);

        Assert.Equal((18m, 18m), (visita.Total, visita.TotalTarjeta));
        Assert.Empty(visita.Productos);
        Assert.Equal((-1, 9), (tinte.Stock, oxidante.Stock));
        Assert.Equal([(TipoMovimientoStock.UsoInterno, -2), (TipoMovimientoStock.UsoInterno, -1)], movimientos.Select(m => (m.Tipo, m.Cantidad)));
        Assert.All(movimientos, m => Assert.Equal((visita.Id, 3, "Usado en el servicio"), (m.VisitaId!.Value, m.AdministradorId!.Value, m.Nota)));
    }

    [Fact]
    public void Unidades_usadas_no_validas_no_cobran_nada()
    {
        var tinte = ProductoTests.Guardado(1, stock: 3, nombre: "Tinte 7.3");
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);

        var ex = Assert.Throws<ReglaDeNegocioException>(() => visita.Cobrar(FormaPago.Efectivo, null, null, [tinte], usados: [new(tinte, 0)]));

        Assert.Equal("Usados", ex.Campo);
        Assert.False(visita.EstaCobrada);
        Assert.Equal(3, tinte.Stock);
    }

    [Fact]
    public void Cobrar_productos_sin_cargarlos_es_un_error_de_programacion()
    {
        var champu = ProductoTests.Guardado(1, stock: 5);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [], productos: [new(champu, 1)]);

        Assert.Throws<InvalidOperationException>(() => visita.Cobrar(FormaPago.Efectivo, null, null));
        Assert.False(visita.EstaCobrada);
    }

    [Fact]
    public void Una_visita_cobrada_no_cambia_sus_productos()
    {
        var champu = ProductoTests.Guardado(1, stock: 5);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(champu, 1)]);
        visita.Cobrar(FormaPago.Efectivo, null, null, [champu]);

        Assert.Throws<ReglaDeNegocioException>(() => visita.AgregarProducto(champu, 1));
        Assert.Throws<ReglaDeNegocioException>(() => visita.QuitarProducto(visita.Productos[0].Id));
        Assert.Equal(4, champu.Stock);
    }

    [Fact]
    public void Quitar_el_ultimo_producto_de_una_visita_sin_servicios_no_se_permite()
    {
        var champu = ProductoTests.Guardado(1, stock: 5);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [], productos: [new(champu, 1)]);

        var ex = Assert.Throws<ReglaDeNegocioException>(() => visita.QuitarProducto(visita.Productos[0].Id));

        Assert.Contains("vacía", ex.Message);
        Assert.Single(visita.Productos);
    }
}
