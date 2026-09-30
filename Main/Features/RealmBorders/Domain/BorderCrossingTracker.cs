using System.Collections.Generic;

namespace TAOM.Features.RealmBorders.Domain;

/// <summary>
/// Decides when "you enter the lands of X" is news. The first position after a reset is silent (a
/// save load is not a crossing), wild land is never announced, and each realm is announced at most
/// once per cooldown, so riding along a border does not repeat itself every hour.
/// </summary>
public sealed class BorderCrossingTracker
{
    private readonly double _cooldownHours;
    private readonly Dictionary<string, double> _lastAnnounced = new Dictionary<string, double>();
    private bool _started;
    private string? _current;

    public BorderCrossingTracker(double cooldownHours)
    {
        _cooldownHours = cooldownHours;
    }

    /// <summary>The realm to announce for this observation, or null.</summary>
    public string? Observe(string? realmHere, double nowHours)
    {
        if (!_started)
        {
            _started = true;
            _current = realmHere;
            if (realmHere != null)
                _lastAnnounced[realmHere] = nowHours;
            return null;
        }

        if (realmHere == _current)
            return null;
        _current = realmHere;
        if (realmHere == null)
            return null;

        if (_lastAnnounced.TryGetValue(realmHere, out double last) && nowHours - last < _cooldownHours)
            return null;
        _lastAnnounced[realmHere] = nowHours;
        return realmHere;
    }

    public void Reset()
    {
        _started = false;
        _current = null;
        _lastAnnounced.Clear();
    }
}
