using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

internal enum BotActionKind
{
    Wait,
    Buy,
    Horn,
}

internal readonly record struct BotAction(BotActionKind Kind, string? OfferId = null);

/// <summary>
/// The unattended buyer. It buys foot and archers 2:1 (the first affordable offer of the class that is behind) while the garrison is under the ceiling, and sounds the horn
/// once nothing can be bought and half a minute of the phase has passed, which is what proves the early-start bonus.
/// </summary>
internal static class ShopBot
{
    private const int GarrisonCeiling = 170;
    private const float HornAfterSeconds = 30f;

    internal static BotAction Decide(RunView view, float secondsInPhase)
    {
        if (!view.ShopOpen)
            return new BotAction(BotActionKind.Wait);

        if (view.DefendersStanding + view.DefendersPending < GarrisonCeiling)
        {
            var wanted = view.FootBought < 2 * (view.RangedBought + 1) ? FormationSlot.Infantry : FormationSlot.Ranged;
            var pick = view.Offers.FirstOrDefault(o => o.Slot == wanted && o.Price <= view.Gold)
                ?? view.Offers.FirstOrDefault(o => o.Price <= view.Gold);
            if (pick != null)
                return new BotAction(BotActionKind.Buy, pick.Id);
        }

        return secondsInPhase >= HornAfterSeconds ? new BotAction(BotActionKind.Horn) : new BotAction(BotActionKind.Wait);
    }
}
