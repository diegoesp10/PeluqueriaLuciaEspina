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

/// <summary>Sorteos y costes de compra contra SQL Server real, con un contexto por petición como en la web.</summary>
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

        var creado = await Sorteos().CrearAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte));
        var id = creado.Valor;
        var nuevos = await Sorteos().AgregarParticipantesAsync(id, [ana, bea, ana]);
        var repetidos = await Sorteos().AgregarParticipantesAsync(id, [bea, carla]);
        var vacio = await Sorteos().AgregarParticipantesAsync(id, [999999]);

        Assert.True(creado.Exito, creado.Error?.Mensaje);
        Assert.Equal((2, 1), (nuevos.Valor, repetidos.Valor));
        Assert.Equal("ClienteId", vacio.Error?.Campo);

        // El azar dice «el segundo»: en el orden en que se apuntaron, Bea
        _azar = 1;
        var sorteado = await Sorteos().SortearAsync(id, administradorId: null);
        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.True(sorteado.Exito, sorteado.Error?.Mensaje);
        Assert.Equal(3, _pedido);
        Assert.Equal((EstadoSorteo.Realizado, bea, "Bea Pérez", FormaGanador.AlAzar, 3), (sorteo.Estado, sorteo.Ganador!.ClienteId!.Value, sorteo.Ganador.Ganador, sorteo.Ganador.Forma, sorteo.Ganador.Participantes));
        Assert.Equal("Cut and styling", sorteo.PremioEn);

        // Bea no tiene ni correo ni móvil: se repite entre Ana y Carla
        var aviso = (await Sorteos().AvisoAsync(sorteo))!;
        Assert.Equal((null, null), (aviso.Correo, aviso.WhatsApp));
        Assert.Contains("¡Enhorabuena, Bea!", aviso.Texto);
        Assert.Contains("Corte y peinado", aviso.Texto);
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

        // Con ganador, no se borra: queda en el historial
        Assert.False((await Sorteos().EliminarAsync(id)).Exito);
        Assert.Equal([id], (await Sorteos().ListarAsync(historial: true)).Select(s => s.Id));
        Assert.Empty(await Sorteos().ListarAsync(historial: false));
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
        var id = (await Sorteos().CrearAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte))).Valor;

        var octubreEntero = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var otraVez = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31));
        var ninguna = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));
        var alReves = await Sorteos().AgregarClientesConVisitasAsync(id, new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1));

        Assert.Equal((2, 0), (octubreEntero.Valor, otraVez.Valor));
        Assert.Equal(("DesdeVisitas", "HastaVisitas"), (ninguna.Error?.Campo, alReves.Error?.Campo));
        Assert.Equal([ana, bea], (await Sorteos().ObtenerAsync(id))!.Participantes.Select(p => p.ClienteId).Order());
    }

    [Fact]
    public async Task La_web_solo_enseña_los_anuncios_de_hoy_de_sorteos_no_cancelados()
    {
        var tinte = await ServicioAsync("Tinte", "Dye");
        var hoy = Sorteos().Hoy;
        var abierto = (await Sorteos().CrearAsync(Datos("Sorteo de otoño"), new PremioElegido(TipoPremio.Servicio, tinte))).Valor;
        var cancelado = (await Sorteos().CrearAsync(Datos("Sorteo aplazado"), new PremioElegido(TipoPremio.Servicio, tinte))).Valor;
        await Sorteos().AnunciarAsync(abierto, "Participa con tu visita", "Join with your visit", hoy, hoy.AddDays(5));
        await Sorteos().AnunciarAsync(abierto, "Muy pronto", "Coming soon", hoy.AddDays(6), hoy.AddDays(9));
        await Sorteos().AnunciarAsync(cancelado, "Aplazado", "Postponed", hoy, hoy.AddDays(5));
        var sinFechas = await Sorteos().AnunciarAsync(abierto, "Sin fechas", "No dates", null, hoy);
        Assert.Equal(2, (await Sorteos().AnunciosVisiblesAsync()).Count);

        await Sorteos().CancelarAsync(cancelado, "Se aplaza");
        var visibles = await Sorteos().AnunciosVisiblesAsync();

        Assert.Equal("DesdeAnuncio", sinFechas.Error?.Campo);
        var anuncio = Assert.Single(visibles);
        Assert.Equal(("Sorteo de otoño", "Tinte", "Dye", "Join with your visit", false), (anuncio.Titulo, anuncio.Premio, anuncio.PremioEnIdioma(true), anuncio.TextoEnIdioma(true), anuncio.Realizado));
        // Quitar el anuncio lo saca de la web; borrar un sorteo sin ganador se lleva sus anuncios
        var anuncioId = anuncio.AnuncioId;
        Assert.True((await Sorteos().QuitarAnuncioAsync(abierto, anuncioId)).Exito);
        Assert.Empty(await Sorteos().AnunciosVisiblesAsync());
        Assert.True((await Sorteos().EliminarAsync(abierto)).Exito);
        Assert.Equal(1, await _bd.EscalarAsync<int>("SELECT COUNT(*) FROM AnunciosSorteos"));
    }

    [Fact]
    public async Task Borrar_la_ficha_del_ganador_o_el_premio_conserva_el_historial_con_sus_nombres()
    {
        var champu = (await Productos().CrearAsync(new DatosProducto(ProductoTests.Ficha("CH-S"), new CategoriaElegida(null, "Champús"), 2), [], null)).Valor;
        var ana = await ClienteAsync("Ana", null, null);
        var bea = await ClienteAsync("Bea", null, null);
        var id = (await Sorteos().CrearAsync(Datos(), new PremioElegido(TipoPremio.Producto, champu))).Valor;
        await Sorteos().AgregarParticipantesAsync(id, [ana, bea]);
        _azar = 0;
        await Sorteos().SortearAsync(id, null);

        await _bd.EjecutarAsync($"DELETE FROM Clientes WHERE Id = {ana}");
        await _bd.EjecutarAsync($"DELETE FROM Productos WHERE Id = {champu}");

        var sorteo = (await Sorteos().ObtenerAsync(id))!;
        Assert.Equal((null, "Ana Pérez"), (sorteo.Ganador!.ClienteId, sorteo.Ganador.Ganador));
        Assert.Equal((null, "Champú de argán"), (sorteo.ProductoId, sorteo.Premio));
        Assert.Equal([bea], sorteo.Participantes.Select(p => p.ClienteId));
    }

    [Fact]
    public async Task Dos_sorteos_a_la_vez_no_dan_dos_ganadores()
    {
        var corte = await ServicioAsync("Corte", "Cut");
        var id = (await Sorteos().CrearAsync(Datos(), new PremioElegido(TipoPremio.Servicio, corte))).Valor;
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

    private static DatosSorteo Datos(string titulo = "Sorteo de Navidad") =>
        new(titulo, titulo + " (en)", null, null, DateTime.Today.AddDays(10).AddHours(18));

    /// <summary>Servicios de aplicación con un contexto nuevo, como en una petición web.</summary>
    private SorteosService Sorteos()
    {
        var db = _bd.Contexto();
        return new SorteosService(new SorteoRepositorio(db), new ServicioRepositorio(db), new ProductoRepositorio(db), db,
            new AzarFijo(this), _correo, new WhatsAppManual(), TimeProvider.System);
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
