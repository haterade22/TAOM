using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): prefix on the private
/// <c>MissionGauntletMainAgentEquipmentControllerView.DropWeaponTick(float)</c> that skips it while the player's view is in the Seat, because
/// game key 22 acts on the main agent and would act from the air. The view's own <c>IsMainAgentAvailable</c> is not patched: its body is short
/// enough for the JIT to inline. Off the Seat the prefix returns true at its first statement. Seated, it also clears the view's pending press
/// (<c>_prevDropKeyDown</c>) and its hold time (<c>_dropHoldTime</c>): the view clears them only in the else branch of its screen tick, which a skip
/// inside the if branch never reaches, so a press under 0.5 s made just before entering would run its quick release on the first tick after the Seat.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletMainAgentEquipmentControllerView), "DropWeaponTick")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_EquipmentViewDropTick
{
    // The injected fields have a leading underscore, so each parameter has four (HarmonyFieldInjectionNamingTests).
    [HarmonyPrefix]
    public static bool Prefix(ref bool ____prevDropKeyDown, ref float ____dropHoldTime)
    {
        if (!SeatPresence.IsSeated)
            return true;

        ____prevDropKeyDown = false;
        ____dropHoldTime = 0f;
        return false;
    }
}
