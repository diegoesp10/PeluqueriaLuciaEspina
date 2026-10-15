using Microsoft.EntityFrameworkCore;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Domain.Comun;
using PeluqueriaLucia.Domain.Publico;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class TelefonosContactoTests
{
    [Theory]
    [InlineData("659306659", "659306659", "tel:+34659306659")]
    [InlineData("  985  757 692 ", "985 757 692", "tel:+34985757692")]
    [InlineData("+34 659 306 659", "+34 659 306 659", "tel:+34659306659")]
    [InlineData("(985) 75-76-92", "(985) 75-76-92", "tel:+34985757692")]
    public void Telefono_valido_se_guarda_con_espacios_simples_y_su_enlace_lleva_prefijo(string escrito, string guardado, string enlace)
    {
        Assert.Equal(guardado, TelefonoContacto.Normalizar(escrito));
        Assert.Equal(enlace, TelefonoContacto.Enlace(guardado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("659 306 659 ext. 2")]
    [InlineData("659+306659")]
    [InlineData("<script>659306659</script>")]
    [InlineData("1234567890123456")]
    public void Telefono_no_valido_se_rechaza_con_su_campo(string escrito)
    {
        var error = Assert.Throws<ReglaDeNegocioException>(() => TelefonoContacto.Normalizar(escrito));
        Assert.Equal("Telefonos", error.Campo);
    }

    [Fact]
    public async Task Guardar_sustituye_la_lista_en_orden_sin_vacios_ni_repetidos()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync(); await using var db = bd.Contexto();
        var servicio = new ContenidoService(new ContenidoRepositorio(db), new ServicioRepositorio(db), db);
        Assert.Empty(await servicio.TelefonosAsync());
        Assert.True((await servicio.GuardarTelefonosAsync(["659 306 659", " ", "985757692", "659 306 659"])).Exito);
        Assert.Equal(["659 306 659", "985757692"], (await servicio.TelefonosAsync()).Select(t => t.Numero));
        Assert.True((await servicio.GuardarTelefonosAsync(["985 757 692"])).Exito);
        db.ChangeTracker.Clear();
        Assert.Equal([("985 757 692", 1)], (await db.TelefonosContacto.ToListAsync()).Select(t => (t.Numero, t.Orden)));
        Assert.Equal("Telefonos", (await servicio.GuardarTelefonosAsync(["", null])).Error!.Campo);
        Assert.False((await servicio.GuardarTelefonosAsync(["600000001", "600000002", "600000003", "600000004", "600000005"])).Exito);
        Assert.False((await servicio.GuardarTelefonosAsync(["659 306 659", "no es un teléfono"])).Exito);
        db.ChangeTracker.Clear();
        Assert.Single(await db.TelefonosContacto.ToListAsync());
    }
}
