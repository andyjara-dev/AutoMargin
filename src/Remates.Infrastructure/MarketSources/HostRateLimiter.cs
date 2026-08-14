using System.Collections.Concurrent;

namespace Remates.Infrastructure.MarketSources;

/// <summary>
/// Garantiza un intervalo mínimo entre peticiones a un mismo host.
///
/// No es una optimización: es la diferencia entre consultar un sitio y castigarlo. Un puñado
/// de peticiones espaciadas pasa desapercibido; una ráfaga se ve como un ataque y termina con
/// la IP bloqueada, que además dejaría al sistema sin la fuente.
/// </summary>
public sealed class HostRateLimiter(TimeProvider timeProvider)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastRequest = new();

    /// <summary>Espera lo necesario para respetar el intervalo y marca el turno como usado.</summary>
    public async Task WaitTurnAsync(string host, TimeSpan minimumInterval, CancellationToken ct)
    {
        var gate = _gates.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(ct);
        try
        {
            if (_lastRequest.TryGetValue(host, out var last))
            {
                var elapsed = timeProvider.GetUtcNow() - last;
                var remaining = minimumInterval - elapsed;

                if (remaining > TimeSpan.Zero)
                    await Task.Delay(remaining, timeProvider, ct);
            }

            _lastRequest[host] = timeProvider.GetUtcNow();
        }
        finally
        {
            gate.Release();
        }
    }
}
