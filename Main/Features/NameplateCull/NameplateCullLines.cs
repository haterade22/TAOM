// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using System;
using System.Globalization;

namespace TAOM.Features.NameplateCull;

/// <summary>The cull's log lines, built in one place so the tests pin their wording.</summary>
internal static class NameplateCullLines
{
    internal const string Tag = "[NameplateCull]";

    internal static string BuildInstallLine(bool toggleOn) =>
        toggleOn
            ? $"{Tag} ON: the campaign map skips hidden settlement nameplates (toggle on)"
            : $"{Tag} installed, toggle off: every settlement nameplate updates as in the vanilla game";

    internal static string BuildUnavailableLine(string reason) =>
        $"{Tag} OFF: {reason}; the vanilla nameplate update runs";

    /// <summary>One window of culled map frames: how many nameplate updates ran and how many were skipped in them.</summary>
    internal static string BuildWindowLine(long frames, long updated, long skipped)
    {
        var total = updated + skipped;
        var percent = total > 0 ? skipped * 100d / total : 0d;
        return string.Format(CultureInfo.InvariantCulture,
            "{0} {1} culled map frames: {2} nameplate updates run, {3} skipped ({4:0.0} percent)",
            Tag, frames, updated, skipped, percent);
    }

    internal static string BuildSwitchedOffLine(Exception exception) =>
        $"{Tag} OFF after an error, the vanilla nameplate update runs for the rest of this game launch: {exception}";
}
