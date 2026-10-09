// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System.Globalization;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>The skeleton buffer feature's log lines, built in one place so the tests pin their wording.</summary>
internal static class SkeletonBufferLines
{
    internal const string Tag = "[SkeletonBuffer]";

    internal const string NoReadings = Tag + " mission ended with no readings of the skeleton buffer";

    internal const string NoGuard = "no guard installed";

    internal const string ForeignGuard = "guarded by another module";

    internal const string OverflowUnreadable = "guard overflow count unreadable";

    private const string OverNinety = ", over 90 %";

    internal static string GuardOn(int pool, long siteRva, long resumeRva, long cave, long counter, double scanMs) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} guard ON pool {1}: site=0x{2:X} resume=0x{3:X} cave=0x{4:X} counter=0x{5:X} scanMs={6:0.0}",
            Tag, pool, siteRva, resumeRva, cave, counter, scanMs);

    internal static string GuardOffPool(int pool, string reason) =>
        string.Format(CultureInfo.InvariantCulture, "{0} guard OFF pool {1}: {2}", Tag, pool, reason);

    internal static string GuardOff(string reason) => Tag + " guard OFF: " + reason;

    internal static string WatchOff() => Tag + " watch off for this mission (MCM Skeleton Buffer Watch)";

    internal static string Pool2Off(string reason) => Tag + " pool 2 watch OFF: " + reason + "; the watch reads pool 1 only";

    internal static string WatchStartFault(string exceptionType, string message) =>
        $"{Tag} the watch could not start for this mission: {exceptionType}: {message}";

    internal static string WatchReadFault(string exceptionType, string message) =>
        $"{Tag} one watch read failed ({exceptionType}: {message}); the watch goes on, and this is the only read failure logged for this mission";

    internal static string WatchEndFault(string exceptionType, string message) =>
        $"{Tag} the mission-end peak line could not be written: {exceptionType}: {message}";

    internal static string GuardOverflows(int count) =>
        string.Format(CultureInfo.InvariantCulture, "guard overflows this mission: {0}", count);

    /// <summary>
    /// The mission-end line: pool 1's peak and its guard note, then, when pool 2 was read, its peak and its own guard note
    /// (<paramref name="pool2GuardNote"/> is null when pool 2 is not watched).
    /// </summary>
    internal static string Peak(SkeletonBufferWatchState state, string guardNote, string? pool2GuardNote) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} mission peak: {1} of {2} entries ({3:0.0} %){4}, about {5} skeletons, {6} agents, {7:0.0} s into the mission; {8}{9}",
            Tag, state.PeakFill, SkeletonBufferWatchState.Capacity, state.PeakPercent, state.Pool1OverNinety ? OverNinety : "",
            state.PeakSkeletons, state.PeakAgents, state.PeakSeconds, guardNote, Pool2Peak(state, pool2GuardNote));

    /// <summary>The second pool's peak clause with its leading "; " separator and its guard note; empty when pool 2 was not read.</summary>
    private static string Pool2Peak(SkeletonBufferWatchState state, string? pool2GuardNote) =>
        !state.Pool2HasSamples ? "" : string.Format(CultureInfo.InvariantCulture,
            "; pool 2 peak: {0} of {1} entries ({2:0.0} %){3}{4}",
            state.Pool2PeakFill, SkeletonBufferWatchState.Pool2Capacity, state.Pool2PeakPercent,
            state.Pool2OverNinety ? OverNinety : "", pool2GuardNote == null ? "" : "; " + pool2GuardNote);
}
