using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TAOM.Features.MissionPerf;

namespace TAOM.Features.MapPerf.Hooks;

// Patch101 (default off; applied once per process by MapFrameProfilerInstaller, never by name from
// SubModule.cs). Verified on the installed v1.5.3, every target on the main thread:
//   protected override void MapState.OnTick(float dt): virtual, 117 bytes, no exception clause; runs once per
//     application tick while the map state is active and not disabled, BEFORE every submodule's
//     OnApplicationTick (Module.OnApplicationTick). Its prefix is the frame boundary.
//   internal void Campaign.RealTick(float realDt): 161 bytes, one exception clause; called once from
//     MapState.OnMapModeTick. internal void Campaign.Tick(): 452 bytes, called once from the same method.
//     Both far too large to inline.
//   public override void CampaignEvents.Tick(float dt): 17 bytes, exactly
//     `call get_Instance; ldfld _tickEvent; ldarg.1; callvirt MbEvent<float>::Invoke(float); ret`.
//     The one callvirt becomes `call MapFrameProfilerHooks.TimedTickEvent(MbEvent<float>, float)`:
//     stack-identical, no inserted instruction, no branch (TickProfilerTranspiler soft-fails to vanilla).
//   protected override void MapScreen.OnFrameTick(float dt): the map views' OnMapScreenUpdate loop.
//   protected override void TAOM.SubModule.OnApplicationTick(float dt): TAOM's application tick, and the
//     continuity count (exactly one per closed frame).
// Prefixes run first (Priority.First) and postfixes last (Priority.Last), so each bracket encloses every
// other patch on its method. Prefix and postfix only, never a finalizer: no exception path changes.

[HarmonyPatch(typeof(MapState), "OnTick", new[] { typeof(float) })]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class MapState_OnTick_MapProfiler_Patch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state)
    {
        MapFrameBoundary.OnFrameBoundary();
        __state = MapFrameProfilerHooks.BeginPhase();
    }

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(long __state) => MapFrameProfilerHooks.EndPhase(MapPhase.MapState, __state);
}

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class Campaign_RealTick_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => AccessTools.Method(typeof(Campaign), "RealTick", new[] { typeof(float) });

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state) => __state = MapFrameProfilerHooks.BeginPhase();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(long __state) => MapFrameProfilerHooks.EndPhase(MapPhase.RealTick, __state);
}

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class Campaign_Tick_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => AccessTools.Method(typeof(Campaign), "Tick", Type.EmptyTypes);

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state) => __state = MapFrameProfilerHooks.BeginPhase();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(long __state) => MapFrameProfilerHooks.EndPhase(MapPhase.CampaignTick, __state);
}

[HarmonyPatch(typeof(CampaignEvents), nameof(CampaignEvents.Tick), new[] { typeof(float) })]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class CampaignEvents_Tick_MapProfiler_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MapProfilerTargets.TickEventSwaps(),
            "CampaignEvents.Tick", MapFrameProfilerHooks.Logger, out var swapped);
        MapFrameProfilerHooks.TickEventSites = swapped;
        return result;
    }
}

[HarmonyPatch(typeof(MapScreen), "OnFrameTick", new[] { typeof(float) })]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class MapScreen_OnFrameTick_MapProfiler_Patch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state) => __state = MapFrameProfilerHooks.BeginPhase();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(long __state) => MapFrameProfilerHooks.EndPhase(MapPhase.MapScreen, __state);
}

[HarmonyPatch]
[HarmonyPatchCategory(MapFrameProfilerInstaller.Category)]
public static class SubModule_OnApplicationTick_MapProfiler_Patch
{
    public static MethodBase TargetMethod() => AccessTools.Method(typeof(global::TAOM.SubModule), "OnApplicationTick", new[] { typeof(float) });

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    public static void Prefix(out long __state) => __state = MapFrameProfilerHooks.BeginPhase();

    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(long __state) => MapFrameProfilerHooks.EndAppTick(__state);
}
