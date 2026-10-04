using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

// Patch98 hitch probe (plan 041; default on, "Enable Hitch Probe"; applied once per process by
// HitchProbeInstaller from MissionTickProfilerInstaller, never by name from SubModule.cs). Whole-method
// brackets only, no transpiler: each class is a void prefix at Priority.First and a void finalizer at
// Priority.Last, each taking only Harmony's __state (out on the prefix, ref on the finalizer), so the bracket
// encloses every other patch on the method (Patch23 on SpawnAgent, Patch35 on OnTick) and still closes when the
// method throws (a void finalizer keeps Harmony's rethrow). Verified on the installed v1.5.3, none of them virtual:
//   private void Mission.WaitTickCompletion(): `while (!tickCompleted) Thread.Sleep(1);`, called first in
//     OnPreTick; 17 bytes with a loop, so the JIT may inline it into OnPreTick, which the probe detects.
//   [MBCallback] internal void Mission.OnPreTick(float dt): raised by native from Mission.Tick on the main
//     thread; WaitTickCompletion, the reverse OnPreMissionTick loop, the empty TickDebugAgents. Its prefix
//     closes the previous frame and opens the next (one frame is one mission tick), then samples clip loading.
//   public void Mission.OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick): main thread.
//   [EngineCallback] internal void ManagedScriptHolder.TickComponents(float dt): reached from native through
//     ManagedCallbacks for every scene with script components (mission, campaign map, any other); the thread
//     is native's choice and is logged at run time (HitchProbeHooks.Script.cs).
//   public Agent Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject): the only overload; the main
//     thread per .claude/rules/harmony-patches.md, checked at run time; calls nest through OnAgentBuild.
// PatchShield (decision D13, 2026-10-03): WaitTickCompletion and TickComponents stay on
// PatchShieldPolicy.ExcludedTargetMethods; OnPreTick, OnTick and SpawnAgent are shielded again (HitchProbeBindingTests
// walks all five). The shield strips a shielded owner's prefixes after a missing-API exception and never its
// finalizers, so a finalizer here can run with no prefix before it. Each call therefore carries its own
// ProbeState through __state (Harmony starts it at PrefixMissing in every call, each prefix sets Entered before any
// code that can throw (OnPreTick's runs the frame boundary first, which catches its own faults), the finalizer
// closes it): a finalizer whose own call had no prefix records nothing, is counted and writes one WARNING line per
// method per process, and a rerun of the finalizers for one call (Harmony does that when a later finalizer throws)
// does nothing. HitchProbeWiringTests pins the signatures.
// Keep the WaitTickCompletion class first: the category applies classes in assembly order (PatchCategoryIndex),
// and Harmony stops a method being inlined only when it detours that method, so the wait is detoured before
// OnPreTick's replacement compiles and cannot be inlined into it. HitchProbeWiringTests pins the order; the
// runtime wait self-check reports the inlined case anyway.

[HarmonyPatch(typeof(Mission), "WaitTickCompletion")]
[HarmonyPatchCategory("Patch98_HitchProbe")]
public static class Mission_WaitTickCompletion_HitchProbe_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeState __state) => HitchProbeHooks.OnWaitEnter(out __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    public static void Finalizer(ref ProbeState __state) => HitchProbeHooks.OnWaitExit(ref __state);
}

[HarmonyPatch(typeof(Mission), "OnPreTick", new[] { typeof(float) })]
[HarmonyPatchCategory("Patch98_HitchProbe")]
public static class Mission_OnPreTick_HitchProbe_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeState __state) { MissionTickProfilerHooks.OnFrameBoundary(); HitchProbeHooks.OnPreTickEnter(out __state); }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    public static void Finalizer(ref ProbeState __state) => HitchProbeHooks.OnPreTickExit(ref __state);
}

[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory("Patch98_HitchProbe")]
public static class Mission_OnTick_HitchProbe_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeState __state) => HitchProbeHooks.OnTickEnter(out __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    public static void Finalizer(ref ProbeState __state) => HitchProbeHooks.OnTickExit(ref __state);
}

[HarmonyPatch(typeof(ManagedScriptHolder), "TickComponents", new[] { typeof(float) })]
[HarmonyPatchCategory("Patch98_HitchProbe")]
public static class ManagedScriptHolder_TickComponents_HitchProbe_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeState __state) => HitchProbeHooks.OnScriptTickEnter(out __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    public static void Finalizer(ref ProbeState __state) => HitchProbeHooks.OnScriptTickExit(ref __state);
}

[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool), typeof(Equipment), typeof(ItemObject) })]
[HarmonyPatchCategory("Patch98_HitchProbe")]
public static class Mission_SpawnAgent_HitchProbe_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(out ProbeState __state) => HitchProbeHooks.OnSpawnEnter(out __state);

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.Last)]
    public static void Finalizer(ref ProbeState __state) => HitchProbeHooks.OnSpawnExit(ref __state);
}
