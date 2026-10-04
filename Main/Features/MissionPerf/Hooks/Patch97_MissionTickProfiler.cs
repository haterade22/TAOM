using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

// Patch97 (default off; applied once per process by MissionTickProfilerInstaller, never by name from
// SubModule.cs). Verified on the installed v1.5.3:
//   public void Mission.OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick): main thread;
//     its IL holds exactly one `callvirt MissionBehavior::OnPreDisplayMissionTick(float32)` and exactly one
//     `callvirt MissionBehavior::OnMissionTick(float32)`, each after `callvirt List<MissionBehavior>::get_Item`
//     and `ldarg.1`, and no OnPreMissionTick or WaitTickCompletion.
//   [MBCallback] internal void Mission.OnPreTick(float dt): raised by native from Mission.Tick on the main
//     thread; its IL holds exactly one `call Mission::WaitTickCompletion()` and exactly one
//     `callvirt MissionBehavior::OnPreMissionTick(float32)`, and no OnMissionTick or OnPreDisplayMissionTick.
// Each swap turns the instance call into a static `call` of a MissionTickProfilerHooks helper taking the
// instance first: stack-identical, no inserted instruction, no branch. A site that is not found exactly once
// leaves its whole method vanilla with one [TickProfiler] warning (TickProfilerTranspiler). WaitTickCompletion
// is timed at its call site because a 17-byte private method may be inlined into a caller, which would bypass
// a prefix on it. The OnPreTick prefix is the frame boundary: one frame is one mission tick.

[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory(MissionTickProfilerInstaller.Category)]
public static class Mission_OnTick_TickProfiler_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MissionTickProfilerInstaller.OnTickSwaps(),
            "Mission.OnTick", MissionTickProfilerHooks.Logger, out var swapped);
        MissionTickProfilerHooks.OnTickSites = swapped;
        return result;
    }
}

[HarmonyPatch(typeof(Mission), "OnPreTick", new[] { typeof(float) })]
[HarmonyPatchCategory(MissionTickProfilerInstaller.Category)]
public static class Mission_OnPreTick_TickProfiler_Patch
{
    [HarmonyPrefix]
    public static void Prefix() => MissionTickProfilerHooks.OnFrameBoundary();

    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MissionTickProfilerInstaller.OnPreTickSwaps(),
            "Mission.OnPreTick", MissionTickProfilerHooks.Logger, out var swapped);
        MissionTickProfilerHooks.OnPreTickSites = swapped;
        return result;
    }
}
