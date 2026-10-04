using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionPerf.Hooks;

// Patch97's two attribution transpilers (plan 041), in plan 028's category, so they install only with the
// tick profiler on. Each swaps call sites for MissionAttributionHooks helpers with the same stack (the swap
// lists and the verified IL are in MissionAttributionInstaller); a count that does not match leaves the method
// vanilla with one [TickProfiler] warning, and the attribution install line reports the sites found.
//   public Agent Mission.SpawnAgent(AgentBuildData, bool, Equipment, ItemObject): main thread; the probe's
//     Patch98 bracket sits on the same method.
//   [EngineCallback] internal void ManagedScriptHolder.TickComponents(float dt): native's thread; the per-type
//     table records only on the main thread, the block totals from any thread.

[HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent), new[] { typeof(AgentBuildData), typeof(bool), typeof(Equipment), typeof(ItemObject) })]
[HarmonyPatchCategory(MissionTickProfilerInstaller.Category)]
public static class Mission_SpawnAgent_Attribution_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MissionAttributionInstaller.SpawnAgentSwaps(),
            "Mission.SpawnAgent", MissionTickProfilerHooks.Logger, out var swapped);
        MissionAttributionInstaller.SpawnSites = swapped;
        return result;
    }
}

[HarmonyPatch(typeof(ManagedScriptHolder), "TickComponents", new[] { typeof(float) })]
[HarmonyPatchCategory(MissionTickProfilerInstaller.Category)]
public static class ManagedScriptHolder_TickComponents_Attribution_Patch
{
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var result = TickProfilerTranspiler.Rewrite(instructions, MissionAttributionInstaller.TickComponentsSwaps(),
            "ManagedScriptHolder.TickComponents", MissionTickProfilerHooks.Logger, out var swapped);
        MissionAttributionInstaller.ScriptSites = swapped;
        return result;
    }
}
