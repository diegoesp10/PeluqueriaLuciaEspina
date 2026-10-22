using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;

namespace PeluqueriaLucia.Tests.Dominio;

public sealed class ProductoTests
{
    private static readonly DateTime Ahora = new(2026, 10, 17, 10, 0, 0);

    internal static FichaProducto Ficha(string referencia = "CH-ARG-250", string nombre = "Champú de argán", decimal precio = 14.90m,
        string? codigo = null, int minimo = 2, decimal? coste = 7.50m) =>
        new(referencia, codigo, nombre, "Eco Natura", "Champús", "Para cabello seco.", precio, coste, minimo);

    /// <summary>Producto con identificador, como si viniera de la base de datos.</summary>
    internal static Producto Guardado(int id, int stock = 10, string nombre = "Champú de argán", decimal precio = 14.90m)
    {
        var producto = Producto.Crear(Ficha($"REF-{id}", nombre, precio), Ahora);
        typeof(Producto).GetProperty(nameof(Producto.Id))!.SetValue(producto, id);
        producto.Alta(stock, Ahora, null);
        return producto;
    }

    [Fact]
    public void Crear_normaliza_referencia_y_codigo_de_barras()
    {
        var producto = Producto.Crear(Ficha(" ch-arg-250 ", codigo: "8412 3456 78901"), Ahora);

        Assert.Equal("CH-ARG-250", producto.Referencia);
        Assert.Equal("8412345678901", producto.CodigoBarras);
        Assert.Equal(0, producto.Stock);
        Assert.True(producto.Activo);
        Assert.Equal(7.40m, producto.Margen);
    }

    [Theory]
    [InlineData("", "Champú", 10, null, "Referencia")]
    [InlineData("REF<1>", "Champú", 10, null, "Referencia")]
    [InlineData("REF-1", " ", 10, null, "Nombre")]
    [InlineData("REF-1", "Champú", 0, null, "PrecioVenta")]
    [InlineData("REF-1", "Champú", 10000, null, "PrecioVenta")]
    [InlineData("REF-1", "Champú", 10, "12345", "CodigoBarras")]
    [InlineData("REF-1", "Champú", 10, "84123456789AB", "CodigoBarras")]
    public void Crear_con_datos_no_validos_falla_en_su_campo(string referencia, string nombre, decimal precio, string? codigo, string campo)
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => Producto.Crear(Ficha(referencia, nombre, precio, codigo), Ahora));
        Assert.Equal(campo, ex.Campo);
    }

    [Fact]
    public void El_alta_deja_un_movimiento_con_el_stock_inicial_y_sin_unidades_no_deja_ninguno()
    {
        var producto = Producto.Crear(Ficha(), Ahora);
        var vacio = Producto.Crear(Ficha("OTRO"), Ahora);

        var alta = producto.Alta(12, Ahora, 3);

        Assert.NotNull(alta);
        Assert.Equal(TipoMovimientoStock.Alta, alta.Tipo);
        Assert.Equal(12, alta.Cantidad);
        Assert.Equal(12, alta.StockResultante);
        Assert.Equal(3, alta.AdministradorId);
        Assert.Equal(12, producto.Stock);
        Assert.Null(vacio.Alta(0, Ahora, null));
        Assert.Equal("StockInicial", Assert.Throws<ReglaDeNegocioException>(() => vacio.Alta(-1, Ahora, null)).Campo);
    }

    [Fact]
    public void Entradas_usos_y_recuentos_cambian_el_stock_y_guardan_cuanto_y_con_que_stock_queda()
    {
        var producto = Guardado(1, stock: 5);

        var entrada = producto.RegistrarEntrada(10, "  Pedido 245  ", Ahora, 1);
        var uso = producto.RegistrarUsoInterno(2, null, Ahora, 1);
        var recuento = producto.AjustarInventario(11, "Recuento de octubre", Ahora, 1);

        Assert.Equal((10, 15, "Pedido 245"), (entrada.Cantidad, entrada.StockResultante, entrada.Nota));
        Assert.Equal((-2, 13), (uso.Cantidad, uso.StockResultante));
        Assert.Equal((TipoMovimientoStock.Ajuste, -2, 11), (recuento.Tipo, recuento.Cantidad, recuento.StockResultante));
        Assert.Equal(11, producto.Stock);
    }

    [Fact]
    public void Gastar_o_vender_mas_de_lo_que_hay_deja_el_stock_en_negativo()
    {
        var producto = Guardado(1, stock: 2);
        var agotado = Guardado(2, stock: 0, nombre: "Mascarilla");

        var uso = producto.RegistrarUsoInterno(3, null, Ahora, null);
        var venta = agotado.Vender(1, 7, Ahora, null);
        var enServicio = agotado.UsarEnVisita(2, 7, Ahora, 4);

        Assert.Equal((-3, -1), (uso.Cantidad, uso.StockResultante));
        Assert.Equal((-1, -1, (int?)7), (venta.Cantidad, venta.StockResultante, venta.VisitaId));
        Assert.Equal((TipoMovimientoStock.UsoInterno, -2, -3, (int?)7, "Usado en el servicio"), (enServicio.Tipo, enServicio.Cantidad, enServicio.StockResultante, enServicio.VisitaId, enServicio.Nota));
        Assert.True(producto.EnNegativo && producto.Agotado && !producto.BajoStock);
        Assert.Equal(-3, agotado.Stock);
    }

    [Fact]
    public void Un_recuento_devuelve_el_stock_negativo_a_lo_contado_y_una_entrada_lo_recupera()
    {
        var producto = Guardado(1, stock: 0);
        producto.Vender(2, 1, Ahora, null);

        var entrada = producto.RegistrarEntrada(5, "Pedido", Ahora, null);
        var recuento = producto.AjustarInventario(4, null, Ahora, null);

        Assert.Equal((5, 3), (entrada.Cantidad, entrada.StockResultante));
        Assert.Equal((1, 4), (recuento.Cantidad, recuento.StockResultante));
        Assert.False(producto.EnNegativo);
    }

    [Fact]
    public void Un_recuento_igual_al_stock_o_fuera_de_rango_no_se_registra()
    {
        var producto = Guardado(1, stock: 4);

        Assert.Throws<ReglaDeNegocioException>(() => producto.AjustarInventario(4, null, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => producto.AjustarInventario(-1, null, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => producto.RegistrarEntrada(0, null, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => producto.RegistrarEntrada(1, new string('x', MovimientoStock.LongitudNota + 1), Ahora, null));
        Assert.Equal(4, producto.Stock);
    }

    [Theory]
    [InlineData(0, 2, true, false)]
    [InlineData(2, 2, false, true)]
    [InlineData(3, 2, false, false)]
    [InlineData(0, 0, true, false)]
    public void Agotado_y_bajo_stock_segun_el_minimo(int stock, int minimo, bool agotado, bool bajo)
    {
        var producto = Producto.Crear(Ficha(minimo: minimo), Ahora);
        producto.Alta(stock, Ahora, null);

        Assert.Equal(agotado, producto.Agotado);
        Assert.Equal(bajo, producto.BajoStock);
    }

    [Fact]
    public void Las_fotos_tienen_un_maximo_y_la_principal_es_la_primera()
    {
        var producto = Guardado(1);
        var ids = Enumerable.Range(0, Producto.MaximoFotos).Select(_ => Guid.NewGuid()).ToList();
        ids.ForEach(id => producto.AgregarFoto(id, Ahora));

        Assert.Throws<ReglaDeNegocioException>(() => producto.AgregarFoto(Guid.NewGuid(), Ahora));
        Assert.Equal(ids[0], producto.FotoPrincipal!.Id);

        producto.HacerPrincipal(ids[3], Ahora);
        producto.QuitarFoto(ids[0], Ahora);

        Assert.Equal([ids[3], ids[1], ids[2], ids[4], ids[5]], producto.FotosEnOrden.Select(f => f.Id));
        Assert.Equal([0, 1, 2, 3, 4], producto.FotosEnOrden.Select(f => f.Orden));
        Assert.Throws<ReglaDeNegocioException>(() => producto.QuitarFoto(ids[0], Ahora));
    }

    [Fact]
    public void Editar_no_toca_el_stock_y_archivar_lo_conserva()
    {
        var producto = Guardado(1, stock: 7);

        producto.Editar(Ficha("REF-1", "Champú de argán 500 ml", 24m), Ahora.AddDays(1));
        producto.Archivar(Ahora.AddDays(2));

        Assert.Equal(("Champú de argán 500 ml", 24m, 7, false), (producto.Nombre, producto.PrecioVenta, producto.Stock, producto.Activo));
    }
}
