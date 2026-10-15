using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PeluqueriaLucia.Application.Publico;
using PeluqueriaLucia.Domain.Publico;

namespace PeluqueriaLucia.Web.Publico;

public sealed record TelefonoPublico(string Numero, string Enlace)
{
    /// <summary>Con prefijo, para los datos estructurados de Google: «+34 659 306 659».</summary>
    public string Internacional => Numero.StartsWith('+') ? Numero : "+34 " + Numero;
}

/// <summary>
/// Teléfonos de contacto de la web (portada, menú, pie, privacidad) y de los avisos a clientes: los guardados en
/// «Contacto de la web» o, mientras no haya ninguno, el de contenido-publico.json. Se guardan 10 minutos en memoria
/// porque los pide cada página; al cambiarlos en el panel se invalidan al momento.
/// </summary>
public sealed class TelefonosPublicos(ContenidoService contenido, IOptions<ContenidoPublico> publico, IMemoryCache cache)
{
    private const string Clave = "telefonos-contacto";

    public async Task<IReadOnlyList<TelefonoPublico>> ListarAsync(CancellationToken ct = default) =>
        await cache.GetOrCreateAsync(Clave, async entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var numeros = (await contenido.TelefonosAsync(ct)).Select(t => t.Numero).ToList();
            if (numeros.Count == 0 && !string.IsNullOrWhiteSpace(publico.Value.Telefono)) numeros.Add(publico.Value.Telefono.Trim());
            return (IReadOnlyList<TelefonoPublico>)numeros.Select(n => new TelefonoPublico(n, TelefonoContacto.Enlace(n))).ToList();
        }) ?? [];

    public void Invalidar() => cache.Remove(Clave);
}
