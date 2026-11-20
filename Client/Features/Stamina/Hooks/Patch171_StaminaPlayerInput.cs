using HarmonyLib;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Patch171 (docs/reference/harmony-patch-registry.md, "Patch171_Stamina"): postfix on the private
/// <c>MissionMainAgentController.ControlTick()</c> (v1.5.7 MissionMainAgentController.cs:395), which writes the
/// player's input for the frame: it sets the main agent's event and movement flags to None (:463-464) and adds the
/// keys held. The postfix is the last writer in the call that builds the input, and it runs on every return path of
/// that call, so an Exhausted soldier's attack, block, jump and kick input goes here (#1497). Other behaviors'
/// OnPreMissionTick still run after it. A write from the mission tick is too late: the native
/// <c>Mission.Tick</c>, which runs this pre-mission tick and then the agent jobs, comes before the managed
/// <c>Mission.OnTick</c> (<c>MissionState.TickMissionAux</c>). The engine calls ControlTick only for a main agent that a
/// player controls (:226), so the postfix never sees an AI soldier; StaminaInputComponent strips those. In
/// TAOM.Client because the target is client-only (ADR-013). All rules are in <see cref="StaminaHooks.StripPlayerInput"/>,
/// which does nothing unless the mission tick put this soldier on the player lock path.
/// </summary>
[HarmonyPatch(typeof(MissionMainAgentController), "ControlTick")]
[HarmonyPatchCategory(StaminaModule.PatchCategory)]
public static class Patch171_StaminaPlayerInput
{
    [HarmonyPostfix]
    public static void Postfix(MissionMainAgentController __instance) =>
        StaminaHooks.StripPlayerInput(__instance.Mission?.MainAgent);
}
