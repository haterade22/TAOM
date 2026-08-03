using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Phase 5b — closes the last blind window in agent construction.
//
// Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch brackets ONE call inside Mission.BuildAgent
// (Mission.cs:4034). The engine then keeps working on the same agent with no stamp of any kind:
//
//   agent.InitializeAgentRecord();              // :4035
//   agent.AgentVisuals.BatchLastLodMeshes();    // :4036   mesh/GPU batching
//   agent.PreloadForRendering();                // :4037
//   agent.SetActionChannel(0, ...);             // :4041   plays GetCurrentAction(0) on channel 0
//   agent.InitializeComponents();               // :4043
//   _activeAgents.Add(agent);                   // :4048
//
// A native fault anywhere in there produced a log ending on `AgentEquipOk agent#0` — identical to
// a fault between two agents, which is where the 2026-08-02 Dunland tournament CTD stalled the
// investigation. A postfix here makes the two cases distinguishable: an AgentEquipOk with no
// matching AgentBuildDone means the fault is in that tail.
//
// BuildAgent is private (verified against the installed v1.4.7 TaleWorlds.MountAndBlade.dll:
// `private void BuildAgent(Agent agent, AgentBuildData agentBuildData)` at Mission.cs:4015), so the
// target is declared by name+type rather than nameof.
[HarmonyPatch(typeof(Mission), "BuildAgent")]
[HarmonyPatchCategory("Patch43_BattleLoadDiagnostics")]
public static class Mission_BuildAgent_BattleLoad_Patch
{
    private static IBattleLoadDiagnosticsService? _service;

    public static void Initialize(IBattleLoadDiagnosticsService service) => _service = service;

    // Same double gate as the equip patch: a two-bool no-op outside the initial-load window, so
    // reinforcement waves stay unlogged.
    [HarmonyPostfix]
    public static void Postfix(Agent agent)
    {
        var svc = _service;
        if (svc == null || !svc.IsEnabled) return;
        if (!BattleLoadLoadingWindow.IsOpen) return;
        try { svc.LogAgentBuildDone(agent?.Index ?? -1, agent?.Name ?? "<unnamed>"); }
        catch { /* diagnostic only — never break agent spawn */ }
    }
}
