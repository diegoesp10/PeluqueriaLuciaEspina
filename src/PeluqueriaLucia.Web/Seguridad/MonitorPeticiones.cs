using System.Collections.Concurrent;

namespace PeluqueriaLucia.Web.Seguridad;

/// <summary>Peticiones de una IP en la última hora, para la pantalla de seguridad del panel.</summary>
public sealed record ActividadIp(string Ip, int Peticiones, int Rechazadas, int AlPanel, DateTime UltimaVez);

/// <summary>
/// Cuenta en memoria las peticiones por IP, por minutos, durante la última hora. Tiene un tope de IPs para que un
/// ataque con muchas direcciones no pueda llenar la memoria. Se reinicia con la aplicación.
/// </summary>
public sealed class MonitorPeticiones(TimeProvider reloj)
{
    private const int MaximoIps = 2000;
    private static readonly TimeSpan Ventana = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, Contador> _porIp = new();

    public void Anotar(string ip, bool alPanel) => ContadorDe(ip)?.Sumar(Minuto(), alPanel, rechazada: false);

    public void AnotarRechazo(string ip) => ContadorDe(ip)?.Sumar(Minuto(), alPanel: false, rechazada: true);

    public IReadOnlyList<ActividadIp> Resumen()
    {
        var desde = Minuto() - (long)Ventana.TotalMinutes;
        return _porIp
            .Select(par => par.Value.Resumir(par.Key, desde))
            .Where(a => a.Peticiones + a.Rechazadas > 0)
            .OrderByDescending(a => a.Rechazadas).ThenByDescending(a => a.Peticiones)
            .ToList();
    }

    private Contador? ContadorDe(string ip)
    {
        if (_porIp.TryGetValue(ip, out var contador))
            return contador;
        lock (_porIp)
        {
            if (_porIp.TryGetValue(ip, out contador)) return contador;
            if (_porIp.Count >= MaximoIps) Limpiar();
            return _porIp.Count >= MaximoIps ? null : _porIp.GetOrAdd(ip, _ => new Contador());
        }
    }

    private void Limpiar()
    {
        var desde = Minuto() - (long)Ventana.TotalMinutes;
        foreach (var (ip, contador) in _porIp)
        {
            if (contador.UltimoMinuto < desde)
                _porIp.TryRemove(ip, out _);
        }
    }

    private long Minuto() => reloj.GetUtcNow().ToUnixTimeSeconds() / 60;

    private sealed class Contador
    {
        private readonly Dictionary<long, (int Peticiones, int Rechazadas, int AlPanel)> _minutos = [];

        public long UltimoMinuto { get; private set; }

        public void Sumar(long minuto, bool alPanel, bool rechazada)
        {
            lock (_minutos)
            {
                _minutos.TryGetValue(minuto, out var actual);
                _minutos[minuto] = rechazada
                    ? actual with { Rechazadas = actual.Rechazadas + 1 }
                    : (actual.Peticiones + 1, actual.Rechazadas, actual.AlPanel + (alPanel ? 1 : 0));
                UltimoMinuto = minuto;
                foreach (var viejo in _minutos.Keys.Where(m => m < minuto - (long)Ventana.TotalMinutes).ToList())
                    _minutos.Remove(viejo);
            }
        }

        public ActividadIp Resumir(string ip, long desde)
        {
            lock (_minutos)
            {
                var recientes = _minutos.Where(m => m.Key >= desde).Select(m => m.Value).ToList();
                return new ActividadIp(
                    ip,
                    recientes.Sum(r => r.Peticiones),
                    recientes.Sum(r => r.Rechazadas),
                    recientes.Sum(r => r.AlPanel),
                    DateTimeOffset.FromUnixTimeSeconds(UltimoMinuto * 60).UtcDateTime);
            }
        }
    }
}
