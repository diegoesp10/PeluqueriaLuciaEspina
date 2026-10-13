using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Tests.Integracion;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Tests.Web;

/// <summary>Piezas para publicar en Azure App Service detrás de Cloudflare.</summary>
public sealed class AzureTests
{
    [Fact]
    public async Task La_ip_del_visitante_sale_de_la_cabecera_de_cloudflare_si_se_configura()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena, new() { ["Seguridad:CabeceraIpCliente"] = "CF-Connecting-IP" });
        using var cliente = fabrica.Cliente();

        using var conCabecera = new HttpRequestMessage(HttpMethod.Get, "/servicios");
        conCabecera.Headers.Add("CF-Connecting-IP", "203.0.113.7");
        (await cliente.SendAsync(conCabecera)).EnsureSuccessStatusCode();

        using var falsa = new HttpRequestMessage(HttpMethod.Get, "/servicios");
        falsa.Headers.Add("CF-Connecting-IP", "no-es-una-ip");
        (await cliente.SendAsync(falsa)).EnsureSuccessStatusCode();

        var ips = fabrica.Services.GetRequiredService<MonitorPeticiones>().Resumen().Select(a => a.Ip).ToList();
        Assert.Contains("203.0.113.7", ips);
        Assert.DoesNotContain("no-es-una-ip", ips);
    }

    [Fact]
    public async Task Sin_configurar_la_cabecera_no_se_hace_caso_de_ella()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena);
        using var cliente = fabrica.Cliente();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/servicios");
        peticion.Headers.Add("CF-Connecting-IP", "198.51.100.9");
        (await cliente.SendAsync(peticion)).EnsureSuccessStatusCode();
        Assert.DoesNotContain("198.51.100.9", fabrica.Services.GetRequiredService<MonitorPeticiones>().Resumen().Select(a => a.Ip));
    }

    [Fact]
    public void La_cadena_de_conexion_de_azure_tiene_preferencia_y_se_valida()
    {
        const string variable = "ConnectionStrings__Peluqueria";
        var anterior = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable,
                "Server=tcp:sql-lucia-peluqueria.database.windows.net,1433;Initial Catalog=PeluqueriaLuciaProd;Encrypt=True;Authentication=Active Directory Managed Identity;");
            var cadena = ServicioConexionSql.Obtener();
            Assert.Contains("sql-lucia-peluqueria.database.windows.net", cadena);
            Assert.Contains("PeluqueriaLuciaProd", cadena);

            Environment.SetEnvironmentVariable(variable, "Server=;Initial Catalog=");
            Assert.Throws<InvalidOperationException>(ServicioConexionSql.Obtener);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, anterior);
        }
    }
}
