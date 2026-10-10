using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PeluqueriaLucia.Application;
using PeluqueriaLucia.Application.Seguridad;
using PeluqueriaLucia.Infrastructure;

namespace PeluqueriaLucia.Tests.Integracion;

public sealed class SeguridadTests
{
    [Fact]
    public async Task Bloqueo_caducidad_revocacion_y_cambio_de_contrasena_se_validan_en_servidor()
    {
        await using var bd = await BaseDeDatosPrueba.CrearAsync();
        var reloj = new RelojPrueba();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddAplicacion();
        servicios.AddInfraestructura(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = bd.Cadena }).Build());
        servicios.AddSingleton<TimeProvider>(reloj);
        await using var proveedor = servicios.BuildServiceProvider();
        async Task<T> Ejecutar<T>(Func<AccesoService, Task<T>> accion)
        {
            await using var scope = proveedor.CreateAsyncScope();
            return await accion(scope.ServiceProvider.GetRequiredService<AccesoService>());
        }
        const string original = "Una frase para pruebas 123!";
        var origen = new Origen("127.0.0.1", "Pruebas");
        Assert.True((await Ejecutar(s => s.EstablecerAdministradorAsync("lucia", original))).Exito);
        const string secreto = "JBSWY3DPEHPK3PXP";
        await using (var db = bd.Contexto()) {
            var administrador = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Administradores);
            administrador.ActivarMfa(proveedor.GetRequiredService<PeluqueriaLucia.Application.Abstracciones.IProtectorSecretos>().Proteger(secreto, "totp"), -1);
            await db.SaveChangesAsync();
        }
        string Codigo() => new OtpNet.Totp(OtpNet.Base32Encoding.ToBytes(secreto)).ComputeTotp(reloj.GetUtcNow().UtcDateTime);
        var errorInexistente = (await Ejecutar(s => s.IniciarSesionAsync("nadie", original, origen))).Error!.Mensaje;
        for (var i = 0; i < 5; i++)
            Assert.Equal(errorInexistente, (await Ejecutar(s => s.IniciarSesionAsync("lucia", "incorrecta", origen))).Error!.Mensaje);
        Assert.False((await Ejecutar(s => s.IniciarSesionAsync("lucia", original, origen))).Exito);
        reloj.Avanzar(TimeSpan.FromMinutes(16));
        var primera = (await Ejecutar(s => s.IniciarSesionAsync("lucia", original, origen, codigo: Codigo()))).Valor!;
        reloj.Avanzar(TimeSpan.FromSeconds(31));
        var segunda = (await Ejecutar(s => s.IniciarSesionAsync("lucia", original, origen, codigo: Codigo()))).Valor!;
        Assert.NotEqual(primera.Token, segunda.Token);
        Assert.Null(await Ejecutar(s => s.ValidarSesionAsync(primera.SesionId, segunda.Token, origen.Ip)));
        Assert.NotNull(await Ejecutar(s => s.ValidarSesionAsync(primera.SesionId, primera.Token, origen.Ip)));
        Assert.False((await Ejecutar(s => s.CerrarSesionDeAsync(primera.AdministradorId + 1, primera.SesionId))).Exito);
        reloj.Avanzar(TimeSpan.FromSeconds(31));
        Assert.True((await Ejecutar(s => s.CambiarContrasenaAsync(primera.AdministradorId, primera.SesionId, original, "Otra frase completamente nueva 456!", codigo: Codigo()))).Exito);
        Assert.Null(await Ejecutar(s => s.ValidarSesionAsync(segunda.SesionId, segunda.Token, origen.Ip)));
        Assert.NotNull(await Ejecutar(s => s.ValidarSesionAsync(primera.SesionId, primera.Token, origen.Ip)));
        reloj.Avanzar(TimeSpan.FromHours(12));
        Assert.Null(await Ejecutar(s => s.ValidarSesionAsync(primera.SesionId, primera.Token, origen.Ip)));
    }

    private sealed class RelojPrueba : TimeProvider
    {
        private DateTimeOffset ahora = new(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => ahora;
        internal void Avanzar(TimeSpan tiempo) => ahora += tiempo;
    }
}
