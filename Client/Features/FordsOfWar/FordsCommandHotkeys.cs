using TaleWorlds.InputSystem;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The command panel's key table, read from the scene layer's input context each frame (the panel layer is not a focus layer):
/// 1 to 4 pick a lane, F1 to F8 buy a pack, H fields the first affordable hero, Z Push, X Hold, C Fall back. Ctrl or Alt held
/// leaves every key to the game's own shortcuts. Kept out of <see cref="FordsCommandView"/> to hold it under the ADR-002 cap.
/// </summary>
internal static class FordsCommandHotkeys
{
    internal static void Read(IInputContext input, FordsCommandVM vm)
    {
        if (input.IsControlDown() || input.IsAltDown())
            return;

        for (var lane = 0; lane < FordsCommandVM.LaneHotkeys; lane++)
        {
            if (input.IsKeyPressed(InputKey.D1 + lane))
                vm.SelectLane(lane);
        }

        for (var offer = 0; offer < FordsCommandVM.OfferHotkeys; offer++)
        {
            if (input.IsKeyPressed(InputKey.F1 + offer))
                vm.BuyOffer(offer);
        }

        if (input.IsKeyPressed(InputKey.H))
            vm.FieldFirstAffordableHero();
        if (input.IsKeyPressed(InputKey.Z))
            vm.SetOrder(LaneOrder.Push);
        if (input.IsKeyPressed(InputKey.X))
            vm.SetOrder(LaneOrder.Hold);
        if (input.IsKeyPressed(InputKey.C))
            vm.SetOrder(LaneOrder.FallBack);
    }
}
