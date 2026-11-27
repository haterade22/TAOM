using HarmonyLib;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Features.SeatOfSeeing.Hooks;

/// <summary>
/// Patch140 (docs/features/seat-of-seeing.md): prefix on <c>MissionOrderVM.OnTroopFormationSelected(int)</c>, the one method the number keys call
/// (<c>GauntletOrderUIHandler.TickInput</c> :562) with no Control test. It stores a Ctrl and number transfer request and skips the original;
/// every other call runs vanilla whole.
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
