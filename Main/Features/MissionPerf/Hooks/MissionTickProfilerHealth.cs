using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics.Hooks;

namespace TAOM.Features.MissionPerf.Hooks;

/// <summary>
/// Whether the hooks the Patch97 tick profiler needs are in place now. <see cref="MissionTickProfilerHooks.Installed"/>
/// is a startup fact (the category applied and both <c>Mission.OnTick</c> calls were swapped); Harmony patches are not:
/// another mod's transpiler can take an anchor (Patch97's rewrite then finds none and lowers
/// <see cref="MissionTickProfilerHooks.OnTickSites"/>), and PatchShield strips every unprotected owner's patches on a
/// method it rescues, TAOM's own included: the strip removes Patch97's transpiler, so nothing rewrites
/// <see cref="MissionTickProfilerHooks.OnTickSites"/> afterwards. Required: the <c>Mission.OnTick</c> swaps
/// (behaviour time), the <c>Mission.OnPreTick</c> frame-boundary prefix (no frame closes without it) and Patch91's
/// agent-tick bracket (<c>agentTickMs</c>); the <c>OnPreTick</c> transpiler is not, because its loss only zeroes
/// <c>waitTickMs</c> and <c>preTickMs</c> and the header shows its count. A mission reads all of it at its start
/// (<see cref="Problems"/>). While it measures, every tick re-reads only the site count (<see cref="StopIfSitesLost"/>):
/// reading patch info deserializes it on every call and resolves each patch's method by scanning the loaded assemblies,
/// which is cheap at a mission start and not something to do on the clock of the measurement being taken.
/// </summary>
internal static class MissionTickProfilerHealth
{
    /// <summary>The behaviour calls <c>Mission.OnTick</c> must have swapped: <c>OnPreDisplayMissionTick</c> and
    /// <c>OnMissionTick</c>. The install and every later check use the same count.</summary>
    internal const int ExpectedOnTickSites = 2;

    /// <summary>The required hook that closes every frame, in both modes: Patch98's <c>Mission.OnPreTick</c> prefix.</summary>
    internal const string FrameBoundaryHook = "Mission.OnPreTick frame-boundary prefix (Patch98)";

    /// <summary>Reads the patches on a method; tests replace it.</summary>
    internal static Func<MethodBase, Patches?> PatchInfo = Harmony.GetPatchInfo;

    private static IReadOnlyList<RequiredHook>? _required;

    internal static IReadOnlyList<RequiredHook> RequiredHooks() => _required ??= new[]
    {
        RequiredHook.Of("Mission.OnTick transpiler (Patch97)",
            typeof(Mission_OnTick_TickProfiler_Patch), nameof(Mission_OnTick_TickProfiler_Patch.Transpiler), HarmonyPatchType.Transpiler),
        RequiredHook.Of(FrameBoundaryHook,
            typeof(Mission_OnPreTick_HitchProbe_Patch), nameof(Mission_OnPreTick_HitchProbe_Patch.Prefix), HarmonyPatchType.Prefix),
        RequiredHook.Of("Mission.TickAgentsAndTeamsImp prefix (Patch91)",
            typeof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch), nameof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch.Prefix), HarmonyPatchType.Prefix),
        RequiredHook.Of("Mission.TickAgentsAndTeamsImp finalizer (Patch91)",
            typeof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch), nameof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch.Finalizer), HarmonyPatchType.Finalizer),
    };

    /// <summary>What is wrong with the hooks right now; empty when every one is in place. A hook that cannot be
    /// checked reads as a problem (<see cref="HookHealth.Missing"/> never throws), so a mission never measures on a
    /// hook it could not confirm.</summary>
    internal static string[] Problems()
    {
        var problems = new List<string>(5);
        var sites = MissionTickProfilerHooks.OnTickSites;
        if (sites != ExpectedOnTickSites)
            problems.Add(SitesProblem(sites));
        problems.AddRange(HookHealth.Missing(RequiredHooks(), PatchInfo));
        return problems.ToArray();
    }

    /// <summary>Whether <paramref name="problems"/> names the frame-boundary prefix, missing or unreadable. No frame
    /// closes without it, so then the hitch probe measures nothing either.</summary>
    internal static bool FrameBoundaryMissing(IReadOnlyList<string> problems)
    {
        foreach (var problem in problems)
        {
            if (problem.StartsWith(FrameBoundaryHook, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>Called on every tick of a measuring mission. A rewrite that found fewer anchors than the swaps need
    /// (Patch97's own warning says no mission is measured) has left <c>Mission.OnTick</c> without its behaviour
    /// timing, so measuring stops for <paramref name="generation"/> with one warning.</summary>
    internal static void StopIfSitesLost(IModLogger logger, int generation, int missionNumber, double tSeconds)
    {
        var sites = MissionTickProfilerHooks.OnTickSites;
        if (sites == ExpectedOnTickSites)
            return;
        MissionTickProfilerHooks.EndMission(generation);
        logger.LogWarning(TickProfileLines.BuildHooksLostLine(missionNumber, tSeconds, SitesProblem(sites)));
    }

    private static string SitesProblem(int sites) =>
        string.Format(CultureInfo.InvariantCulture, "Mission.OnTick call sites {0}/{1}", sites, ExpectedOnTickSites);
}
