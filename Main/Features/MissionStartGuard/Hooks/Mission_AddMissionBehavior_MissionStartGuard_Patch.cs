using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.MissionStartGuard.Hooks;

/// <summary>
/// Patch103's second class (docs/reference/harmony-patch-registry.md "Patch103_MissionStartGuard"): a bool prefix on
/// <c>Mission.AddMissionBehavior</c>. While a wrapped <c>EarlyStart</c> or <c>AfterStart</c> call runs, the engine is
/// walking <c>MissionBehaviors</c>, so an add made then is queued (<see cref="MissionBehaviorDeferral"/>) and the
/// engine's add is skipped; Patch103's <c>AfterStart</c> finalizer applies the queue. Any other call, an add outside
/// those two windows or on another mission, runs the original untouched. Nothing here throws.
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.AddMissionBehavior))]
[HarmonyPatchCategory(MissionStartGuardModule.PatchCategory)]
public static class Mission_AddMissionBehavior_MissionStartGuard_Patch
{
    // __0, not the engine's name `missionBehavior`: Harmony binds by name, and an engine rename would fail this class's
    // apply and with it the whole Patch103 category, the older AfterStart guard included.
    // Priority.First: a foreign prefix that skips the add must not hide it from the queue (and one that runs first and
    // skips it keeps its own meaning, as Harmony orders it).
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static bool Prefix(Mission __instance, MissionBehavior __0) =>
        !MissionStartGuardCalls.TryDeferAdd(__instance, __0);
}
