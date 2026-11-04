using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI.Mission;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md, build-spec 6.4): postfix on the private <c>MissionGauntletMainAgentControlModeView.IsMainAgentAvailable</c> that answers
/// false while the player's view is in the Seat, so the walk mode control does not open from the air; a false answer also closes one that is open. The
/// control opens on the hot key <c>ControlModeToggle</c>, the controller's left pad down. Game key 30 (CapsLock) is read by
/// <c>MissionMainAgentController</c> (line 811), which the shut controller already stops, so this patch serves a player who mixes a gamepad with the
/// Seat. Off the Seat the postfix returns at its first statement.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletMainAgentControlModeView), "IsMainAgentAvailable")]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_ControlModeViewAvailable
{
    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        if (SeatPresence.IsSeated)
            __result = false;
    }
}
