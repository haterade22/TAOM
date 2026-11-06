using TaleWorlds.InputSystem;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The command panel's key table, read from the scene layer's input context each frame (the panel layer is not a focus layer):
/// 1 to 4 pick a lane, F1 to F12 buy a pack, Shift+F1 to F6 buy an upgrade, U shows or hides the upgrades, H fields the first affordable hero, Z Push, X Hold, C Fall back. Ctrl or Alt held
/// leaves every key to the game's own shortcuts. Kept out of <see cref="FordsCommandView"/> to hold it under the ADR-002 cap.
/// </summary>
internal static class FordsCommandHotkeys
{
    /// <summary>The buy keys by offer slot. A table, not F1 plus the slot: the engine numbers F11 and F12 apart from F10 (InputKey.cs).</summary>
    internal static readonly InputKey[] OfferKeys =
    {
        InputKey.F1, InputKey.F2, InputKey.F3, InputKey.F4, InputKey.F5, InputKey.F6, InputKey.F7, InputKey.F8, InputKey.F9, InputKey.F10, InputKey.F11, InputKey.F12,
    };

    internal static void Read(IInputContext input, FordsCommandVM vm)
    {
        if (input.IsControlDown() || input.IsAltDown())
            return;

        for (var lane = 0; lane < FordsCommandVM.LaneHotkeys; lane++)
        {
            if (input.IsKeyPressed(InputKey.D1 + lane))
                vm.SelectLane(lane);
        }

        for (var offer = 0; offer < OfferKeys.Length; offer++)
        {
            if (!input.IsKeyPressed(OfferKeys[offer]))
                continue;

            if (input.IsShiftDown())
                vm.BuyUpgrade(offer);
            else
                vm.BuyOffer(offer);
        }

        if (input.IsKeyPressed(InputKey.U))
            vm.ToggleUpgrades();
        if (input.IsKeyPressed(InputKey.L))
            vm.CycleSpeed();
        if (input.IsKeyPressed(InputKey.H))
            vm.FieldFirstAffordableHero();
        if (input.IsKeyPressed(InputKey.Z))
            vm.SetOrder(LaneOrder.Push);
        if (input.IsKeyPressed(InputKey.X))
            vm.SetOrder(LaneOrder.Hold);
        if (input.IsKeyPressed(InputKey.C))
            vm.SetOrder(LaneOrder.FallBack);
        if (input.IsKeyPressed(InputKey.V))
            vm.SetOrder(LaneOrder.Seize);
        if (input.IsKeyPressed(InputKey.R))
            vm.ToggleRegionPicker();
        if (input.IsKeyPressed(InputKey.G))
            vm.CallCreature(0);
        if (input.IsKeyPressed(InputKey.B))
            vm.CallCreature(1);
        if (input.IsKeyPressed(InputKey.N))
            vm.CallCreature(2);
        if (input.IsKeyPressed(InputKey.M))
            vm.CallCreature(3);
    }
}
