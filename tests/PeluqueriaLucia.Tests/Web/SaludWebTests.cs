using System.Net;
using Microsoft.Data.SqlClient;
using PeluqueriaLucia.Tests.Integracion;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>«/salud», lo que comprueba el despliegue tras publicar: conexión con la base de datos y migraciones al día.</summary>
public sealed class SaludWebTests(SeguridadWebFixture fixture) : IClassFixture<SeguridadWebFixture>
{
    [Fact]
    public async Task Con_la_base_al_dia_responde_OK_sin_sesion_y_sin_guardarse_en_cache()
    {
        using var cliente = fixture.Fabrica.Cliente();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("OK", await respuesta.Content.ReadAsStringAsync());
        Assert.True(respuesta.Headers.CacheControl?.NoStore, "Un proxy no debe guardar la respuesta");
    }

    [Fact]
    public async Task Si_a_la_base_le_falta_una_migracion_responde_503_con_el_motivo()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync("20261022083304_PreferenciaDeAviso");
        using var fabrica = new FabricaWeb(bd.Cadena);
        using var cliente = fabrica.Cliente();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("A la base de datos le falta 1 migración", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sin_su_base_de_datos_responde_503_sin_dar_detalles_de_la_conexion()
    {
        var inexistente = new SqlConnectionStringBuilder(fixture.Bd.Cadena) { InitialCatalog = $"PeluqueriaLucia_NoExiste_{Guid.NewGuid():N}" };
        using var fabrica = new FabricaWeb(inexistente.ConnectionString);
        using var cliente = fabrica.Cliente();

        var respuesta = await cliente.GetAsync("/salud");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("Sin conexión con la base de datos", await respuesta.Content.ReadAsStringAsync());
    }
}
