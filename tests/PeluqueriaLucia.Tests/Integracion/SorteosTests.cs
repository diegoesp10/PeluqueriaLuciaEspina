using Comunicaciones.Correo;
using Comunicaciones.WhatsApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Productos;
using PeluqueriaLucia.Application.Sorteos;
using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Sorteos;
using PeluqueriaLucia.Domain.Visitas;
using PeluqueriaLucia.Infrastructure.Almacenamiento;
using PeluqueriaLucia.Infrastructure.Exportacion;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Dominio;

namespace PeluqueriaLucia.Tests.Integracion;

/// <summary>Sorteos, productos de sorteo y costes de compra contra SQL Server real, con un contexto por petición como en la web.</summary>
public sealed class SorteosTests : IAsyncLifetime
{
    private readonly string _carpeta = Path.GetFullPath(Path.Combine(".artifacts", "sorteos-prueba-" + Guid.NewGuid().ToString("N")));
    private readonly CorreoFalso _correo = new();
    private BaseDeDatosPrueba _bd = null!;

    /// <summary>Lo que dirá el azar en el próximo sorteo (y cuántos participantes le llegaron).</summary>
    private int _azar;
    private int _pedido;

    public async Task InitializeAsync() => _bd = await BaseDeDatosPrueba.CrearAsync();

    public async Task DisposeAsync()
    {
        await _bd.DisposeAsync();
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }

    [Fact]
    public async Task Un_sorteo_completo_de_principio_a_fin()
    {
        var corte = await ServicioAsync("Corte y peinado", "Cut and styling");
        var ana = await ClienteAsync("Ana", "ana@example.test", "611 222 333");
        var bea = await ClienteAsync("Bea", null, null);
        var carla = await ClienteAsync("Carla", "carla@example.test", null);

        var creado = await Sorteos().CrearAsync(Datos());
        var id = creado.Valor;
        Assert.True(creado.Exito, creado.Error?.Mensaje);
        Assert.True((await Sorteos().AgregarPremioAsync(id, new PremioElegido(TipoPremio.Servicio, corte), 1)).Exito);
        var nuevos = await Sorteos().AgregarParticipantesAsync(id, [ana, bea, ana]);
        var repetidos = await Sorteos().AgregarParticipantesAsync(id, [bea, carla]);
        var vacio = await Sorteos().AgregarParticipantesAsync(id, [999999]);

        Assert.Equal((2, 1), (nuevos.Valor, repetidos.Valor));
        Assert.Equal("ClienteId", vacio.Error?.Campo);

        // El azar dice «el segundo»: en el orden en que se apuntaron, Bea
        _azar = 1;
        var sorteado = await Sorteos().SortearAsync(id, administradorId: null);
        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.True(sorteado.Exito, sorteado.Error?.Mensaje);
        Assert.Equal(3, _pedido);
        Assert.Equal((EstadoSorteo.Realizado, bea, "Bea Pérez", FormaGanador.AlAzar, 3), (sorteo.Estado, sorteo.Ganador!.ClienteId!.Value, sorteo.Ganador.Ganador, sorteo.Ganador.Forma, sorteo.Ganador.Participantes));
        Assert.Equal("Cut and styling", sorteo.Premios.Single().NombreEn);

        // Bea no tiene ni correo ni móvil: se repite entre Ana y Carla
        var aviso = (await Sorteos().AvisoAsync(sorteo))!;
        Assert.Equal((null, null), (aviso.Correo, aviso.WhatsApp));
        Assert.Contains("¡Enhorabuena, Bea!", aviso.Texto);
        Assert.Contains("Has ganado Corte y peinado", aviso.Texto);
        _azar = 0;
        Assert.True((await Sorteos().RepetirAsync(id, "No tenemos cómo avisarle", null)).Exito);
        sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal((ana, 2, "No tenemos cómo avisarle"), (sorteo.Ganador!.ClienteId!.Value, sorteo.Ganador.Participantes, sorteo.Ganador.Motivo));
        Assert.Equal(2, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ExtraccionesSorteos"));

        // Ana: aviso por correo (con la imagen del salón) y por WhatsApp, con su móvil en formato internacional
        aviso = (await Sorteos().AvisoAsync(sorteo))!;
        Assert.Equal(("ana@example.test", "+34611222333"), (aviso.Correo, aviso.WhatsApp));
        var corto = await Sorteos().AvisarPorCorreoAsync(id, "Hola");
        var correo = await Sorteos().AvisarPorCorreoAsync(id, aviso.Texto);
        Assert.Equal("TextoAviso", corto.Error?.Campo);
        Assert.True(correo.Exito, correo.Error?.Mensaje);
        var enviado = Assert.Single(_correo.Enviados);
        Assert.Equal("ana@example.test", enviado.Destinatario);
        Assert.Contains("Has ganado", enviado.Texto);
        Assert.Contains("Corte y peinado", enviado.Html);
        Assert.Equal(CanalConfirmacion.Correo, (await Sorteos().ObtenerAsync(id))!.Ganador!.AvisoCanal);

        var whatsApp = await Sorteos().AvisarPorWhatsAppAsync(id, "¡Enhorabuena, Ana! Has ganado.");
        Assert.True(whatsApp.Exito, whatsApp.Error?.Mensaje);
        Assert.StartsWith("https://wa.me/34611222333?text=", whatsApp.Valor!.AbsoluteUri);
        Assert.Equal(CanalConfirmacion.WhatsApp, (await Sorteos().ObtenerAsync(id))!.Ganador!.AvisoCanal);

        // Entregado el premio, el ganador ya no cambia
        Assert.True((await Sorteos().MarcarEntregadoAsync(id)).Exito);
        Assert.NotNull((await Sorteos().ObtenerAsync(id))!.Ganador!.EntregadoEn);
        Assert.False((await Sorteos().RepetirAsync(id, "Otra vez", null)).Exito);

        // Con ganador, no se borra: queda en el historial
        Assert.False((await Sorteos().EliminarAsync(id)).Exito);
        Assert.Equal([id], (await Sorteos().ListarAsync(historial: true)).Select(s => s.Id));
        Assert.Empty(await Sorteos().ListarAsync(historial: false));
    }

    [Fact]
    public async Task Un_lote_con_servicio_producto_de_la_tienda_y_producto_de_sorteo_queda_en_el_historial_de_cada_uno()
    {
        var tinte = await ServicioAsync("Tinte ecológico", "Organic dye");
        var champu = (await Productos().CrearAsync(new DatosProducto(ProductoTests.Ficha("CH-L"), new CategoriaElegida(null, "Champús"), 3), [], null)).Valor;
        var cena = await ProductosSorteo().CrearAsync(new FichaProductoSorteo("Cena para dos", "Dinner for two", "En Casa Ramón", "At Casa Ramón"));
        var repetida = await ProductosSorteo().CrearAsync(new FichaProductoSorteo("cena para dos", "Dinner", null, null));
        Assert.True(cena.Exito, cena.Error?.Mensaje);
        Assert.Equal("Nombre", repetida.Error?.Campo);

        var id = (await Sorteos().CrearAsync(Datos())).Valor;
        await Sorteos().AgregarPremioAsync(id, new PremioElegido(TipoPremio.Servicio, tinte), 1);
        await Sorteos().AgregarPremioAsync(id, new PremioElegido(TipoPremio.Producto, champu), 2);
        await Sorteos().AgregarPremioAsync(id, new PremioElegido(TipoPremio.ProductoSorteo, cena.Valor), 1);
        var inexistente = await Sorteos().AgregarPremioAsync(id, new PremioElegido(TipoPremio.ProductoSorteo, 999999), 1);
        Assert.Equal("Premio", inexistente.Error?.Campo);
        Assert.Equal("Tinte ecológico, 2 × Champú de argán y Cena para dos", (await Sorteos().ObtenerAsync(id))!.PremiosEnTexto());

        // Corregir el producto de sorteo pone al día el sorteo abierto
        var editado = await ProductosSorteo().EditarAsync(cena.Valor, new FichaProductoSorteo("Cena para dos personas", "Dinner for two", "En Casa Ramón", "At Casa Ramón"));
        Assert.Equal(1, editado.Valor);
        Assert.Equal("Cena para dos personas", (await Sorteos().ObtenerAsync(id))!.Premios.Single(p => p.Tipo == TipoPremio.ProductoSorteo).Nombre);
        Assert.Equal((1, 0, 0), Uso(await ProductosSorteo().ListarAsync(), cena.Valor));

        await Sorteos().AgregarParticipantesAsync(id, [await ClienteAsync("Ana", null, null), await ClienteAsync("Bea", null, null)]);
        _azar = 1;
        await Sorteos().SortearAsync(id, null);
        await Sorteos().MarcarEntregadoAsync(id);

        // Ya sorteado, otro cambio en el producto no toca lo que se sorteó
        await ProductosSorteo().EditarAsync(cena.Valor, new FichaProductoSorteo("Cena", "Dinner", null, null));
        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal("Cena para dos personas", sorteo.Premios.Single(p => p.Tipo == TipoPremio.ProductoSorteo).Nombre);
        Assert.Equal((0, 1, 1), Uso(await ProductosSorteo().ListarAsync(), cena.Valor));
        var historial = Assert.Single(await ProductosSorteo().HistorialAsync(cena.Valor));
        Assert.Equal((id, "Bea Pérez", 3), (historial.Id, historial.Ganador!.Ganador, historial.Premios.Count));
        Assert.NotNull(historial.Ganador.EntregadoEn);

        // Eliminado no se ofrece ni se puede añadir a otro sorteo, pero el historial sigue
        Assert.Equal(0, (await ProductosSorteo().EliminarAsync(cena.Valor)).Valor);
        Assert.Empty(await ProductosSorteo().ActivosAsync());
        var otro = (await Sorteos().CrearAsync(Datos("Sorteo de verano"))).Valor;
        Assert.Contains("eliminado", (await Sorteos().AgregarPremioAsync(otro, new PremioElegido(TipoPremio.ProductoSorteo, cena.Valor), 1)).Error?.Mensaje);
        Assert.Single(await ProductosSorteo().HistorialAsync(cena.Valor));
        Assert.True((await ProductosSorteo().RecuperarAsync(cena.Valor)).Exito);
        Assert.Single(await ProductosSorteo().ActivosAsync());
        Assert.Equal(3, await _bd.EscalarAsync<int>($"SELECT COUNT(*) FROM PremiosSorteos WHERE SorteoId = {id}"));
    }

    [Fact]
    public async Task Los_clientes_que_vinieron_entre_dos_fechas_entran_de_golpe()
    {
        var corte = await ServicioAsync("Corte", "Cut");
        var ana = await ClienteAsync("Ana", null, null);
        var bea = await ClienteAsync("Bea", null, null);
        await ClienteAsync("Carla", null, null);
        await using (var db = _bd.Contexto())
        {
            var servicio = await db.Servicios.SingleAsync(s => s.Id == corte);
            var octubre = new DateTime(2026, 10, 10, 10, 0, 0);
            db.Visitas.AddRange(
                Visita.Registrar(await db.Clientes.SingleAsync(c => c.Id == ana), octubre, FormaPago.Efectivo, [servicio]),
                Visita.Registrar(await db.Clientes.SingleAsync(c => c.Id == ana), octubre.AddDays(5), FormaPago.Efectivo, [servicio]),
                Visita.Registrar(await db.Clientes.SingleAsync(c => c.Id == bea), new DateTime(2026, 10, 31, 19, 0, 0), FormaPago.Efectivo, [servicio]),
                Visita.Registrar(await db.Clientes.SingleAsync(c => c.Id == bea), new DateTime(2026, 9, 1, 10, 0, 0), FormaPago.Efectivo, [servicio]));
            await db.SaveChangesAsync();
        }
        var id = await ConPremioAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte));

        var octubreEntero = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var otraVez = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var ninguna = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));
        var alReves = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

        Assert.Equal((2, 0), (octubreEntero.Valor, otraVez.Valor));
        Assert.Equal(("DesdeVisitas", "HastaVisitas"), (ninguna.Error?.Campo, alReves.Error?.Campo));
        Assert.Equal([ana, bea], (await Sorteos().ObtenerAsync(id))!.Participantes.Select(p => p.ClienteId).Order());
    }

    [Fact]
    public async Task La_web_solo_enseña_los_anuncios_de_hoy_de_sorteos_no_cancelados_con_todos_sus_premios()
    {
        var tinte = await ServicioAsync("Tinte", "Dye");
        var cena = (await ProductosSorteo().CrearAsync(new FichaProductoSorteo("Cena", "Dinner", "Para dos", "For two"))).Valor;
        var hoy = Sorteos().Hoy;
        var abierto = await ConPremioAsync(Datos("Sorteo de otoño"), new PremioElegido(TipoPremio.Servicio, tinte));
        await Sorteos().AgregarPremioAsync(abierto, new PremioElegido(TipoPremio.ProductoSorteo, cena), 2);
        var cancelado = await ConPremioAsync(Datos("Sorteo aplazado"), new PremioElegido(TipoPremio.Servicio, tinte));
        await Sorteos().AnunciarAsync(abierto, "Participa con tu visita", "Join with your visit", hoy, hoy.AddDays(5));
        await Sorteos().AnunciarAsync(abierto, "Muy pronto", "Coming soon", hoy.AddDays(6), hoy.AddDays(9));
        await Sorteos().AnunciarAsync(cancelado, "Aplazado", "Postponed", hoy, hoy.AddDays(5));
        var sinFechas = await Sorteos().AnunciarAsync(abierto, "Sin fechas", "No dates", null, hoy);
        Assert.Equal(2, (await Sorteos().AnunciosVisiblesAsync()).Count);

        await Sorteos().CancelarAsync(cancelado, "Se aplaza");
        var visibles = await Sorteos().AnunciosVisiblesAsync();

        Assert.Equal("DesdeAnuncio", sinFechas.Error?.Campo);
        var anuncio = Assert.Single(visibles);
        Assert.Equal(("Sorteo de otoño", "Join with your visit", false), (anuncio.Titulo, anuncio.TextoEnIdioma(true), anuncio.Realizado));
        Assert.Equal(("Tinte y 2 × Cena", "Dye and 2 × Dinner"), (anuncio.PremiosEnIdioma(false), anuncio.PremiosEnIdioma(true)));
        Assert.Equal([(TipoPremio.Servicio, (string?)null), (TipoPremio.ProductoSorteo, "For two")], anuncio.Premios.Select(p => (p.Tipo, p.DescripcionEnIdioma(true))));
        // Quitar el anuncio lo saca de la web; borrar un sorteo sin ganador se lleva sus anuncios y sus premios
        var anuncioId = anuncio.AnuncioId;
        Assert.True((await Sorteos().QuitarAnuncioAsync(abierto, anuncioId)).Exito);
        Assert.Empty(await Sorteos().AnunciosVisiblesAsync());
        Assert.True((await Sorteos().EliminarAsync(abierto)).Exito);
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM AnunciosSorteos"));
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM PremiosSorteos"));
    }

    [Fact]
    public async Task Borrar_la_ficha_del_ganador_o_el_premio_conserva_el_historial_con_sus_nombres()
    {
        var champu = (await Productos().CrearAsync(new DatosProducto(ProductoTests.Ficha("CH-S"), new CategoriaElegida(null, "Champús"), 2), [], null)).Valor;
        var ana = await ClienteAsync("Ana", null, null);
        var bea = await ClienteAsync("Bea", null, null);
        var id = await ConPremioAsync(Datos(), new PremioElegido(TipoPremio.Producto, champu));
        await Sorteos().AgregarParticipantesAsync(id, [ana, bea]);
        _azar = 0;
        await Sorteos().SortearAsync(id, null);

        await _bd.EjecutarAsync($"DELETE FROM Clientes WHERE Id = {ana}");
        await _bd.EjecutarAsync($"DELETE FROM Productos WHERE Id = {champu}");

        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal((null, "Ana Pérez"), (sorteo.Ganador!.ClienteId, sorteo.Ganador.Ganador));
        var premio = sorteo.Premios.Single();
        Assert.Equal((null, "Champú de argán"), (premio.ProductoId, premio.Nombre));
        Assert.Equal([bea], sorteo.Participantes.Select(p => p.ClienteId));
    }

    [Fact]
    public async Task Dos_sorteos_a_la_vez_no_dan_dos_ganadores()
    {
        var corte = await ServicioAsync("Corte", "Cut");
        var id = await ConPremioAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte));
        await Sorteos().AgregarParticipantesAsync(id, [await ClienteAsync("Ana", null, null), await ClienteAsync("Bea", null, null)]);
        await using var primero = _bd.Contexto();
        await using var segundo = _bd.Contexto();
        var a = await new SorteoRepositorio(primero).ObtenerAsync(id, default);
        var b = await new SorteoRepositorio(segundo).ObtenerAsync(id, default);

        a!.Sortear(_ => 0, DateTime.Now, null);
        b!.Sortear(_ => 1, DateTime.Now, null);
        await primero.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync());
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ExtraccionesSorteos"));
    }

    [Fact]
    public async Task La_migracion_pasa_el_premio_de_cada_sorteo_a_su_lista_de_premios_sin_borrar_nada()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261029175921_SorteosYCostesDeProductos");
        int tinte;
        await using (var db = bd.Contexto())
        {
            var servicio = Servicio.Crear("Tinte", 30m, Sexo.Mujer, DateTime.Now, "Dye");
            db.Add(servicio);
            await db.SaveChangesAsync();
            tinte = servicio.Id;
        }
        // Un sorteo con un servicio y otro cuyo producto ya se borró (queda el nombre)
        await bd.EjecutarAsync($"""
            INSERT INTO Sorteos (Titulo, TituloEn, TipoPremio, ServicioId, ProductoId, Premio, PremioEn, FechaSorteo, Estado, CreadoEn, ActualizadoEn)
            VALUES (N'Sorteo de otoño', N'Autumn giveaway', 0, {tinte}, NULL, N'Tinte', N'Dye', '2026-11-20T18:00:00', 0, '2026-10-30', '2026-10-30'),
                   (N'Sorteo de la tienda', N'Shop giveaway', 1, NULL, NULL, N'Champú ya borrado', NULL, '2026-11-25T18:00:00', 1, '2026-10-30', '2026-10-30');
            """);

        await using (var db = bd.Contexto())
            await db.Database.MigrateAsync();

        Assert.Equal("Tinte|Dye|0|1|1;Champú ya borrado||1|0|1", await bd.EscalarAsync<string>("""
            SELECT STRING_AGG(CONCAT(p.Nombre, '|', p.NombreEn, '|', p.Tipo, '|', IIF(p.ServicioId IS NULL, 0, 1), '|', p.Cantidad), ';') WITHIN GROUP (ORDER BY p.SorteoId)
            FROM PremiosSorteos p
            """));
        // Las columnas antiguas conservan sus datos
        Assert.Equal(2, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM Sorteos WHERE Premio IS NOT NULL AND TipoPremio IS NOT NULL"));
        await using (var db = bd.Contexto())
        {
            var sorteo = await new SorteoRepositorio(db).ObtenerAsync(await bd.EscalarAsync<int>("SELECT MIN(Id) FROM Sorteos"), default);
            Assert.Equal("Tinte", sorteo!.PremiosEnTexto());
        }
    }

    [Fact]
    public async Task El_historial_de_costes_se_anota_al_dar_de_alta_al_editar_al_recibir_mercancia_y_a_mano()
    {
        var alta = await Productos().CrearAsync(new DatosProducto(ProductoTests.Ficha("CO-1", coste: 100m), new CategoriaElegida(null, "Champús"), 0), [], 1);
        var id = alta.Valor;
        var producto = (await Productos().ObtenerAsync(id))!;
        var editado = await Productos().EditarAsync(id, ProductoTests.Ficha("CO-1", coste: 110m), new CategoriaElegida(producto.CategoriaId, null),
            new CambiosFotos([], [], null), default, 1);
        var sinCambio = await Productos().EditarAsync(id, ProductoTests.Ficha("CO-1", coste: 110m), new CategoriaElegida(producto.CategoriaId, null),
            new CambiosFotos([], [], null), default, 1);
        var entrada = await Productos().RegistrarMovimientoAsync(id, MovimientoManual.Entrada, 6, null, 1, default, coste: 120m);
        var pasado = await Productos().RegistrarCosteAsync(id, 95m, new DateOnly(2026, 1, 15), "Oferta de enero", 1);
        var sinImporte = await Productos().RegistrarCosteAsync(id, null, null, null, 1);

        Assert.True(alta.Exito && editado.Exito && sinCambio.Exito && entrada.Exito && pasado.Exito);
        Assert.Equal("Coste", sinImporte.Error?.Campo);
        var costes = await Productos().CostesAsync(id);
        Assert.Equal([120m, 110m, 100m, 95m], costes.Select(c => c.Coste));
        Assert.Equal(["Entrada de 6 uds", null, "Al dar de alta el producto", "Oferta de enero"], costes.Select(c => c.Nota));
        // El de enero es antiguo: el coste del producto sigue siendo el último
        Assert.Equal((120m, 6), ((await Productos().ObtenerAsync(id))!.PrecioCoste!.Value, (await Productos().ObtenerAsync(id))!.Stock));
    }

    [Fact]
    public async Task La_migracion_empieza_el_historial_con_el_coste_que_ya_tenia_cada_producto()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261029095912_VisitasDeCitasYProductosDelServicio");
        await bd.EjecutarAsync("""
            INSERT INTO CategoriasProductos (Nombre, Activa, CreadaEn) VALUES (N'Champús', 1, '2026-03-01');
            INSERT INTO Productos (Referencia, Nombre, CategoriaId, PrecioVenta, PrecioCoste, Stock, StockMinimo, Activo, ArchivadoConCategoria, CreadoEn, ActualizadoEn)
            VALUES (N'A-1', N'Con coste', 1, 10, 4.25, 0, 0, 1, 0, '2026-03-05T10:00:00', '2026-04-01T09:00:00'),
                   (N'A-2', N'Sin coste', 1, 10, NULL, 0, 0, 1, 0, '2026-03-05T10:00:00', '2026-03-05T10:00:00');
            """);

        await using (var db = bd.Contexto())
            await db.Database.MigrateAsync();

        Assert.Equal(1, await bd.EscalarAsync<int>("SELECT COUNT(*) FROM CostesProductos"));
        Assert.Equal("4.25|2026-03-05|Coste anterior al historial", await bd.EscalarAsync<string>(
            "SELECT CONCAT(CAST(c.Coste AS varchar(20)), '|', CONVERT(varchar(10), c.Desde, 23), '|', c.Nota) FROM CostesProductos c JOIN Productos p ON p.Id = c.ProductoId WHERE p.Referencia = N'A-1'"));
        Assert.Equal(4.25m, await bd.EscalarAsync<decimal>("SELECT PrecioCoste FROM Productos WHERE Referencia = N'A-1'"));
    }

    [Fact]
    public async Task Los_numeros_se_meten_a_mano_se_cambian_y_un_numero_sin_ficha_puede_ganar()
    {
        var corte = await ServicioAsync("Corte", "Cut");
        var id = await ConPremioAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte));
        var ana = await ClienteAsync("Ana", "ana@example.test", "611 222 333");
        var bea = await ClienteAsync("Bea", null, null);

        var conAna = await Sorteos().AgregarClienteAsync(id, ana, null);
        var conBea = await Sorteos().AgregarClienteAsync(id, bea, 20);
        var otraVez = await Sorteos().AgregarClienteAsync(id, ana, null);
        var sinCliente = await Sorteos().AgregarClienteAsync(id, null, 5);
        Assert.Equal((true, true, false), (conAna.Valor, conBea.Valor, otraVez.Valor));
        Assert.Equal("ClienteId", sinCliente.Error?.Campo);

        // Números de un sorteo que ya estaba en marcha en otro sitio: rangos, sueltos y con nombre
        var numeros = await Sorteos().AgregarNumerosAsync(id, "2-4\n7 María López\n1");
        Assert.True(numeros.Exito, numeros.Error?.Mensaje);
        Assert.Equal(4, numeros.Valor!.Agregados);
        Assert.Equal([1], numeros.Valor.Repetidos);
        Assert.Equal("Numeros", (await Sorteos().AgregarNumerosAsync(id, "María 12")).Error?.Campo);
        Assert.Equal("Numero", (await Sorteos().AgregarClienteAsync(id, await ClienteAsync("Carla", null, null), 7)).Error?.Campo);

        Assert.True((await Sorteos().CambiarNumeroAsync(id, 7, 27, "María López Gil")).Exito);
        Assert.Equal("Numero", (await Sorteos().CambiarNumeroAsync(id, 27, null, null)).Error?.Campo);
        Assert.Contains("Bea Pérez", (await Sorteos().CambiarNumeroAsync(id, 27, 20, null)).Error?.Mensaje);
        Assert.True((await Sorteos().QuitarParticipanteAsync(id, 3)).Exito);
        Assert.False((await Sorteos().QuitarParticipanteAsync(id, 3)).Exito);

        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal(["Nº 1 · Ana Pérez", "Nº 2", "Nº 4", "Nº 20 · Bea Pérez", "Nº 27 · María López Gil"],
            sorteo.Participantes.OrderBy(p => p.Numero).Select(p => p.Etiqueta));
        Assert.Equal(3, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM ParticipantesSorteos WHERE ClienteId IS NULL"));

        // Ganó en el sorteo de fuera: se elige por su número
        Assert.True((await Sorteos().ElegirGanadorAsync(id, 27, "Ganó en Instagram", null)).Exito);
        sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal(((int?)null, 27, "María López Gil", FormaGanador.ElegidoPorElSalon),
            (sorteo.Ganador!.ClienteId, sorteo.Ganador.Numero!.Value, sorteo.Ganador.Ganador, sorteo.Ganador.Forma));

        // Sin ficha no hay correo ni móvil: el salón copia el mensaje y le avisa por su cuenta
        var aviso = (await Sorteos().AvisoAsync(sorteo))!;
        Assert.True(aviso.SinFicha);
        Assert.Equal(((string?)null, (string?)null), (aviso.Correo, aviso.WhatsApp));
        Assert.StartsWith("¡Enhorabuena, María López Gil! Has ganado Corte", aviso.Texto);
        Assert.Contains("por tu cuenta", (await Sorteos().AvisarPorCorreoAsync(id, aviso.Texto)).Error?.Mensaje);
        Assert.Contains("por tu cuenta", (await Sorteos().AvisarPorWhatsAppAsync(id, aviso.Texto)).Error?.Mensaje);
        Assert.Empty(_correo.Enviados);

        // Al repetir no vuelve a salir el 27: el azar elige entre el 1, 2, 4 y 20
        _azar = 2;
        Assert.True((await Sorteos().RepetirAsync(id, "No contesta", null)).Exito);
        sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal((4, 4, "Nº 4"), (_pedido, sorteo.Ganador!.Numero!.Value, sorteo.Ganador.Etiqueta));
        Assert.StartsWith("¡Enhorabuena! El número 4 ha ganado", (await Sorteos().AvisoAsync(sorteo))!.Texto);
        Assert.Equal("27,4", await _bd.EscalarAsync<string>("SELECT STRING_AGG(Numero, ',') WITHIN GROUP (ORDER BY Id) FROM ExtraccionesSorteos"));
    }

    [Fact]
    public async Task La_migracion_numera_a_los_participantes_que_ya_habia_y_a_sus_ganadores_sin_borrar_nada()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261105103526_ProductosDeSorteoYVariosPremios");
        List<int> clientes;
        await using (var db = bd.Contexto())
        {
            var nuevos = new[] { "Ana", "Bea", "Carla" }.Select(n => Cliente.Crear(n, "Pérez", null, null, Sexo.Mujer, DateTime.Now)).ToList();
            db.AddRange(nuevos);
            await db.SaveChangesAsync();
            clientes = nuevos.Select(c => c.Id).ToList();
        }
        var (ana, bea, carla) = (clientes[0], clientes[1], clientes[2]);
        // Dos sorteos; en el A ya hubo dos extracciones: la primera, de un cliente ya borrado, y la última, Ana
        await bd.EjecutarAsync($"""
            INSERT INTO Sorteos (Titulo, FechaSorteo, Estado, CreadoEn, ActualizadoEn)
            VALUES (N'Sorteo A', '2026-11-20T18:00:00', 1, '2026-11-01', '2026-11-01'),
                   (N'Sorteo B', '2026-11-25T18:00:00', 0, '2026-11-01', '2026-11-01');
            DECLARE @a int = (SELECT Id FROM Sorteos WHERE Titulo = N'Sorteo A');
            DECLARE @b int = (SELECT Id FROM Sorteos WHERE Titulo = N'Sorteo B');
            INSERT INTO ParticipantesSorteos (SorteoId, ClienteId, AnadidoEn)
            VALUES (@a, {carla}, '2026-11-01'), (@a, {ana}, '2026-11-01'), (@b, {bea}, '2026-11-01'), (@a, {bea}, '2026-11-01'), (@b, {ana}, '2026-11-01');
            INSERT INTO ExtraccionesSorteos (SorteoId, ClienteId, Ganador, Fecha, Forma, Participantes)
            VALUES (@a, NULL, N'Cliente borrado', '2026-11-20T18:00:00', 0, 4),
                   (@a, {ana}, N'Ana Pérez', '2026-11-20T18:05:00', 0, 3);
            """);

        await using (var db = bd.Contexto())
            await db.Database.MigrateAsync();

        // En cada sorteo, 1, 2, 3… en el orden en que se apuntaron
        Assert.Equal("A:1-Carla,2-Ana,3-Bea;B:1-Bea,2-Ana", await bd.EscalarAsync<string>("""
            SELECT STRING_AGG(x.Grupo, ';') WITHIN GROUP (ORDER BY x.Titulo)
            FROM (SELECT s.Titulo, CONCAT(RIGHT(s.Titulo, 1), ':', STRING_AGG(CONCAT(p.Numero, '-', c.Nombre), ',') WITHIN GROUP (ORDER BY p.Numero)) AS Grupo
                  FROM ParticipantesSorteos p JOIN Sorteos s ON s.Id = p.SorteoId JOIN Clientes c ON c.Id = p.ClienteId
                  GROUP BY s.Titulo) AS x
            """));
        // Las extracciones llevan el número de su ganador si sigue en el sorteo; la del cliente borrado se queda sin él
        Assert.Equal("Cliente borrado|;Ana Pérez|2", await bd.EscalarAsync<string>(
            "SELECT STRING_AGG(CONCAT(Ganador, '|', Numero), ';') WITHIN GROUP (ORDER BY Id) FROM ExtraccionesSorteos"));
        await using (var db = bd.Contexto())
        {
            var sorteo = await new SorteoRepositorio(db).ObtenerAsync(await bd.EscalarAsync<int>("SELECT Id FROM Sorteos WHERE Titulo = N'Sorteo A'"), default);
            Assert.Equal("Nº 2 · Ana Pérez", sorteo!.Ganador!.Etiqueta);
            Assert.Equal([2], sorteo.Participantes.Where(sorteo.HaGanado).Select(p => p.Numero));
            Assert.Equal("Cliente borrado", sorteo.Extracciones[0].Etiqueta);
        }
    }

    private static DatosSorteo Datos(string titulo = "Sorteo de Navidad") =>
        new(titulo, titulo + " (en)", null, null, DateTime.Today.AddDays(10).AddHours(18));

    /// <summary>Cuántas veces está en marcha, se ha sorteado y se ha entregado un producto de sorteo.</summary>
    private static (int, int, int) Uso(IReadOnlyList<ResumenProductoSorteo> resumen, int id)
    {
        var r = resumen.Single(x => x.Producto.Id == id);
        return (r.EnMarcha, r.Sorteados, r.Entregados);
    }

    /// <summary>Un sorteo con un premio, listo para apuntar participantes.</summary>
    private async Task<int> ConPremioAsync(DatosSorteo datos, PremioElegido premio)
    {
        var id = (await Sorteos().CrearAsync(datos)).Valor;
        var agregado = await Sorteos().AgregarPremioAsync(id, premio, 1);
        Assert.True(agregado.Exito, agregado.Error?.Mensaje);
        return id;
    }

    /// <summary>Servicios de aplicación con un contexto nuevo, como en una petición web.</summary>
    private SorteosService Sorteos()
    {
        var db = _bd.Contexto();
        return new SorteosService(new SorteoRepositorio(db), new ServicioRepositorio(db), new ProductoRepositorio(db), new ProductoSorteoRepositorio(db), db,
            new AzarFijo(this), _correo, new WhatsAppManual(), TimeProvider.System);
    }

    private ProductosSorteoService ProductosSorteo()
    {
        var db = _bd.Contexto();
        return new ProductosSorteoService(new ProductoSorteoRepositorio(db), new SorteoRepositorio(db), db, TimeProvider.System);
    }

    private ProductosService Productos()
    {
        var db = _bd.Contexto();
        var fotos = new FotosProductos(Options.Create(new OpcionesFotosProductos { Carpeta = _carpeta }), NullLogger<FotosProductos>.Instance);
        return new ProductosService(new ProductoRepositorio(db), fotos, db, TimeProvider.System);
    }

    private async Task<int> ServicioAsync(string nombre, string nombreEn)
    {
        await using var db = _bd.Contexto();
        var servicio = Servicio.Crear(nombre, 20m, Sexo.Mujer, DateTime.Now, nombreEn);
        db.Add(servicio);
        await db.SaveChangesAsync();
        return servicio.Id;
    }

    private async Task<int> ClienteAsync(string nombre, string? email, string? telefono)
    {
        await using var db = _bd.Contexto();
        var cliente = Cliente.Crear(nombre, "Pérez", email, telefono, Sexo.Mujer, DateTime.Now);
        db.Add(cliente);
        await db.SaveChangesAsync();
        return cliente.Id;
    }

    private sealed class AzarFijo(SorteosTests prueba) : IAzar
    {
        public int Siguiente(int maximo)
        {
            prueba._pedido = maximo;
            return prueba._azar;
        }
    }

    private sealed class CorreoFalso : IEnviadorCorreo
    {
        public List<CorreoSaliente> Enviados { get; } = [];

        public bool Configurado => true;

        public Task<ResultadoEnvio> EnviarAsync(CorreoSaliente correo, CancellationToken ct = default)
        {
            Enviados.Add(correo);
            return Task.FromResult(ResultadoEnvio.Aceptado);
        }
    }
}
