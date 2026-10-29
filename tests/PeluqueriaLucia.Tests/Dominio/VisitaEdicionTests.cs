using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Tests.Dominio;

/// <summary>
/// Una visita pendiente se edita hasta cobrarla: precios de cada línea, servicios que se añaden o se quitan, y la visita de una
/// cita de la agenda, que empieza con el servicio de la cita.
/// </summary>
public sealed class VisitaEdicionTests
{
    private static readonly DateTime Ahora = new(2026, 10, 29, 16, 0, 0);

    private static Cliente Ana() => Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, Ahora);

    private static Servicio Corte() => Servicio.Crear("Corte", 18m, Sexo.Mujer, Ahora);

    private static Servicio Tinte() => Servicio.Crear("Tinte", 35m, Sexo.Mujer, Ahora);

    [Fact]
    public void Los_precios_se_cambian_antes_de_cobrar_y_se_cobra_el_nuevo_total()
    {
        var champu = ProductoTests.Guardado(1, stock: 5, precio: 14.90m);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(champu, 2)]);
        var linea = visita.Lineas[0].Id;
        var producto = visita.Productos[0].Id;

        visita.CambiarPrecios(new Dictionary<int, decimal> { [linea] = 22.505m }, new Dictionary<int, decimal> { [producto] = 12m });
        visita.Cobrar(FormaPago.Efectivo, null, null, [champu]);

        Assert.Equal((22.51m, 12m, 24m), (visita.Lineas[0].Precio, visita.Productos[0].PrecioUnitario, visita.Productos[0].Importe));
        Assert.Equal((46.51m, 46.51m), (visita.Total, visita.TotalEfectivo!.Value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(10000)]
    public void Si_un_precio_no_vale_no_cambia_ninguno(decimal malo)
    {
        var champu = ProductoTests.Guardado(1, stock: 5, precio: 14.90m);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(champu, 1)]);

        var ex = Assert.Throws<ReglaDeNegocioException>(() => visita.CambiarPrecios(
            new Dictionary<int, decimal> { [visita.Lineas[0].Id] = 20m }, new Dictionary<int, decimal> { [visita.Productos[0].Id] = malo }));

        Assert.Equal("Productos", ex.Campo);
        Assert.Contains("Champú de argán", ex.Message);
        Assert.Equal((18m, 14.90m), (visita.Lineas[0].Precio, visita.Productos[0].PrecioUnitario));
    }

    [Fact]
    public void Las_lineas_que_ya_no_estan_se_pasan_por_alto_y_lo_incluido_sigue_a_cero()
    {
        var tinte = ProductoTests.Guardado(1, stock: 5, nombre: "Tinte 7.3");
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()], productos: [new(tinte, 1, Uso: UsoProducto.ServicioTerminado)]);

        visita.CambiarPrecios(new Dictionary<int, decimal> { [999] = 50m }, new Dictionary<int, decimal> { [visita.Productos[0].Id] = 12m });

        Assert.Equal((18m, 0m), (visita.Total, visita.Productos[0].Importe));
    }

    [Fact]
    public void Una_visita_cobrada_no_cambia_precios_ni_servicios()
    {
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);
        visita.Cobrar(FormaPago.Efectivo, null, null);

        var precios = Assert.Throws<ReglaDeNegocioException>(() => visita.CambiarPrecios(new Dictionary<int, decimal> { [visita.Lineas[0].Id] = 10m }, null));
        Assert.Throws<ReglaDeNegocioException>(() => visita.AgregarServicio(Tinte()));
        Assert.Throws<ReglaDeNegocioException>(() => visita.QuitarServicio(visita.Lineas[0].Id));

        Assert.Contains("cobrada", precios.Message);
        Assert.Equal(18m, visita.TotalEfectivo);
    }

    [Fact]
    public void Se_anaden_servicios_a_su_precio_o_al_indicado_y_se_quitan_sin_dejarla_vacia()
    {
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);

        visita.AgregarServicio(Tinte());
        visita.AgregarServicio(Tinte(), 30m);
        var cero = Assert.Throws<ReglaDeNegocioException>(() => visita.AgregarServicio(Tinte(), 0m));
        visita.QuitarServicio(visita.Lineas[0].Id);
        visita.QuitarServicio(visita.Lineas[0].Id);
        var ultima = Assert.Throws<ReglaDeNegocioException>(() => visita.QuitarServicio(visita.Lineas[0].Id));

        Assert.Equal("Servicios", cero.Campo);
        Assert.Contains("vacía", ultima.Message);
        Assert.Equal([("Tinte", 30m)], visita.Lineas.Select(l => (l.Nombre, l.Precio)));
    }

    [Fact]
    public void Un_servicio_archivado_no_se_anade()
    {
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);
        var archivado = Tinte();
        archivado.Archivar();

        var ex = Assert.Throws<ReglaDeNegocioException>(() => visita.AgregarServicio(archivado));

        Assert.Contains("archivado", ex.Message);
        Assert.Single(visita.Lineas);
    }

    [Fact]
    public void La_visita_de_una_cita_empieza_con_su_servicio_y_sin_el_si_no_tiene_precio()
    {
        var cita = Guid.NewGuid();
        var porDefinir = Servicio.Crear("Recogido", 0m, Sexo.Mujer, Ahora);

        var conServicio = Visita.DesdeCita(Ana(), cita, Ahora, Corte());
        var sinPrecio = Visita.DesdeCita(Ana(), Guid.NewGuid(), Ahora, porDefinir);
        var consulta = Visita.DesdeCita(Ana(), Guid.NewGuid(), Ahora, null);

        Assert.Equal((cita, Ahora, EstadoVisita.Pendiente, 18m), (conServicio.ReservaId!.Value, conServicio.Fecha, conServicio.Estado, conServicio.Total));
        Assert.Empty(sinPrecio.Lineas);
        Assert.Empty(consulta.Lineas);
        // Vacía no se cobra; con un servicio añadido, sí
        Assert.Throws<ReglaDeNegocioException>(() => consulta.Cobrar(FormaPago.Efectivo, null, null));
        consulta.AgregarServicio(porDefinir, 45m);
        consulta.Cobrar(FormaPago.Tarjeta, null, null);
        Assert.Equal(45m, consulta.TotalTarjeta);
    }

    [Fact]
    public void Una_visita_registrada_aparte_se_enlaza_a_su_cita_pero_no_a_otra()
    {
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [Corte()]);
        var cita = Guid.NewGuid();

        visita.VincularCita(cita);
        visita.VincularCita(cita);

        Assert.Equal(cita, visita.ReservaId);
        Assert.Throws<ReglaDeNegocioException>(() => visita.VincularCita(Guid.NewGuid()));
    }
}
