using System;

namespace TAOM.Features.CreatureSiegeRole.Domain;

/// <summary>
/// A token bucket for the per-creature and per-blow diagnostic lines: a burst up front (the first few lines of a spike are
/// the ones Mike reads), then a steady trickle, and a count of the lines it swallowed that the next line reports. Pure: the
/// caller passes the clock. A clock that is not finite refills nothing and never throws, and one that runs backwards does
/// not take tokens away. Thread-safe, because the gate-blow line is raised from whichever thread native picks for a hit.
/// </summary>
public sealed class SiegeLogThrottle
{
    private readonly object _lock = new();
    private readonly double _burst;
    private readonly double _refillPerSecond;
    private double _tokens;
    private double _last = double.NaN;
    private int _suppressed;

    public SiegeLogThrottle(int burst, double refillPerSecond)
    {
        _burst = Math.Max(1, burst);
        _refillPerSecond = double.IsNaN(refillPerSecond) ? 0.0 : Math.Max(0.0, refillPerSecond);
        _tokens = _burst;
    }

    /// <summary>
    /// True when a line may be written at <paramref name="nowSeconds"/>. <paramref name="suppressed"/> is how many lines were
    /// refused since the last one that passed (0 when this one is refused), and is reset by this call.
    /// </summary>
    public bool TryAcquire(double nowSeconds, out int suppressed)
    {
        lock (_lock)
        {
            Refill(nowSeconds);

            if (_tokens >= 1.0)
            {
                _tokens -= 1.0;
                suppressed = _suppressed;
                _suppressed = 0;
                return true;
            }

            _suppressed++;
            suppressed = 0;
            return false;
        }
    }

    private void Refill(double now)
    {
        if (double.IsNaN(now) || double.IsInfinity(now)) return;

        if (double.IsNaN(_last))
        {
            _last = now;
            return;
        }

        if (now <= _last) return;

        _tokens = Math.Min(_burst, _tokens + (now - _last) * _refillPerSecond);
        _last = now;
    }
}
