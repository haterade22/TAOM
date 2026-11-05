using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>Patch140 (build-spec 8.2): prefix on <c>MissionBoundaryCrossingHandler.HandleAgentStateChange</c> that tells the engine the main agent is inside the line while the view is in the Seat; the registry holds why.</summary>
[HarmonyPatch(typeof(MissionBoundaryCrossingHandler), "HandleAgentStateChange")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_BoundaryCountdown
{
    [HarmonyPrefix]
    public static void Prefix(Agent agent, ref bool isAgentOutside)
    {
        if (isAgentOutside && SeatPresence.HoldsBoundaryCountdown(agent != null && agent.IsMainAgent))
            isAgentOutside = false;
    }
}
