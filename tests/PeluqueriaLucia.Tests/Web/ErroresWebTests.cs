using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using PeluqueriaLucia.Tests.Integracion;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Tests.Web;

public sealed class ErroresWebTests
{
    [Fact]
    public async Task Development_oculta_excepcion_y_muestra_error_generico()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        await using var fabrica = new FabricaWeb(bd.Cadena).WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.Configure<MvcOptions>(o => o.Filters.Add(new FalloPrueba()))));
        using var cliente = fabrica.CreateClient();
        var respuesta = await cliente.GetAsync("/");
        var html = await respuesta.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Contains("Algo ha fallado", html);
        Assert.DoesNotContain("SECRETO_DE_PRUEBA", html);
        Assert.DoesNotContain("Stack", html);
        Assert.Contains("no-store", respuesta.Headers.CacheControl!.ToString());
    }

    [Fact]
    public void Conexion_malformada_no_filtra_su_valor()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ServicioConexionSql.Validar("Server=localhost;Password=SECRETO_DE_PRUEBA;roto"));
        Assert.DoesNotContain("SECRETO_DE_PRUEBA", error.ToString());
    }
    private sealed class FalloPrueba : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context) => throw new InvalidOperationException("SECRETO_DE_PRUEBA");
        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
