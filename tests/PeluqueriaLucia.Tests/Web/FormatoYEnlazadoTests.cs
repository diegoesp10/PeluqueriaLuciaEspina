using PeluqueriaLucia.Application.Visitas;
using PeluqueriaLucia.Web.Infraestructura;

namespace PeluqueriaLucia.Tests.Web;

public sealed class FormatoYEnlazadoTests
{
    [Theory]
    [InlineData("12,50", 12.50)]
    [InlineData("12.50", 12.50)]
    [InlineData("1.234,50", 1234.50)]
    [InlineData("1,234.50", 1234.50)]
    [InlineData("8", 8)]
    [InlineData("-3,5", -3.5)]
    public void Los_importes_se_leen_con_coma_o_con_punto(string texto, decimal esperado)
    {
        Assert.True(DecimalFlexibleModelBinder.TryParse(texto, out var numero));
        Assert.Equal(esperado, numero);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12,5,0x")]
    public void Un_texto_que_no_es_un_importe_se_rechaza(string texto)
    {
        Assert.False(DecimalFlexibleModelBinder.TryParse(texto, out _));
    }

    [Fact]
    public void Los_euros_no_se_parten_entre_lineas()
    {
        Assert.Equal("1.234,50 €", Formato.Euros(1234.5m));
    }

    [Fact]
    public void El_filtro_de_un_mes_cubre_del_primer_al_ultimo_dia()
    {
        var filtro = FiltroVisitas.Mes(2026, 2);

        Assert.Equal(new DateTime(2026, 2, 1), filtro.Inicio);
        Assert.Equal(new DateTime(2026, 3, 1), filtro.FinExcluido);
        Assert.Equal("febrero de 2026", filtro.Descripcion);
    }
}
