using PeluqueriaLucia.Application.Agenda;

namespace PeluqueriaLucia.Web.Configuracion;

internal sealed class EnvioCorreosCitas(IServiceScopeFactory scopes, ILogger<EnvioCorreosCitas> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (Environment.GetEnvironmentVariable("PELUQUERIA_CORREO_ACTIVO") != "true") continue;
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<VerificacionContactoService>().ProcesarColaAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<IComunicacionesCitas>().ProcesarColaAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "No se ha podido procesar la cola de comunicaciones de citas."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
