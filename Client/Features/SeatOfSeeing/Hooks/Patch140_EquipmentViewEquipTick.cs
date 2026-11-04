using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): prefix on the private
/// <c>MissionGauntletMainAgentEquipmentControllerView.EquipWeaponTick(float)</c> that skips it while the player's view is in the Seat, because
/// game key 13 acts on the main agent and would act from the air. The view's own <c>IsMainAgentAvailable</c> is not patched: its body is short
/// enough for the JIT to inline. Off the Seat the prefix returns true at its first statement. Seated, it also clears the view's pending press
/// (<c>_prevEquipKeyDown</c>) and its hold time (<c>_equipHoldTime</c>): the view clears them only in the else branch of its screen tick, which a skip
/// inside the if branch never reaches, so a press under 0.5 s made just before entering would run its quick release on the first tick after the Seat.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletMainAgentEquipmentControllerView), "EquipWeaponTick")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_EquipmentViewEquipTick
{
    // The injected fields have a leading underscore, so each parameter has four (HarmonyFieldInjectionNamingTests).
    [HarmonyPrefix]
    public static bool Prefix(ref bool ____prevEquipKeyDown, ref float ____equipHoldTime)
    {
        if (!SeatPresence.IsSeated)
            return true;

        ____prevEquipKeyDown = false;
        ____equipHoldTime = 0f;
        return false;
    }
}
