// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), map-view-release.
using System;

namespace TAOM.Features.MapViewRelease;

/// <summary>The map-view release's log lines, built in one place so the tests pin their wording.</summary>
internal static class MapViewReleaseLines
{
    internal const string Tag = "[MapViewRelease]";

    internal static string BuildInstallLine(bool toggleOn, int interval)
    {
        if (!toggleOn)
            return $"{Tag} installed, toggle off: closed menus leave the map's render targets alone as in the vanilla game";

        var cadence = interval == 1 ? "on every cover" : $"once per {interval} covers";
        return $"{Tag} ON: the campaign map's view releases its render targets {cadence} (toggle on)";
    }

    internal static string BuildReleasedLine(int releases, int covers) =>
        $"{Tag} released {releases} times, map covered {covers} times";

    internal static string BuildSwitchedOffLine(Exception exception) =>
        $"{Tag} OFF after an error, the map view is no longer released for the rest of this game launch: {exception}";
}
