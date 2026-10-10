using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Infrastructure.Persistencia;

namespace PeluqueriaLucia.Web.Configuracion;

public sealed class OpcionesBaseDeDatos
{
    public const string Seccion = "BaseDeDatos";

    /// <summary>Aplica las migraciones pendientes al arrancar. Por defecto no: se aplican con «dotnet ef database update».</summary>
    public bool AplicarMigracionesAlArrancar { get; set; }
}

/// <summary>
/// Se ejecuta antes de que la aplicación empiece a atender peticiones: construye el modelo de EF Core y abre la primera
/// conexión, que es lo más lento de la primera carga. Así la primera pantalla del día no tiene que esperar por ello.
/// </summary>
internal sealed class ArranqueBaseDeDatos(
    IServiceScopeFactory scopes,
    IOptions<OpcionesBaseDeDatos> opciones,
    ILogger<ArranqueBaseDeDatos> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var cronometro = Stopwatch.StartNew();
        await using var scope = scopes.CreateAsyncScope();
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<PeluqueriaDbContext>();
            if (opciones.Value.AplicarMigracionesAlArrancar)
                await db.Database.MigrateAsync(cancellationToken);
            await db.Visitas.AsNoTracking().Include(v => v.Cliente).Include(v => v.Lineas).OrderBy(v => v.Id).Take(1).ToListAsync(cancellationToken);
            logger.LogInformation("Base de datos preparada en {Milisegundos} ms", cronometro.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Preparar la conexión es solo una optimización: si la base de datos no responde, la aplicación arranca igual
            // y la página de error lo explicará en la primera petición
            logger.LogWarning(ex, "No se ha podido preparar la base de datos al arrancar");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
