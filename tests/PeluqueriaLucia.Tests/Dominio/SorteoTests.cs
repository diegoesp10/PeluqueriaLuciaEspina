using PeluqueriaLucia.Domain.Agenda;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Productos;
using PeluqueriaLucia.Domain.Sorteos;

namespace PeluqueriaLucia.Tests.Dominio;

/// <summary>
/// Sorteos: datos en los dos idiomas, premios (servicios, productos de la tienda y productos de sorteo), participantes,
/// extracción al azar o elegida por el salón, repeticiones con motivo, avisos y entrega del premio, y anuncios en la web.
/// Y el historial de costes de compra de los productos.
/// </summary>
public sealed class SorteoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 2, 10, 0, 0);
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(Ahora);

    private static DatosSorteo Datos(string? tituloEn = "Christmas giveaway", string? descripcion = null, string? descripcionEn = null) =>
        new("Sorteo de Navidad", tituloEn, descripcion, descripcionEn, new DateTime(2026, 12, 20, 18, 0, 30));

    private static DatosPremio Tinte() => new(TipoPremio.Servicio, 7, "Tinte ecológico", "Organic dye");

    private static DatosPremio Champu() => new(TipoPremio.Producto, 3, "Champú de argán", null);

    private static DatosPremio Cena(string nombre = "Cena para dos") =>
        new(TipoPremio.ProductoSorteo, 5, nombre, "Dinner for two", "En Casa Ramón, hasta marzo", "At Casa Ramón, until March");

    /// <summary>Cliente con identificador, como si viniera de la base de datos.</summary>
    private static Cliente Cliente(int id, string nombre)
    {
        var cliente = Domain.Clientes.Cliente.Crear(nombre, "Pérez", null, null, Sexo.Mujer, Ahora);
        typeof(Cliente).GetProperty(nameof(Domain.Clientes.Cliente.Id))!.SetValue(cliente, id);
        return cliente;
    }

    private static Sorteo ConParticipantes(params string[] nombres)
    {
        var sorteo = Sorteo.Crear(Datos(), Ahora);
        sorteo.AgregarPremio(Tinte(), 1, Ahora);
        sorteo.AgregarParticipantes(nombres.Select((n, i) => Cliente(i + 1, n)), Ahora);
        return sorteo;
    }

    [Fact]
    public void Se_crea_abierto_sin_premios_y_con_los_textos_en_los_dos_idiomas()
    {
        var sorteo = Sorteo.Crear(Datos(descripcion: "Entre las clientas de diciembre", descripcionEn: "Among December clients"), Ahora);

        Assert.Equal((EstadoSorteo.Abierto, "Sorteo de Navidad", "Christmas giveaway", "Among December clients"),
            (sorteo.Estado, sorteo.Titulo, sorteo.TituloEn, sorteo.DescripcionEnIdioma(true)));
        Assert.Equal(new DateTime(2026, 12, 20, 18, 0, 0), sorteo.FechaSorteo);
        Assert.Empty(sorteo.Premios);
        Assert.Equal("", sorteo.PremiosEnTexto());
        Assert.Null(sorteo.Ganador);
    }

    [Fact]
    public void Lo_que_se_publica_va_en_ingles_tambien()
    {
        var sinTitulo = Assert.Throws<ReglaDeNegocioException>(() => Sorteo.Crear(Datos(tituloEn: " "), Ahora));
        var sinDescripcion = Assert.Throws<ReglaDeNegocioException>(() => Sorteo.Crear(Datos(descripcion: "Para clientas"), Ahora));
        var raro = Assert.Throws<ReglaDeNegocioException>(() => Sorteo.Crear(Datos(), Ahora).AgregarPremio(new DatosPremio((TipoPremio)9, 1, "x", null), 1, Ahora));

        Assert.Equal(("TituloEn", "DescripcionEn", "Premio"), (sinTitulo.Campo, sinDescripcion.Campo, raro.Campo));
    }

    [Fact]
    public void Un_sorteo_puede_tener_varios_premios_de_la_peluqueria_y_productos_de_sorteo()
    {
        var sorteo = Sorteo.Crear(Datos(), Ahora);

        sorteo.AgregarPremio(Tinte(), 1, Ahora);
        var champu = sorteo.AgregarPremio(Champu(), 1, Ahora);
        var cena = sorteo.AgregarPremio(Cena(), 1, Ahora);
        // El mismo otra vez suma la cantidad
        var otraVez = sorteo.AgregarPremio(Champu(), 1, Ahora);

        Assert.Same(champu, otraVez);
        Assert.Equal([(TipoPremio.Servicio, 7, 1), (TipoPremio.Producto, 3, 2), (TipoPremio.ProductoSorteo, 5, 1)],
            sorteo.Premios.Select(p => (p.Tipo, p.ArticuloId!.Value, p.Cantidad)));
        Assert.Equal(((int?)null, (int?)null, (int?)5), (cena.ServicioId, cena.ProductoId, cena.ProductoSorteoId));
        Assert.Equal(("En Casa Ramón, hasta marzo", "At Casa Ramón, until March"), (cena.Descripcion, cena.DescripcionEnIdioma(true)));
        Assert.Equal("Tinte ecológico, 2 × Champú de argán y Cena para dos", sorteo.PremiosEnTexto());
        // El producto de la tienda no tiene nombre en inglés: sale el español
        Assert.Equal("Organic dye, 2 × Champú de argán and Dinner for two", sorteo.PremiosEnTexto(ingles: true));
    }

    [Fact]
    public void La_cantidad_va_de_1_a_99_y_un_sorteo_admite_hasta_veinte_premios()
    {
        var sorteo = Sorteo.Crear(Datos(), Ahora);

        var cero = Assert.Throws<ReglaDeNegocioException>(() => sorteo.AgregarPremio(Tinte(), 0, Ahora));
        sorteo.AgregarPremio(Tinte(), 98, Ahora);
        var demasiados = Assert.Throws<ReglaDeNegocioException>(() => sorteo.AgregarPremio(Tinte(), 2, Ahora));
        for (var i = 1; i < Sorteo.MaximoPremios; i++)
            sorteo.AgregarPremio(new DatosPremio(TipoPremio.ProductoSorteo, 100 + i, $"Regalo {i}", $"Gift {i}"), 1, Ahora);
        var veintiuno = Assert.Throws<ReglaDeNegocioException>(() => sorteo.AgregarPremio(Cena(), 1, Ahora));

        Assert.Equal(("Cantidad", "Cantidad", "Premio"), (cero.Campo, demasiados.Campo, veintiuno.Campo));
        Assert.Equal(98, sorteo.Premios[0].Cantidad);
        Assert.Equal(Sorteo.MaximoPremios, sorteo.Premios.Count);
    }

    [Fact]
    public void Los_premios_se_cambian_mientras_esta_abierto_y_sin_premio_no_se_sortea()
    {
        var sorteo = Sorteo.Crear(Datos(), Ahora);
        sorteo.AgregarParticipantes([Cliente(1, "Ana"), Cliente(2, "Bea")], Ahora);

        var sinPremio = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Sortear(_ => 0, Ahora, null));
        var sinPremioAMano = Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(1, null, Ahora, null));
        sorteo.AgregarPremio(Tinte(), 1, Ahora);
        sorteo.Sortear(_ => 0, Ahora, null);

        Assert.Equal(("Premio", "Premio"), (sinPremio.Campo, sinPremioAMano.Campo));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.AgregarPremio(Cena(), 1, Ahora));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.QuitarPremio(sorteo.Premios[0].Id, Ahora));
        Assert.Single(sorteo.Premios);
    }

    [Fact]
    public void Corregir_un_producto_de_sorteo_pone_al_dia_los_sorteos_abiertos_y_no_los_realizados()
    {
        var abierto = Sorteo.Crear(Datos(), Ahora);
        abierto.AgregarPremio(Cena(), 1, Ahora);
        var realizado = ConParticipantes("Ana", "Bea");
        realizado.AgregarPremio(Cena(), 1, Ahora);
        realizado.Sortear(_ => 0, Ahora, null);

        var corregida = Cena("Cena para dos personas");
        var cambiaAbierto = abierto.ActualizarPremio(corregida, Ahora);
        var cambiaRealizado = realizado.ActualizarPremio(corregida, Ahora);
        var sinCambios = abierto.ActualizarPremio(corregida, Ahora);

        Assert.Equal((true, false, false), (cambiaAbierto, cambiaRealizado, sinCambios));
        Assert.Equal("Cena para dos personas", abierto.Premios.Single().Nombre);
        Assert.Equal("Cena para dos", realizado.Premios.Single(p => p.Tipo == TipoPremio.ProductoSorteo).Nombre);
    }

    [Theory]
    [InlineData(new[] { "Corte" }, false, "Corte")]
    [InlineData(new[] { "Corte", "Hidratación" }, false, "Corte e Hidratación")]
    [InlineData(new[] { "Corte", "Iluminador" }, false, "Corte e Iluminador")]
    [InlineData(new[] { "Corte", "Hielo seco" }, false, "Corte y Hielo seco")]
    [InlineData(new[] { "Corte", "2 × Iluminador" }, false, "Corte y 2 × Iluminador")]
    [InlineData(new[] { "Corte", "Mascarilla", "Cena" }, false, "Corte, Mascarilla y Cena")]
    [InlineData(new[] { "Cut", "Mask", "Dinner" }, true, "Cut, Mask and Dinner")]
    public void Los_premios_se_unen_como_se_diria(string[] partes, bool ingles, string esperado) =>
        Assert.Equal(esperado, PremioSorteo.Unir(partes, ingles));

    [Fact]
    public void Los_participantes_repetidos_no_se_duplican_y_se_pueden_quitar_mientras_esta_abierto()
    {
        var sorteo = ConParticipantes("Ana", "Bea");

        var nuevos = sorteo.AgregarParticipantes([Cliente(2, "Bea"), Cliente(3, "Carla"), Cliente(3, "Carla")], Ahora);
        sorteo.QuitarParticipante(1, Ahora);

        Assert.Equal(1, nuevos);
        Assert.Equal([2, 3], sorteo.Participantes.Select(p => p.ClienteId));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.QuitarParticipante(1, Ahora));
    }

    [Fact]
    public void Sortear_elige_al_que_dice_el_azar_entre_todos_y_lo_deja_en_el_historial()
    {
        var sorteo = ConParticipantes("Ana", "Bea", "Carla");
        var pedido = 0;

        var extraccion = sorteo.Sortear(n => { pedido = n; return 1; }, Ahora, administradorId: 4);

        Assert.Equal(3, pedido);
        Assert.Equal(EstadoSorteo.Realizado, sorteo.Estado);
        Assert.Same(extraccion, sorteo.Ganador);
        Assert.Equal((2, "Bea Pérez", FormaGanador.AlAzar, 3, Ahora, (int?)4, (string?)null), (extraccion.ClienteId!.Value, extraccion.Ganador, extraccion.Forma, extraccion.Participantes, extraccion.Fecha, extraccion.AdministradorId, extraccion.Motivo));
        Assert.Null(extraccion.EntregadoEn);
        // Ya sorteado, los participantes y los datos no cambian
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.AgregarParticipantes([Cliente(9, "Zoe")], Ahora));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.Editar(Datos(), Ahora));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.Sortear(_ => 0, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.ComprobarQueSePuedeEliminar());
    }

    [Fact]
    public void Para_sortear_hacen_falta_dos_participantes_y_un_azar_dentro_del_rango()
    {
        var uno = ConParticipantes("Ana");
        var dos = ConParticipantes("Ana", "Bea");

        Assert.Equal("Participantes", Assert.Throws<ReglaDeNegocioException>(() => uno.Sortear(_ => 0, Ahora, null)).Campo);
        Assert.Throws<InvalidOperationException>(() => dos.Sortear(n => n, Ahora, null));
        Assert.Equal(EstadoSorteo.Abierto, dos.Estado);
    }

    [Fact]
    public void Repetir_pide_motivo_y_sortea_solo_entre_quienes_no_han_ganado()
    {
        var sorteo = ConParticipantes("Ana", "Bea", "Carla");
        sorteo.Sortear(_ => 0, Ahora, null);

        var sinMotivo = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Repetir(_ => 0, " ", Ahora, null));
        var pedido = 0;
        var segunda = sorteo.Repetir(n => { pedido = n; return 0; }, "No ha respondido", Ahora.AddDays(1), null);
        sorteo.Repetir(_ => 0, "Tampoco", Ahora.AddDays(2), null);
        var nadie = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Repetir(_ => 0, "Otra vez", Ahora.AddDays(3), null));

        Assert.Equal("Motivo", sinMotivo.Campo);
        Assert.Equal((2, 2, "Bea Pérez", "No ha respondido"), (pedido, segunda.Participantes, segunda.Ganador, segunda.Motivo));
        Assert.Equal(["Ana Pérez", "Bea Pérez", "Carla Pérez"], sorteo.Extracciones.Select(e => e.Ganador));
        Assert.Equal("Carla Pérez", sorteo.Ganador!.Ganador);
        Assert.Contains("todos", nadie.Message);
    }

    [Fact]
    public void El_salon_puede_elegir_al_ganador_y_cambiarlo_con_motivo()
    {
        var sorteo = ConParticipantes("Ana", "Bea");

        var elegido = sorteo.ElegirGanador(2, null, Ahora, 1);
        var mismo = Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(2, "otra vez", Ahora, 1));
        var sinMotivo = Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(1, null, Ahora, 1));
        var deFuera = Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(9, "no participa", Ahora, 1));
        sorteo.ElegirGanador(1, "Bea no puede venir", Ahora.AddHours(1), 1);

        Assert.Equal((FormaGanador.ElegidoPorElSalon, 2, EstadoSorteo.Realizado), (elegido.Forma, elegido.Participantes, sorteo.Estado));
        Assert.Equal(("ClienteId", "Motivo", "ClienteId"), (mismo.Campo, sinMotivo.Campo, deFuera.Campo));
        Assert.Equal((1, "Bea no puede venir"), (sorteo.Ganador!.ClienteId!.Value, sorteo.Ganador.Motivo));
    }

    [Fact]
    public void El_aviso_al_ganador_queda_anotado_en_su_extraccion()
    {
        var sorteo = ConParticipantes("Ana", "Bea");
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.MarcarAvisado(CanalConfirmacion.Correo, Ahora));
        sorteo.Sortear(_ => 0, Ahora, null);

        sorteo.MarcarAvisado(CanalConfirmacion.WhatsApp, Ahora.AddMinutes(5));

        Assert.Equal((CanalConfirmacion.WhatsApp, Ahora.AddMinutes(5)), (sorteo.Ganador!.AvisoCanal!.Value, sorteo.Ganador.AvisadoEn!.Value));
    }

    [Fact]
    public void Entregado_el_premio_queda_en_el_historial_y_el_ganador_ya_no_cambia()
    {
        var sorteo = ConParticipantes("Ana", "Bea", "Carla");
        var sinGanador = Assert.Throws<ReglaDeNegocioException>(() => sorteo.MarcarEntregado(Ahora));
        sorteo.Sortear(_ => 0, Ahora, null);

        sorteo.MarcarEntregado(Ahora.AddDays(2));

        Assert.Contains("ganador", sinGanador.Message);
        Assert.Equal(Ahora.AddDays(2), sorteo.Ganador!.EntregadoEn);
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.MarcarEntregado(Ahora.AddDays(3)));
        Assert.Contains("ya se entregó", Assert.Throws<ReglaDeNegocioException>(() => sorteo.Repetir(_ => 0, "No vino", Ahora, null)).Message);
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(2, "Otra", Ahora, null));
        Assert.Single(sorteo.Extracciones);
    }

    [Fact]
    public void Cancelado_queda_en_el_historial_y_no_se_sortea_ni_se_anuncia()
    {
        var sorteo = ConParticipantes("Ana", "Bea");

        sorteo.Cancelar("Se aplaza", Ahora);

        Assert.Equal((EstadoSorteo.Cancelado, "Se aplaza"), (sorteo.Estado, sorteo.MotivoCancelacion));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.Sortear(_ => 0, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.ElegirGanador(1, null, Ahora, null));
        Assert.Throws<ReglaDeNegocioException>(() => sorteo.Anunciar("Hola", "Hi", Hoy, Hoy.AddDays(3), Ahora));
        sorteo.ComprobarQueSePuedeEliminar();
    }

    [Fact]
    public void Un_anuncio_se_ve_entre_sus_fechas_y_en_el_idioma_de_la_web()
    {
        var sorteo = ConParticipantes("Ana", "Bea");

        var anuncio = sorteo.Anunciar(" Participa con tu visita ", "Join with your visit", Hoy, Hoy.AddDays(10), Ahora);
        var alReves = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Anunciar("Hola", "Hi", Hoy.AddDays(3), Hoy, Ahora));
        var pasado = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Anunciar("Hola", "Hi", Hoy.AddDays(-9), Hoy.AddDays(-1), Ahora));
        var sinIngles = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Anunciar("Hola", null, Hoy, Hoy.AddDays(1), Ahora));
        var largo = Assert.Throws<ReglaDeNegocioException>(() => sorteo.Anunciar("Hola", "Hi", Hoy, Hoy.AddDays(400), Ahora));

        Assert.Equal("Participa con tu visita", anuncio.Texto);
        Assert.Equal("Join with your visit", anuncio.TextoEnIdioma(true));
        Assert.Equal([false, true, true, false], new[] { Hoy.AddDays(-1), Hoy, Hoy.AddDays(10), Hoy.AddDays(11) }.Select(anuncio.VisibleEl));
        Assert.Equal(("HastaAnuncio", "HastaAnuncio", "TextoAnuncioEn", "HastaAnuncio"), (alReves.Campo, pasado.Campo, sinIngles.Campo, largo.Campo));
        Assert.Single(sorteo.Anuncios);
    }

    [Fact]
    public void Un_producto_de_sorteo_lleva_nombre_y_descripcion_en_los_dos_idiomas_y_se_elimina_sin_borrarse()
    {
        var cena = ProductoSorteo.Crear(new FichaProductoSorteo(" Cena para dos ", "Dinner for two", "En Casa Ramón", "At Casa Ramón"), Ahora);
        var sinIngles = Assert.Throws<ReglaDeNegocioException>(() => ProductoSorteo.Crear(new FichaProductoSorteo("Entradas", " ", null, null), Ahora));
        var sinDescripcionEn = Assert.Throws<ReglaDeNegocioException>(() => ProductoSorteo.Crear(new FichaProductoSorteo("Entradas", "Tickets", "Para el teatro", null), Ahora));

        Assert.Equal(("Cena para dos", "Dinner for two", "At Casa Ramón", true), (cena.Nombre, cena.NombreEnIdioma(true), cena.DescripcionEnIdioma(true), cena.Activo));
        Assert.Equal(("NombreEn", "DescripcionEn"), (sinIngles.Campo, sinDescripcionEn.Campo));
        Assert.Equal((TipoPremio.ProductoSorteo, "Cena para dos", "En Casa Ramón"), (DatosPremio.De(cena).Tipo, DatosPremio.De(cena).Nombre, DatosPremio.De(cena).Descripcion));

        cena.Eliminar(Ahora);
        Assert.Equal((false, (DateTime?)Ahora), (cena.Activo, cena.EliminadoEn));
        Assert.Throws<ReglaDeNegocioException>(() => cena.Eliminar(Ahora));
        cena.Recuperar(Ahora.AddDays(1));
        Assert.Equal((true, (DateTime?)null), (cena.Activo, cena.EliminadoEn));
    }

    [Fact]
    public void Un_coste_reciente_pasa_a_ser_el_del_producto_y_uno_pasado_solo_completa_el_historial()
    {
        var producto = Producto.Crear(ProductoTests.Ficha(coste: 100m), ProductoTests.Categoria(), Ahora);

        var febrero = producto.RegistrarCoste(120m, new DateOnly(2026, 2, 1), " Proveedor nuevo ", new DateOnly(2026, 1, 1), Ahora, 3);
        var enero = producto.RegistrarCoste(95m, new DateOnly(2026, 1, 15), null, new DateOnly(2026, 2, 1), Ahora, 3);
        var futuro = Assert.Throws<ReglaDeNegocioException>(() => producto.RegistrarCoste(1m, Hoy.AddDays(1), null, null, Ahora, null));
        var negativo = Assert.Throws<ReglaDeNegocioException>(() => producto.RegistrarCoste(-1m, Hoy, null, null, Ahora, null));

        Assert.Equal(120m, producto.PrecioCoste);
        Assert.Equal((120m, new DateOnly(2026, 2, 1), "Proveedor nuevo", (int?)3), (febrero.Coste, febrero.Desde, febrero.Nota, febrero.AdministradorId));
        Assert.Equal(95m, enero.Coste);
        Assert.Equal(("Desde", "Coste"), (futuro.Campo, negativo.Campo));
    }

    [Fact]
    public void La_ficha_solo_anota_el_coste_cuando_cambia()
    {
        var producto = Producto.Crear(ProductoTests.Ficha(coste: 7.50m), ProductoTests.Categoria(), Ahora);

        var alta = producto.CosteDeLaFicha(null, "Al dar de alta", Ahora, null);
        var igual = producto.CosteDeLaFicha(7.50m, null, Ahora, null);
        producto.Editar(ProductoTests.Ficha(coste: null), ProductoTests.Categoria(), Ahora);
        var quitado = producto.CosteDeLaFicha(7.50m, null, Ahora, null);

        Assert.Equal((7.50m, Hoy, "Al dar de alta"), (alta!.Coste, alta.Desde, alta.Nota));
        Assert.Null(igual);
        Assert.Null(quitado);
    }
}
