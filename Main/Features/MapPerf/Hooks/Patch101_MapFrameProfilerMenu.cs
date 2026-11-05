using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu;
using TAOM.Features.Enlistment;
using TAOM.Features.MenuLinkColors.Hooks;

namespace TAOM.Features.MapPerf.Hooks;

// Patch101 menu probes (#1019; default off, applied once per process by MapFrameProfilerInstaller and only when the
// toggle is on, in a category of their own so a failing probe cannot stop the frame boundary or the views). Three
// brackets that settle the owed timing questions on the service wait menu (docs/features/map-perf-profiler.md):
//  - GameMenuVM.OnFrameTick(): the vanilla per-frame menu tick, keyed GameMenuVM. Not TAOM-owned.
//  - the GameMenuVM.ContextText setter: Patch64's prefix runs inside this bracket, so the entry is Patch64's
//    per-frame rewrite plus the setter. Keyed Patch64_MenuLinkColors, TAOM-owned.
//  - ServiceStatusService.RefreshIfChanged(): the status board refresh, keyed ServiceStatusService. Not TAOM-owned:
//    a TAOM tick listener already calls it under its own entry, so counting it would add it twice.
// The three entries are nested (OnFrameTick contains the setter), so they do not add up. Targets come from
// MapProfilerTargets.MenuTargets(). Main thread.

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.MenuCategory)]
public static class GameMenuVM_OnFrameTick_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => MapProfilerTargets.MenuTargets()[0];

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeStamp __state) => __state = MapFrameProfilerHooks.BeginProbe();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(ProbeStamp __state) =>
        MapFrameProfilerHooks.RecordProbe(typeof(GameMenuVM), __state, taomOwned: false);
}

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.MenuCategory)]
public static class GameMenuVM_ContextText_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => MapProfilerTargets.MenuTargets()[1];

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeStamp __state) => __state = MapFrameProfilerHooks.BeginProbe();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(ProbeStamp __state) =>
        MapFrameProfilerHooks.RecordProbe(typeof(Patch64_MenuLinkColors), __state, taomOwned: true);
}

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.MenuCategory)]
public static class ServiceStatusService_RefreshIfChanged_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => MapProfilerTargets.MenuTargets()[2];

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeStamp __state) => __state = MapFrameProfilerHooks.BeginProbe();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(ProbeStamp __state) =>
        MapFrameProfilerHooks.RecordProbe(typeof(ServiceStatusService), __state, taomOwned: false);
}
