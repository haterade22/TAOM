// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Features.MissionPerf;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// Patch103 (docs/reference/harmony-patch-registry.md "Patch103_MissionStartGuard"). One transpiler swaps the six start
/// calls in <c>Mission.AfterStart</c> for <see cref="MissionStartGuardCalls"/> helpers (a try with an exception filter);
/// a void prefix opens the mission's counts and hands the finalizer "I ran" through Harmony's <c>__state</c>; a void
/// finalizer observes what still escapes, checks that flag and the live swap count (the lost-guard warning) and writes
/// the summary. The finalizer is void so Harmony keeps <c>rethrow</c> and the engine's stack. The flag rides in
/// <c>__state</c> because Harmony reruns every finalizer for the same call when a later one throws; a process-wide flag
/// the first run cleared would read false on the rerun. <see cref="Cleanup"/> zeroes the swap count when the apply
/// fails after the transpiler ran. Nothing here changes a call that does not throw.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.AfterStart))]
[HarmonyPatchCategory(MissionStartGuardModule.PatchCategory)]
public static class Mission_AfterStart_MissionStartGuard_Patch
{
    // Priority.First, as Patch98 does: ahead of every prefix at a lower priority; a foreign prefix at First that registered
    // earlier, one at a higher raw priority, or one ordered before it with HarmonyBefore still runs first.
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(object __instance, out bool __state)
    {
        __state = true;
        try { MissionStartGuardCalls.Service?.BeginMission(__instance); }
        catch { /* BeginMission never throws; this guards the call itself */ }
    }

    // The soft-failing swap of the profiler patches (TickProfilerTranspiler): a site not found exactly once leaves the
    // whole method vanilla. No logger is passed: the install line in MissionStartGuardModule.OnPhase names the count.
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MissionStartGuardSwaps.Build(), "Mission.AfterStart", null, out var swapped);
        MissionStartGuardSwaps.LastSwapped = swapped;
        return result;
    }

    // Priority.First: ahead of PatchShield's finalizer (400) so a swallow there cannot hide the throw from this log.
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Exception? __exception, bool __state)
    {
        try
        {
            var service = MissionStartGuardCalls.Service;
            if (__exception != null)
                service?.ReportEscaped(__exception);
            service?.EndMissionStart(__state, MissionStartGuardSwaps.LastSwapped);
        }
        catch { /* observe-only: a log line must never change the engine's outcome */ }
    }

    // Harmony registers nothing when the apply fails after the transpiler ran, but the transpiler already stored its
    // count: without this the install line would claim six wrapped sites. Void, so Harmony's exception is untouched.
    [HarmonyCleanup]
    public static void Cleanup(Exception? ex)
    {
        if (ex != null) MissionStartGuardSwaps.LastSwapped = 0;
    }
}
