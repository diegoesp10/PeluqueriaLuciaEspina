using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Abstracciones;
using PeluqueriaLucia.Application.Catalogo;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Dominio;
using SkiaSharp;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>Inventario y ventas de productos contra SQL Server real: unicidad, agregados, concurrencia y ficheros.</summary>
public sealed class ProductosTests : IAsyncLifetime
{
    private readonly string _carpeta = Path.GetFullPath(Path.Combine(".artifacts", "productos-prueba-" + Guid.NewGuid().ToString("N")));
    private BaseDeDatosPrueba _bd = null!;

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync()
    {
        await _bd.DisposeAsync();
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }

    [Fact]
    public async Task Crear_con_stock_inicial_y_fotos_guarda_la_ficha_el_alta_y_los_dos_tamanos()
    {
        var resultado = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha(codigo: "8412345678901"), 12),
            [Foto(2400, 1200, "frente.png"), Foto(800, 800, "lateral.jpg")], administradorId: null);

        Assert.True(resultado.Exito, resultado.Error?.Mensaje);
        var (productos, _, db) = Servicios();
        var producto = (await productos.ObtenerAsync(resultado.Valor))!;
        Assert.Equal(("CH-ARG-250", "8412345678901", 12), (producto.Referencia, producto.CodigoBarras, producto.Stock));
        Assert.Equal(2, producto.Fotos.Count);
        var alta = Assert.Single((await productos.MovimientosAsync(producto.Id, 1)).Elementos);
        Assert.Equal((TipoMovimientoStock.Alta, 12, 12), (alta.Tipo, alta.Cantidad, alta.StockResultante));

        var principal = producto.FotoPrincipal!.Id;
        using (var grande = SKBitmap.Decode(productos.RutaFoto(principal, miniatura: false)))
            Assert.Equal((1600, 800), (grande.Width, grande.Height));
        using (var mini = SKBitmap.Decode(productos.RutaFoto(principal, miniatura: true)))
            Assert.Equal((480, 240), (mini.Width, mini.Height));
        Assert.Equal(4, Directory.GetFiles(_carpeta).Length);
        await db.DisposeAsync();
    }

    [Fact]
    public async Task Referencia_y_codigo_de_barras_no_se_repiten_aunque_cambien_mayusculas()
    {
        await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-1", codigo: "12345678")), [], null);

        var referencia = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("ch-1")), [], null);
        var codigo = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-2", codigo: "12345678")), [], null);
        var sinCodigo = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-3")), [], null);
        var otroSinCodigo = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-4")), [], null);

        Assert.Equal("Referencia", referencia.Error?.Campo);
        Assert.Equal("CodigoBarras", codigo.Error?.Campo);
        Assert.True(sinCodigo.Exito && otroSinCodigo.Exito, "Varios productos sin código de barras tienen que poder convivir");
    }

    [Fact]
    public async Task Una_foto_falsa_no_deja_ni_ficha_ni_ficheros()
    {
        using var falsa = new MemoryStream("<svg onload='alert(1)'/>"u8.ToArray());

        var resultado = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha(), 3),
            [Foto(600, 600, "buena.png"), new ArchivoImagen(falsa, "mala.jpg", falsa.Length)], null);

        Assert.False(resultado.Exito);
        Assert.Equal("Fotos", resultado.Error!.Campo);
        Assert.Contains("mala.jpg", resultado.Error.Mensaje);
        Assert.Equal(0, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM Productos"));
        Assert.Empty(Directory.Exists(_carpeta) ? Directory.GetFiles(_carpeta) : []);
    }

    [Fact]
    public async Task Editar_quita_fotos_cambia_la_principal_y_borra_los_ficheros_quitados()
    {
        var id = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha()), [Foto(400, 400, "a.png"), Foto(400, 400, "b.png")], null)).Valor;
        var (productos, _, _) = Servicios();
        var fotos = (await productos.ObtenerAsync(id))!.FotosEnOrden.Select(f => f.Id).ToList();

        var resultado = await Servicios().Productos.EditarAsync(id, ProductoTests.Ficha(nombre: "Champú de argán 500 ml"), new CategoriaElegida(null, "Champús"),
            new CambiosFotos([Foto(400, 400, "c.png")], [fotos[0]], fotos[1]));

        Assert.True(resultado.Exito, resultado.Error?.Mensaje);
        var editado = (await Servicios().Productos.ObtenerAsync(id))!;
        Assert.Equal("Champú de argán 500 ml", editado.Nombre);
        Assert.Equal(fotos[1], editado.FotoPrincipal!.Id);
        Assert.Equal(2, editado.Fotos.Count);
        Assert.Null(productos.RutaFoto(fotos[0], miniatura: false));
        Assert.Null(productos.RutaFoto(fotos[0], miniatura: true));
        Assert.Equal(4, Directory.GetFiles(_carpeta).Length);
    }

    [Fact]
    public async Task Buscar_filtra_por_palabras_estado_y_categoria_y_el_resumen_valora_el_inventario()
    {
        var productos = Servicios().Productos;
        await productos.CrearAsync(Datos(ProductoTests.Ficha("CH-1", "Champú de argán", 10m, "84100001", minimo: 2, coste: 5m), 6), [], null);
        await productos.CrearAsync(Datos(ProductoTests.Ficha("MA-1", "Mascarilla reparadora", 20m, minimo: 3, coste: 8m), 2, "Tratamientos"), [], null);
        await productos.CrearAsync(Datos(ProductoTests.Ficha("LA-1", "Laca fuerte", 8m, minimo: 1, coste: null), 0, "Fijadores"), [], null);
        var archivado = (await productos.CrearAsync(Datos(ProductoTests.Ficha("VI-1", "Vieja colonia", 5m), 4), [], null)).Valor;
        await Servicios().Productos.ArchivarAsync(archivado, archivar: true);

        async Task<string[]> Nombres(FiltroProductos filtro) => [.. (await Servicios().Productos.BuscarAsync(filtro, 1)).Elementos.Select(p => p.Referencia)];

        Assert.Equal(["CH-1", "LA-1", "MA-1"], await Nombres(new()));
        Assert.Equal(["CH-1"], await Nombres(new("argán")));
        Assert.Equal(["CH-1"], await Nombres(new("84100001")));
        Assert.Equal(["MA-1"], await Nombres(new("ma-1")));
        Assert.Equal(["LA-1", "MA-1"], await Nombres(new(Estado: EstadoInventario.PorReponer)));
        Assert.Equal(["VI-1"], await Nombres(new(Estado: EstadoInventario.Archivados)));
        Assert.Equal(4, (await Nombres(new(Estado: EstadoInventario.Todos))).Length);
        // Por categoría (solo las suyas, en el estado pedido) y por su nombre en el buscador de texto
        var champus = await _bd.EscalarAsync<int>("SELECT Id FROM CategoriasProductos WHERE Nombre = N'Champús'");
        Assert.Equal(["CH-1"], await Nombres(new(CategoriaId: champus)));
        Assert.Equal(["CH-1", "VI-1"], await Nombres(new(Estado: EstadoInventario.Todos, CategoriaId: champus)));
        Assert.Equal(["LA-1"], await Nombres(new("fijador")));

        var resumen = await Servicios().Productos.ResumirAsync();
        Assert.Equal(new ResumenInventario(Productos: 3, Unidades: 8, BajoStock: 1, Agotados: 1, ValorVenta: 100m, ValorCoste: 46m), resumen);
        Assert.Equal(["LA-1", "MA-1"], (await Servicios().Productos.PorReponerAsync(5)).Select(p => p.Referencia));
    }

    [Fact]
    public async Task Los_movimientos_manuales_cambian_el_stock_y_quedan_en_el_historial()
    {
        var id = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha(), 5), [], null)).Valor;

        var entrada = await Servicios().Productos.RegistrarMovimientoAsync(id, MovimientoManual.Entrada, 10, "Pedido 245", 1);
        var uso = await Servicios().Productos.RegistrarMovimientoAsync(id, MovimientoManual.UsoInterno, 3, null, 1);
        var demasiado = await Servicios().Productos.RegistrarMovimientoAsync(id, MovimientoManual.UsoInterno, 20, null, 1);
        var negativo = (await Servicios().Productos.ObtenerAsync(id))!;
        var recuento = await Servicios().Productos.RegistrarMovimientoAsync(id, MovimientoManual.Recuento, 11, "Recuento", 1);

        // Gastar más de lo que hay no se impide: el stock queda en negativo hasta la entrada o el recuento
        Assert.Equal((15, 12, -8, 11), (entrada.Valor, uso.Valor, demasiado.Valor, recuento.Valor));
        Assert.True(negativo.EnNegativo);
        var historial = (await Servicios().Productos.MovimientosAsync(id, 1)).Elementos;
        Assert.Equal([TipoMovimientoStock.Ajuste, TipoMovimientoStock.UsoInterno, TipoMovimientoStock.UsoInterno, TipoMovimientoStock.Entrada, TipoMovimientoStock.Alta], historial.Select(m => m.Tipo));
        Assert.Equal([11, -8, 12, 15, 5], historial.Select(m => m.StockResultante));
        Assert.Equal(11, (await Servicios().Productos.ObtenerAsync(id))!.Stock);
    }

    [Fact]
    public async Task Cobrar_una_visita_con_productos_descuenta_el_stock_y_el_resumen_los_cuenta()
    {
        var (clienteId, servicioId) = await ClienteYServicioAsync();
        var champu = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-1", precio: 10m), 5), [], null)).Valor;
        var laca = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("LA-1", "Laca", 8m), 2), [], null)).Valor;
        var dia = new DateTime(2026, 10, 17, 10, 0, 0);

        var registrada = await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, dia, FormaPago.Efectivo, [servicioId],
            Productos: [new ProductoNuevaVisita(champu, 2), new ProductoNuevaVisita(laca, 1, 7.50m)]));
        Assert.True(registrada.Exito, registrada.Error?.Mensaje);
        var pendiente = await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, dia.AddHours(1), FormaPago.Efectivo, [],
            Productos: [new ProductoNuevaVisita(champu, 1)]));

        Assert.Equal(5, (await Servicios().Productos.ObtenerAsync(champu))!.Stock);
        var cobro = await Servicios().Visitas.CobrarAsync(registrada.Valor, FormaPago.Mixto, 20m, 25.50m, administradorId: 1);

        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        Assert.Equal(3, (await Servicios().Productos.ObtenerAsync(champu))!.Stock);
        Assert.Equal(1, (await Servicios().Productos.ObtenerAsync(laca))!.Stock);
        var venta = (await Servicios().Productos.MovimientosAsync(champu, 1)).Elementos[0];
        Assert.Equal((TipoMovimientoStock.Venta, -2, 3, registrada.Valor, 1), (venta.Tipo, venta.Cantidad, venta.StockResultante, venta.VisitaId, venta.AdministradorId));

        var visita = (await Servicios().Visitas.ObtenerAsync(registrada.Valor))!;
        Assert.Equal((45.50m, 2), (visita.Total, visita.Productos.Count));
        var resumen = await Servicios().Visitas.ResumirAsync(FiltroVisitas.Dia(DateOnly.FromDateTime(dia)));
        Assert.Equal(new ResumenVisitas(Visitas: 2, Servicios: 1, Facturado: 55.50m, Cobrado: 45.50m, Efectivo: 20m, Tarjeta: 25.50m, Productos: 4), resumen);
        Assert.True(pendiente.Exito);
    }

    [Fact]
    public async Task Sin_stock_al_cobrar_se_cobra_igual_y_el_producto_queda_en_negativo_y_por_reponer()
    {
        var (clienteId, _) = await ClienteYServicioAsync();
        var champu = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha(precio: 10m, minimo: 0), 2), [], null)).Valor;
        var primera = (await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, DateTime.Today, FormaPago.Efectivo, [], Productos: [new ProductoNuevaVisita(champu, 2)]))).Valor;
        var segunda = (await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, DateTime.Today, FormaPago.Efectivo, [], Productos: [new ProductoNuevaVisita(champu, 1)]))).Valor;
        Assert.True((await Servicios().Visitas.CobrarAsync(primera, FormaPago.Efectivo, null, null)).Exito);

        var cobro = await Servicios().Visitas.CobrarAsync(segunda, FormaPago.Efectivo, null, null);

        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        Assert.Equal(-1, (await Servicios().Productos.ObtenerAsync(champu))!.Stock);
        Assert.Equal(-1, await _bd.EscalarAsync<int>($"SELECT Stock FROM Productos WHERE Id = {champu}"));
        Assert.Equal([champu], (await Servicios().Productos.PorReponerAsync(5)).Select(p => p.Id));
        // El negativo cuenta como agotado y no resta unidades ni valor al inventario
        Assert.Equal(new ResumenInventario(Productos: 1, Unidades: 0, BajoStock: 0, Agotados: 1, ValorVenta: 0m, ValorCoste: 0m), await Servicios().Productos.ResumirAsync());
    }

    [Fact]
    public async Task Los_productos_usados_al_cobrar_se_descuentan_con_su_visita_y_no_se_cobran()
    {
        var (clienteId, servicioId) = await ClienteYServicioAsync();
        var tinte = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("TI-73", "Tinte 7.3", 12m), 1), [], null)).Valor;
        var oxidante = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("OX-20", "Oxidante 20 vol", 8m), 10), [], null)).Valor;
        var visita = (await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, DateTime.Today, FormaPago.Efectivo, [servicioId]))).Valor;

        var inexistente = await Servicios().Visitas.CobrarAsync(visita, FormaPago.Efectivo, null, null, usados: [new ProductoUsadoCobro(999999, 1)]);
        var cobro = await Servicios().Visitas.CobrarAsync(visita, FormaPago.Efectivo, null, null, administradorId: 2,
            usados: [new ProductoUsadoCobro(tinte, 2), new ProductoUsadoCobro(oxidante, 1)]);

        Assert.Equal("Usados", inexistente.Error?.Campo);
        Assert.True(cobro.Exito, cobro.Error?.Mensaje);
        var cobrada = (await Servicios().Visitas.ObtenerAsync(visita))!;
        Assert.Equal((18m, 18m), (cobrada.Total, cobrada.TotalEfectivo!.Value));
        Assert.Equal((-1, 9), ((await Servicios().Productos.ObtenerAsync(tinte))!.Stock, (await Servicios().Productos.ObtenerAsync(oxidante))!.Stock));
        var usados = await Servicios().Visitas.UsadosAsync(visita);
        Assert.Equal([("Tinte 7.3", -2, -1), ("Oxidante 20 vol", -1, 9)], usados.Select(u => (u.Producto!.Nombre, u.Cantidad, u.StockResultante)));
        Assert.All(usados, u => Assert.Equal((TipoMovimientoStock.UsoInterno, (int?)visita, (int?)2), (u.Tipo, u.VisitaId, u.AdministradorId)));
    }

    [Fact]
    public async Task Dos_cambios_de_stock_a_la_vez_no_se_pisan()
    {
        var id = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha(), 5), [], null)).Valor;
        await using var primero = _bd.Contexto();
        await using var segundo = _bd.Contexto();
        var a = await primero.Productos.SingleAsync(p => p.Id == id);
        var b = await segundo.Productos.SingleAsync(p => p.Id == id);

        primero.MovimientosStock.Add(a.RegistrarUsoInterno(1, null, DateTime.Now, null));
        await primero.SaveChangesAsync();
        segundo.MovimientosStock.Add(b.RegistrarUsoInterno(1, null, DateTime.Now, null));

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
        Assert.Equal(4, await _bd.EscalarAsync<int>($"SELECT Stock FROM Productos WHERE Id = {id}"));
    }

    [Fact]
    public async Task Eliminar_borra_lo_que_nunca_se_vendio_y_archiva_lo_vendido()
    {
        var (clienteId, _) = await ClienteYServicioAsync();
        var nuevo = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("NU-1"), 3), [Foto(300, 300, "nuevo.png")], null)).Valor;
        var vendido = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("VE-1"), 3), [], null)).Valor;
        await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, DateTime.Today, FormaPago.Efectivo, [], Productos: [new ProductoNuevaVisita(vendido, 1)]));

        var borrado = await Servicios().Productos.EliminarAsync(nuevo);
        var archivado = await Servicios().Productos.EliminarAsync(vendido);

        Assert.Equal((Eliminacion.Eliminado, Eliminacion.Archivado), (borrado.Valor, archivado.Valor));
        Assert.Null(await Servicios().Productos.ObtenerAsync(nuevo));
        Assert.False((await Servicios().Productos.ObtenerAsync(vendido))!.Activo);
        Assert.Empty(Directory.GetFiles(_carpeta));
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM MovimientosStock"));
    }

    [Fact]
    public async Task La_exportacion_detalla_los_productos_vendidos_en_otra_hoja()
    {
        var (clienteId, servicioId) = await ClienteYServicioAsync();
        var champu = (await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("CH-1", precio: 10m), 5), [], null)).Valor;
        await Servicios().Visitas.RegistrarAsync(new NuevaVisita(clienteId, new DateTime(2026, 10, 17, 9, 0, 0), FormaPago.Efectivo, [servicioId],
            Productos: [new ProductoNuevaVisita(champu, 3)]));

        var archivo = await Servicios().Visitas.ExportarAsync(FiltroVisitas.Mes(2026, 10));

        using var libro = new XLWorkbook(new MemoryStream(archivo.Valor!.Contenido));
        var visita = libro.Worksheet("Visitas").Table("Visitas").DataRange.Rows().Single();
        Assert.Equal("3 × Champú de argán", visita.Cell(5).GetString());
        Assert.Equal(48m, visita.Cell(6).GetValue<decimal>());
        var linea = libro.Worksheet("Productos").Table("Productos").DataRange.Rows().Single();
        Assert.Equal(("CH-1", 3, 30m), (linea.Cell(4).GetString(), linea.Cell(6).GetValue<int>(), linea.Cell(8).GetValue<decimal>()));
    }

    [Fact]
    public async Task Eliminar_una_categoria_archiva_sus_productos_con_su_stock_y_recuperarla_los_devuelve()
    {
        var productos = Servicios().Productos;
        var argan = (await productos.CrearAsync(Datos(ProductoTests.Ficha("CH-1", "Champú de argán", 10m), 5), [], null)).Valor;
        var ortiga = (await productos.CrearAsync(Datos(ProductoTests.Ficha("CH-2", "Champú de ortiga", 9m), 3), [], null)).Valor;
        var antiguo = (await productos.CrearAsync(Datos(ProductoTests.Ficha("CH-3", "Champú antiguo", 7m), 1), [], null)).Valor;
        await productos.CrearAsync(Datos(ProductoTests.Ficha("MA-1", "Mascarilla", 20m), 2, "Mascarillas"), [], null);
        await Servicios().Productos.ArchivarAsync(antiguo, archivar: true);
        var champus = await _bd.EscalarAsync<int>("SELECT Id FROM CategoriasProductos WHERE Nombre = N'Champús'");

        // El aviso antes de eliminar sale de aquí: dos productos a la venta con 8 unidades
        var antes = (await Servicios().Productos.CategoriasAsync()).Single(c => c.Categoria.Id == champus);
        Assert.Equal((2, 8, 0, 0), (antes.Productos, antes.Unidades, antes.Archivados, antes.UnidadesArchivadas));

        var eliminada = await Servicios().Productos.EliminarCategoriaAsync(champus);

        Assert.Equal(new CambioCategoria("Champús", 2), eliminada.Valor);
        // Borrado lógico: la categoría y los productos siguen ahí, archivados con su stock, y el inventario ya no los cuenta
        Assert.Equal(1, await _bd.EscalarAsync<int>($"SELECT COUNT(*) FROM CategoriasProductos WHERE Id = {champus} AND Activa = 0 AND EliminadaEn IS NOT NULL"));
        Assert.Equal(2, await _bd.EscalarAsync<int>($"SELECT COUNT(*) FROM Productos WHERE CategoriaId = {champus} AND Activo = 0 AND ArchivadoConCategoria = 1"));
        Assert.Equal((5, 3), ((await Servicios().Productos.ObtenerAsync(argan))!.Stock, (await Servicios().Productos.ObtenerAsync(ortiga))!.Stock));
        var inventario = await Servicios().Productos.ResumirAsync();
        Assert.Equal((1, 2, 40m), (inventario.Productos, inventario.Unidades, inventario.ValorVenta));
        Assert.DoesNotContain(await Servicios().Productos.CategoriasActivasAsync(), c => c.Id == champus);
        Assert.DoesNotContain(await Servicios().Productos.ParaVentaAsync(), p => p.CategoriaId == champus);
        var despues = (await Servicios().Productos.CategoriasAsync()).Single(c => c.Categoria.Id == champus);
        Assert.Equal((0, 0, 2, 8), (despues.Productos, despues.Unidades, despues.Archivados, despues.UnidadesArchivadas));

        // Ni se vuelve a vender un producto suelto ni se da de alta uno nuevo en ella; otra categoría puede llevar su nombre
        Assert.Equal("CategoriaId", (await Servicios().Productos.ArchivarAsync(argan, archivar: false)).Error?.Campo);
        Assert.Equal("CategoriaId", (await Servicios().Productos.CrearAsync(new DatosProducto(ProductoTests.Ficha("CH-9"), new CategoriaElegida(champus)), [], null)).Error?.Campo);
        Assert.False((await Servicios().Productos.EliminarCategoriaAsync(champus)).Exito, "Ya estaba eliminada");
        var otra = (await Servicios().Productos.CrearCategoriaAsync("champús")).Valor;
        Assert.Equal("Nombre", (await Servicios().Productos.RecuperarCategoriaAsync(champus)).Error?.Campo);
        Assert.True((await Servicios().Productos.RenombrarCategoriaAsync(otra, "Champús sólidos")).Exito);

        var recuperada = await Servicios().Productos.RecuperarCategoriaAsync(champus);

        // Vuelven los que se archivaron con ella, con las mismas unidades; el que se archivó a mano sigue archivado
        Assert.Equal(new CambioCategoria("Champús", 2), recuperada.Valor);
        Assert.True((await Servicios().Productos.ObtenerAsync(argan))!.Activo);
        Assert.Equal((true, 3), ((await Servicios().Productos.ObtenerAsync(ortiga))!.Activo, (await Servicios().Productos.ObtenerAsync(ortiga))!.Stock));
        Assert.False((await Servicios().Productos.ObtenerAsync(antiguo))!.Activo);
        Assert.Equal(10, (await Servicios().Productos.ResumirAsync()).Unidades);
    }

    [Fact]
    public async Task La_categoria_es_obligatoria_y_escribir_una_nueva_la_crea_o_usa_la_que_se_llama_igual()
    {
        var sinCategoria = await Servicios().Productos.CrearAsync(new DatosProducto(ProductoTests.Ficha("SC-1"), new CategoriaElegida(null)), [], null);
        var inexistente = await Servicios().Productos.CrearAsync(new DatosProducto(ProductoTests.Ficha("SC-2"), new CategoriaElegida(999999)), [], null);
        var larga = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("SC-3"), categoria: new string('x', CategoriaProducto.LongitudNombre + 1)), [], null);

        Assert.Equal(("CategoriaId", "CategoriaId", "CategoriaId"), (sinCategoria.Error?.Campo, inexistente.Error?.Campo, larga.Error?.Campo));
        Assert.Equal(0, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM Productos"));
        Assert.Equal(0, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM CategoriasProductos"));

        var primero = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("AC-1"), categoria: "Aceites"), [], null);
        var segundo = await Servicios().Productos.CrearAsync(Datos(ProductoTests.Ficha("AC-2"), categoria: "  ACEITES "), [], null);
        var aceites = await _bd.EscalarAsync<int>("SELECT Id FROM CategoriasProductos WHERE Nombre = N'Aceites'");
        var tercero = await Servicios().Productos.CrearAsync(new DatosProducto(ProductoTests.Ficha("AC-3"), new CategoriaElegida(aceites)), [], null);

        Assert.True(primero.Exito && segundo.Exito && tercero.Exito);
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM CategoriasProductos"));
        Assert.Equal(3, await _bd.EscalarAsync<int>($"SELECT COUNT(*) FROM Productos WHERE CategoriaId = {aceites}"));

        // Al editar se cambia de categoría eligiendo otra o escribiendo una nueva
        var editado = await Servicios().Productos.EditarAsync(tercero.Valor, ProductoTests.Ficha("AC-3"), new CategoriaElegida(aceites, "Aceites capilares"), CambiosFotos.Ninguno);
        Assert.True(editado.Exito, editado.Error?.Mensaje);
        Assert.Equal("Aceites capilares", (await Servicios().Productos.ObtenerAsync(tercero.Valor))!.Categoria!.Nombre);
    }

    [Fact]
    public async Task No_hay_dos_categorias_activas_con_el_mismo_nombre()
    {
        var aceites = await Servicios().Productos.CrearCategoriaAsync("Aceites");
        var repetida = await Servicios().Productos.CrearCategoriaAsync(" ACEITES ");
        var vacia = await Servicios().Productos.CrearCategoriaAsync("  ");
        var lacas = (await Servicios().Productos.CrearCategoriaAsync("Lacas")).Valor;
        var renombrada = await Servicios().Productos.RenombrarCategoriaAsync(lacas, "aceites");

        Assert.True(aceites.Exito);
        Assert.Equal(("Nombre", "Nombre", "Nombre"), (repetida.Error?.Campo, vacia.Error?.Campo, renombrada.Error?.Campo));
        Assert.Equal(["Aceites", "Lacas"], (await Servicios().Productos.CategoriasActivasAsync()).Select(c => c.Nombre));
    }

    private static DatosProducto Datos(FichaProducto ficha, int stock = 0, string categoria = "Champús") => new(ficha, new CategoriaElegida(null, categoria), stock);

    private async Task<(int Cliente, int Servicio)> ClienteYServicioAsync()
    {
        await using var db = _bd.Contexto();
        var cliente = Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, DateTime.Now);
        var servicio = Servicio.Crear("Corte", 18m, Sexo.Mujer, DateTime.Now);
        db.AddRange(cliente, servicio);
        await db.SaveChangesAsync();
        return (cliente.Id, servicio.Id);
    }

    private static ArchivoImagen Foto(int ancho, int alto, string nombre)
    {
        using var bitmap = new SKBitmap(ancho, alto);
        using (var lienzo = new SKCanvas(bitmap))
            lienzo.Clear(SKColors.MediumPurple);
        using var imagen = SKImage.FromBitmap(bitmap);
        using var datos = imagen.Encode(nombre.EndsWith(".png") ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg, 90);
        var bytes = datos.ToArray();
        return new ArchivoImagen(new MemoryStream(bytes), nombre, bytes.Length);
    }

    /// <summary>Servicios de aplicación con un contexto nuevo, como en una petición web.</summary>
    private (ProductosService Productos, VisitasService Visitas, PeluqueriaDbContext Db) Servicios()
    {
        var db = _bd.Contexto();
        var reloj = TimeProvider.System;
        var productos = new ProductoRepositorio(db);
        var fotos = new FotosProductos(Options.Create(new OpcionesFotosProductos { Carpeta = _carpeta }), NullLogger<FotosProductos>.Instance);
        return (
            new ProductosService(productos, fotos, db, reloj),
            new VisitasService(new VisitaRepositorio(db), new ClienteRepositorio(db), new ServicioRepositorio(db), productos, new ExportadorExcelVisitas(reloj), db, reloj),
            db);
    }
}
