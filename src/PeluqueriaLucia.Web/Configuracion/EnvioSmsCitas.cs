using PeluqueriaLucia.Application.Agenda;

namespace PeluqueriaLucia.Web.Configuracion;

internal sealed class EnvioSmsCitas(IServiceScopeFactory scopes, ILogger<EnvioSmsCitas> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (Environment.GetEnvironmentVariable("PELUQUERIA_SMS_ACTIVO") != "true") continue;
            try { await using var scope=scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ISmsCitas>().ProcesarColaAsync(stoppingToken); }
            catch (OperationCanceledException) when(stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex,"No se ha podido procesar la cola de avisos de citas."); }
        }
    }
}
