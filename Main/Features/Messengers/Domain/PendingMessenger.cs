using System.Globalization;

namespace TAOM.Features.Messengers.Domain;

public sealed class PendingMessenger
{
    public string TargetHeroId { get; }
    public double DispatchTimeDays { get; }
    public MapCoord Position { get; set; }
    public bool Arrived { get; set; }

    public PendingMessenger(string targetHeroId, double dispatchTimeDays, MapCoord position, bool arrived)
    {
        TargetHeroId = targetHeroId;
        DispatchTimeDays = dispatchTimeDays;
        Position = position;
        Arrived = arrived;
    }

    // Not saved, so the save format keeps its four fields: after a load the player is told once more.
    private bool _waitNoticeShown;

    /// <summary>True the first time only: the hourly tick tells the player once that this messenger waits for its
    /// target to be on the map again (#1133).</summary>
    public bool TryMarkWaitNoticeShown()
    {
        if (_waitNoticeShown)
            return false;
        _waitNoticeShown = true;
        return true;
    }

    public string Serialize()
    {
        var inv = CultureInfo.InvariantCulture;
        return $"{DispatchTimeDays.ToString("R", inv)}|{Position.X.ToString("R", inv)}|{Position.Y.ToString("R", inv)}|{(Arrived ? 1 : 0)}";
    }

    public static bool TryDeserialize(string heroId, string serialized, out PendingMessenger messenger)
    {
        messenger = null;
        if (string.IsNullOrEmpty(heroId) || string.IsNullOrEmpty(serialized))
            return false;

        var parts = serialized.Split('|');
        if (parts.Length != 4)
            return false;

        var inv = CultureInfo.InvariantCulture;
        // Codex review #34 round 2: NumberStyles.Float ACCEPTS "NaN", "Infinity", "-Infinity". A tampered or
        // corrupted save entry like `NaN|0|0|0` would parse, then elapsed-day comparisons (current - NaN)
        // never become true → the messenger sits as already-pending forever. Reject non-finite here so
        // malformed entries take the "drop with logged warning" path in the store.
        if (!double.TryParse(parts[0], NumberStyles.Float, inv, out var days)
            || double.IsNaN(days) || double.IsInfinity(days)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, inv, out var x)
            || float.IsNaN(x) || float.IsInfinity(x)) return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, inv, out var y)
            || float.IsNaN(y) || float.IsInfinity(y)) return false;
        if (parts[3] != "0" && parts[3] != "1") return false;

        messenger = new PendingMessenger(heroId, days, new MapCoord(x, y), parts[3] == "1");
        return true;
    }
}
