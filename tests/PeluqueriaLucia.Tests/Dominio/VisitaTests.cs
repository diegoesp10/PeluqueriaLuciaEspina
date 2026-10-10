using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Visitas;

namespace PeluqueriaLucia.Tests.Dominio;

public sealed class VisitaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 10, 12, 0, 0);

    private static Cliente Ana() => Cliente.Crear("Ana", "García", null, null, Sexo.Mujer, Ahora);

    private static Visita VisitaDe(params decimal[] precios) =>
        Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo,
            precios.Select((p, i) => Servicio.Crear($"Servicio {i}", p, Sexo.Mujer, Ahora)).ToList());

    [Fact]
    public void Registrar_guarda_nombre_y_precio_del_momento()
    {
        var corte = Servicio.Crear("Corte", 18.50m, Sexo.Mujer, Ahora);
        var visita = Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [corte]);

        corte.Actualizar("Corte nuevo", 25m, Sexo.Mujer);

        var linea = Assert.Single(visita.Lineas);
        Assert.Equal("Corte", linea.Nombre);
        Assert.Equal(18.50m, linea.Precio);
        Assert.Equal(18.50m, visita.Total);
    }

    [Fact]
    public void Registrar_sin_servicios_falla()
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, []));
        Assert.Equal("Servicios", ex.Campo);
    }

    [Fact]
    public void Registrar_con_un_servicio_archivado_falla()
    {
        var archivado = Servicio.Crear("Permanente", 40m, Sexo.Mujer, Ahora);
        archivado.Archivar();

        Assert.Throws<ReglaDeNegocioException>(() => Visita.Registrar(Ana(), Ahora, FormaPago.Efectivo, [archivado]));
    }

    [Theory]
    [InlineData(FormaPago.Efectivo, 53.50, 0)]
    [InlineData(FormaPago.Tarjeta, 0, 53.50)]
    public void Cobrar_asigna_el_total_a_la_forma_de_pago(FormaPago forma, decimal efectivo, decimal tarjeta)
    {
        var visita = VisitaDe(18.50m, 35m);

        visita.Cobrar(forma, null, null);

        Assert.True(visita.EstaCobrada);
        Assert.Equal(forma, visita.FormaPago);
        Assert.Equal(efectivo, visita.TotalEfectivo);
        Assert.Equal(tarjeta, visita.TotalTarjeta);
    }

    [Fact]
    public void Cobrar_mixto_que_cuadra_guarda_el_reparto()
    {
        var visita = VisitaDe(18.50m, 35m);

        visita.Cobrar(FormaPago.Mixto, 20m, 33.50m);

        Assert.Equal(20m, visita.TotalEfectivo);
        Assert.Equal(33.50m, visita.TotalTarjeta);
    }

    [Theory]
    [InlineData(20, 30)]
    [InlineData(53.50, 0)]
    [InlineData(-5, 58.50)]
    public void Cobrar_mixto_que_no_cuadra_falla_y_no_cambia_nada(decimal efectivo, decimal tarjeta)
    {
        var visita = VisitaDe(18.50m, 35m);

        Assert.Throws<ReglaDeNegocioException>(() => visita.Cobrar(FormaPago.Mixto, efectivo, tarjeta));
        Assert.False(visita.EstaCobrada);
        Assert.Null(visita.TotalEfectivo);
    }

    [Fact]
    public void Cobrar_dos_veces_falla()
    {
        var visita = VisitaDe(10m);
        visita.Cobrar(FormaPago.Efectivo, null, null);

        Assert.Throws<ReglaDeNegocioException>(() => visita.Cobrar(FormaPago.Tarjeta, null, null));
        Assert.Equal(FormaPago.Efectivo, visita.FormaPago);
    }

    [Fact]
    public void Una_visita_cobrada_no_se_puede_eliminar()
    {
        var visita = VisitaDe(10m);
        visita.ComprobarQueSePuedeEliminar();
        visita.Cobrar(FormaPago.Efectivo, null, null);

        Assert.Throws<ReglaDeNegocioException>(visita.ComprobarQueSePuedeEliminar);
    }
}
