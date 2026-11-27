using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (design section 3, docs/features/seat-of-seeing.md): prefix on <c>MissionOrderVM.OnTroopFormationSelected(int)</c>, the one method the
/// number keys call (<c>GauntletOrderUIHandler.TickInput</c> :562) that has no Control test. While the command mouse is live, Left Control is
/// down and the index names a company (0 to 7), the prefix stores a transfer request and skips the original; the command mouse's mission tick
/// moves the selected companies' soldiers into that company. Every other call runs vanilla whole. A gamepad's company shortcut reaches this method
/// too (<c>ExecuteSelectHighlightedFormation</c>, :1003) and is taken the same way when Left Control is down on a keyboard: accepted.
/// </summary>
[HarmonyPatch(typeof(MissionOrderVM), nameof(MissionOrderVM.OnTroopFormationSelected))]
[HarmonyPatchCategory(SeatOfSeeingModule.PatchCategory)]
public static class Patch140_CtrlNumberTransfer
{
    [HarmonyPrefix]
    public static bool Prefix(int formationTroopIndex)
    {
        if (!SeatPresence.CommandMouse || !SeatTransferRule.Intercepts(true, Input.IsKeyDown(InputKey.LeftControl), formationTroopIndex))
            return true;

        SeatPresence.RequestTransfer(formationTroopIndex);
        return false;
    }
}
