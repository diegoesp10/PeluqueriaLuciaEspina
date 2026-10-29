using Microsoft.Extensions.Caching.Memory;
using PeluqueriaLucia.Application.Sorteos;

namespace PeluqueriaLucia.Web.Publico;

/// <summary>
/// Los anuncios de sorteos que la web enseña hoy (aviso flotante, menú y página de sorteos). Se guardan 5 minutos en memoria
/// porque los pide cada página; al cambiar un sorteo en el panel se invalidan al momento. Son un extra: si la base de datos
/// falla, la página sale sin ellos (también la de error, que usa la misma plantilla).
/// </summary>
public sealed class SorteosPublicos(SorteosService sorteos, IMemoryCache cache, ILogger<SorteosPublicos> logger)
{
    private const string Clave = "sorteos-anunciados";

    public async Task<IReadOnlyList<AnuncioPublico>> ListarAsync(CancellationToken ct = default)
    {
        try
        {
            return await cache.GetOrCreateAsync($"{Clave}-{sorteos.Hoy:yyyyMMdd}", async entrada =>
            {
                entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return await sorteos.AnunciosVisiblesAsync(ct);
            }) ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "No se han podido leer los sorteos anunciados; la página sale sin ellos.");
            return [];
        }
    }

    public void Invalidar() => cache.Remove($"{Clave}-{sorteos.Hoy:yyyyMMdd}");
}
