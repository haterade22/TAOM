// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
namespace TAOM.Features.MapViewRelease;

/// <summary>
/// Counts the covers of the campaign map, says which one releases and counts the releases that ran
/// (docs/features/map-view-release.md). Pure: no engine call. The interval is passed on every cover, so a live MCM edit applies at the next one. Main thread only.
/// </summary>
internal sealed class MapViewReleaseCounter
{
    internal const int MinInterval = 1;
    internal const int MaxInterval = 1000;
    internal const int DefaultInterval = 20;

    private int _sinceRelease;

    internal int Covers { get; private set; }

    /// <summary>Releases that ran: <see cref="Released"/> counts them, not <see cref="Cover"/>, because a due release can be dropped before the map returns.</summary>
    internal int Releases { get; private set; }

    /// <summary>The interval limited to 1 to 1000.</summary>
    internal static int ClampInterval(int raw) =>
        raw < MinInterval ? MinInterval : raw > MaxInterval ? MaxInterval : raw;

    /// <summary>Counts one cover; true when this one is due a release of the map view (every Nth since the last due cover).</summary>
    internal bool Cover(int interval)
    {
        Covers++;
        _sinceRelease++;
        if (_sinceRelease < ClampInterval(interval)) return false;

        _sinceRelease = 0;
        return true;
    }

    /// <summary>A due release has run.</summary>
    internal void Released() => Releases++;
}
