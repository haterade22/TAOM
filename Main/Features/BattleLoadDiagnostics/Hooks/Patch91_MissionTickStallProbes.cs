using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.BattleLoadDiagnostics.Hooks;

// Patch91 (#634): brackets the two ticks a frozen battle can be stuck in, so MissionTickStallWatchdog
// can photograph the stuck thread from its timer. Verified identical on v1.4.8 and v1.5.3:
// `public void Mission.TickAgentsAndTeamsImp(float dt, bool tickPaused)` (called from the [MBCallback]
// TickAgentsAndTeams on the asynchronous agent thread, or inline when the AI tick is synchronous) and
// `private void MissionState.TickMissionAux(float dt, float realDt, bool updateCamera, bool asyncAITick)`
// (main thread: the native Mission.Tick, whose callbacks include OnPreTick's WaitTickCompletion and every
// native-raised agent callback, then Mission.OnTick with every OnMissionTick). A finalizer, not a postfix,
// clears the probe: it runs when the tick throws too, so an exception can never leave a tick in flight.

[HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp), new[] { typeof(float), typeof(bool) })]
[HarmonyPatchCategory("Patch91_MissionTickStall")]
public static class Mission_TickAgentsAndTeamsImp_StallProbe_Patch
{
    [HarmonyPrefix]
    public static void Prefix() => MissionTickStallProbe.AsyncAgentTick.Enter();

    [HarmonyFinalizer]
    public static void Finalizer() => MissionTickStallProbe.AsyncAgentTick.Exit();
}

[HarmonyPatch(typeof(MissionState), "TickMissionAux", new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory("Patch91_MissionTickStall")]
public static class MissionState_TickMissionAux_StallProbe_Patch
{
    // Only a Continuing mission arms the probe. Once EndMission has run, the next frame's Mission.OnTick
    // tears the mission down inside this call (CheckMissionEnd -> EndMissionInternal), and a long exit is
    // not a frozen battle: ExitStallSampler owns that window.
    [HarmonyPrefix]
    public static void Prefix(MissionState __instance)
    {
        if (__instance.CurrentMission?.CurrentState == Mission.State.Continuing)
            MissionTickStallProbe.MissionFrame.Enter();
    }

    [HarmonyFinalizer]
    public static void Finalizer() => MissionTickStallProbe.MissionFrame.Exit();
}
