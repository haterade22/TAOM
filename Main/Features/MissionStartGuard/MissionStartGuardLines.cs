// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using TAOM.Features.MissionStartGuard.Models;

namespace TAOM.Features.MissionStartGuard;

/// <summary>The guard's log lines, built in one place so the tests pin their wording.</summary>
internal static class MissionStartGuardLines
{
    internal const string Tag = "[MissionStartGuard]";

    internal static string BuildInstallLine(int wrappedSites, bool toggleOn) =>
        $"{Tag} ON: {wrappedSites} call sites wrapped in Mission.AfterStart (toggle {(toggleOn ? "on" : "off")})";

    internal static string BuildSoftFailWarning(int wrappedSites, int expectedSites) =>
        $"{Tag} OFF: wrapped {wrappedSites} of {expectedSites} call sites in Mission.AfterStart, so it stays vanilla "
        + "(the engine loads the mission again every frame when a start call throws)";

    internal static string BuildCaughtLine(StartCall call, string ownerType, string ownerAssembly, Exception exception) =>
        $"{Tag} {call} threw in {ownerType} (assembly {ownerAssembly}); the mission start goes on without it: {exception}";

    internal static string BuildSuppressedWarning(int fullLogCap) =>
        $"{Tag} more than {fullLogCap} start calls threw in this mission; the rest are counted, not logged";

    internal static string BuildPrefixLostWarning() =>
        $"{Tag} the guard's prefix did not run before this mission start, so Patch103 may have been stripped or skipped "
        + "(another mod's patch protection removes a mod's prefix and transpiler but not its finalizers); the start calls are "
        + "probably no longer wrapped, and a throwing one makes the engine load the mission again every frame. Logged once per process";

    internal static string BuildSwapsLostWarning(int liveSwaps, int expectedSites) =>
        $"{Tag} only {liveSwaps} of {expectedSites} start calls are wrapped in Mission.AfterStart now (a patch applied after TAOM's "
        + "install rebuilt the method and the swap failed); a throwing start call can make the engine load the mission again every frame. "
        + "Logged once per process";

    internal static string BuildSummaryLine(int caught, int notices) =>
        $"{Tag} mission start survived {caught} failed call(s); {notices} shown on screen (details above)";

    internal static string BuildEscapedLine(Exception exception, int count, int cap) =>
        $"{Tag} Mission.AfterStart threw and was not survived (a call the guard does not wrap, or the toggle is off; {count} of {cap} logged per process); "
        + $"the engine will load the mission again: {exception}";
}
