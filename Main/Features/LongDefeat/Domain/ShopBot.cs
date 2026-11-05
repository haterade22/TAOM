using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

internal enum BotActionKind
{
    Wait,
    Buy,
    Horn,
    Pick,
    Reroll,
}

internal readonly record struct BotAction(BotActionKind Kind, string? OfferId = null);

/// <summary>
/// The unattended buyer. It buys foot and archers 2:1 (the first affordable offer of the class that is behind) while the garrison is under the ceiling, and sounds the horn
/// once nothing can be bought and half a minute of the phase has passed, which is what proves the early-start bonus. An open boon pick is answered first
/// (phase2-plan.md section 2): a reroll when one is banked and every card has a drawback, else the first card without a drawback, else the first card.
/// A test run also spends a banked reroll on any offer while it has used none (the plan's note that a 5-wave run proves a reroll at clear 3 needs it), which only
/// an unattended run ever does.
/// </summary>
internal static class ShopBot
{
    private const int GarrisonCeiling = 170;
    private const float HornAfterSeconds = 30f;

    internal static BotAction Decide(RunView view, float secondsInPhase)
    {
        if (!view.ShopOpen)
            return new BotAction(BotActionKind.Wait);

        if (view.BoonOffer.Count > 0)
        {
            if (view.Rerolls > 0 && (view.RerollsUsed == 0 || view.BoonOffer.All(c => c.HasDrawback)))
                return new BotAction(BotActionKind.Reroll);

            return new BotAction(BotActionKind.Pick, (view.BoonOffer.FirstOrDefault(c => !c.HasDrawback) ?? view.BoonOffer[0]).Id);
        }

        var held = view.DefendersStanding + view.DefendersPending;
        if (held < GarrisonCeiling)
        {
            // A pack the cap would refuse (a boon can lower it under the ceiling) is never asked for, or the intermission would wait for the timeout.
            bool Fits(ShopOffer o) => o.Price <= view.Gold && held + o.PackSize <= view.GarrisonCap;
            var wanted = view.FootBought < 2 * (view.RangedBought + 1) ? FormationSlot.Infantry : FormationSlot.Ranged;
            var pick = view.Offers.FirstOrDefault(o => o.Slot == wanted && Fits(o))
                ?? view.Offers.FirstOrDefault(Fits);
            if (pick != null)
                return new BotAction(BotActionKind.Buy, pick.Id);
        }

        return secondsInPhase >= HornAfterSeconds ? new BotAction(BotActionKind.Horn) : new BotAction(BotActionKind.Wait);
    }
}
