using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PeluqueriaLucia.Infrastructure.Persistencia;
using PeluqueriaLucia.Web.Seguridad;

namespace PeluqueriaLucia.Web.Configuracion;

/// <summary>
/// <c>/salud</c>: lo que comprueba el despliegue tras publicar. Responde 200 («OK») solo si la aplicación llega a su base
/// de datos y esta tiene todas las migraciones del código; si no, 503 con el motivo. Así, publicar el código antes que sus
/// migraciones, o una conexión rota (identidad, firewall, cadena), hace fallar el despliegue en lugar de descubrirse en
/// la primera pantalla. No da más detalles (ni servidor ni nombres de migraciones).
/// </summary>
public static class Salud
{
    public const string Ruta = "/salud";

    public static IServiceCollection AddSalud(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<SaludBaseDeDatos>("base-de-datos");
        return services;
    }

    public static WebApplication MapSalud(this WebApplication app)
    {
        app.MapHealthChecks(Ruta, new HealthCheckOptions { ResponseWriter = EscribirAsync })
            .AllowAnonymous()
            .RequireRateLimiting(LimitesDePeticiones.Publico);
        return app;
    }

    private static Task EscribirAsync(HttpContext contexto, HealthReport informe)
    {
        contexto.Response.ContentType = "text/plain; charset=utf-8";
        var texto = informe.Status == HealthStatus.Healthy
            ? "OK"
            : string.Join("; ", informe.Entries.Values.Where(e => e.Status != HealthStatus.Healthy).Select(e => e.Description));
        return contexto.Response.WriteAsync(texto);
    }
}

/// <summary>La base de datos responde y no le falta ninguna migración del código publicado.</summary>
internal sealed class SaludBaseDeDatos(PeluqueriaDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Sin conexión con la base de datos");
            var pendientes = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            return pendientes == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"A la base de datos le {(pendientes == 1 ? "falta 1 migración" : $"faltan {pendientes} migraciones")}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Sin conexión con la base de datos", ex);
        }
    }
}
