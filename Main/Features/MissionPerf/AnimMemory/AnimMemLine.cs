using System;
using System.Globalization;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// Formats every <c>[AnimMem]</c> line the animation clip memory probe writes to the TAOM debug log.
/// KB is bytes / 1024 (floor); a percentage of the budget is floored and may exceed 100, because the
/// engine lets the total pass its budget before the eviction pass trims it.
/// </summary>
internal static class AnimMemLine
{
    internal static string Armed(long moduleBase, int textRva, int textSize, int loadSiteRva, int budgetSiteRva,
        int counterRva, int budgetRva, int budgetBytes, double scanMs) =>
        string.Format(CultureInfo.InvariantCulture,
            "[AnimMem] armed: {0} base=0x{1:X} text=0x{2:X}+0x{3:X} loadSite=0x{4:X} budgetSite=0x{5:X} counter=0x{6:X} budget=0x{7:X} budgetBytes={8} scanMs={9:0.0}",
            ClipBudgetSignature.ModuleName, moduleBase, textRva, textSize, loadSiteRva, budgetSiteRva,
            counterRva, budgetRva, budgetBytes, scanMs);

    internal static string Disabled(string reason) =>
        "[AnimMem] disabled for this process: " + reason
        + ". No further [AnimMem] samples will be taken; [MissionPerf] is unaffected.";

    internal static string OffForMission() =>
        "[AnimMem] off for this mission: 'Enable Animation Clip Memory Probe' is off (Battle Load Diagnostics page).";

    internal static string MissionStart(int startBytes, int budgetBytes, bool loadingNow) =>
        string.Format(CultureInfo.InvariantCulture,
            "[AnimMem] mission start: sample every 1 s, line every 5 s, startKB={0} budgetKB={1} pctOfBudget={2} loadingNow={3}",
            Kb(startBytes), Kb(budgetBytes), Pct(startBytes, budgetBytes), loadingNow ? 1 : 0);

    internal static string Periodic(double tSeconds, int loadedBytes, int budgetBytes, bool loadingNow, int drops,
        int minBytes, int maxBytes, int loadingSamples, int samples) =>
        string.Format(CultureInfo.InvariantCulture,
            "[AnimMem] t=+{0:0}s loadedKB={1} budgetKB={2} pctOfBudget={3} loadingNow={4} drops={5} minKB={6} maxKB={7} loadingSamples={8}/{9}",
            tSeconds, Kb(loadedBytes), Kb(budgetBytes), Pct(loadedBytes, budgetBytes), loadingNow ? 1 : 0, drops,
            Kb(minBytes), Kb(maxBytes), loadingSamples, samples);

    internal static string Summary(double tSeconds, int samples, int startBytes, int endBytes, int peakBytes,
        int budgetBytes, int samplesAtOrAbove90Pct, int drops, int loadingSamples, bool stopped) =>
        string.Format(CultureInfo.InvariantCulture,
            "[AnimMem] summary: t=+{0:0}s samples={1} startKB={2} endKB={3} peakKB={4} peakPct={5} samplesAtOrAbove90Pct={6} drops={7} loadingSamples={8} stopped={9}",
            tSeconds, samples, Kb(startBytes), Kb(endBytes), Kb(peakBytes), Pct(peakBytes, budgetBytes),
            samplesAtOrAbove90Pct, drops, loadingSamples, stopped ? 1 : 0);

    internal static string Stopped(Exception ex) =>
        "[AnimMem] stopped for this mission after " + ex.GetType().Name + ": " + ex.Message;

    private static long Kb(int bytes) => bytes / 1024L;

    private static int Pct(int bytes, int budgetBytes) =>
        budgetBytes > 0 ? (int)(bytes * 100L / budgetBytes) : 0;
}
