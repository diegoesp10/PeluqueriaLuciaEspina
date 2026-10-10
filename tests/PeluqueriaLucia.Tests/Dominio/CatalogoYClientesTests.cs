using PeluqueriaLucia.Domain.Catalogo;
using PeluqueriaLucia.Domain.Clientes;
using PeluqueriaLucia.Domain.Comun;

namespace PeluqueriaLucia.Tests.Dominio;

public sealed class CatalogoYClientesTests
{
    private static readonly DateTime Alta = new(2025, 7, 1);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    public void Un_servicio_no_admite_precios_fuera_de_rango(decimal precio)
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => Servicio.Crear("Corte", precio, Sexo.Mujer, Alta));
        Assert.Equal("Precio", ex.Campo);
    }

    [Fact]
    public void El_precio_se_redondea_a_centimos()
    {
        Assert.Equal(12.35m, Servicio.Crear("Corte", 12.345m, Sexo.Mujer, Alta).Precio);
    }

    [Fact]
    public void Actualizar_un_cliente_conserva_la_fecha_de_alta_y_limpia_los_textos()
    {
        var cliente = Cliente.Crear("Ana", "García", "ana@ejemplo.es", "600111222", Sexo.Mujer, Alta);

        cliente.Actualizar("  Ana María ", "García", "   ", "", Sexo.Mujer);

        Assert.Equal(Alta, cliente.FechaCreacion);
        Assert.Equal("Ana María", cliente.Nombre);
        Assert.Null(cliente.Email);
        Assert.Null(cliente.Telefono);
        Assert.Equal("AG", cliente.Iniciales);
    }

    [Fact]
    public void Un_cliente_necesita_nombre_y_apellido()
    {
        var ex = Assert.Throws<ReglaDeNegocioException>(() => Cliente.Crear("Ana", " ", null, null, Sexo.Mujer, Alta));
        Assert.Equal("Apellido", ex.Campo);
    }
}
