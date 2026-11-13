using System;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

internal enum BotActionKind
{
    Wait,
    Buy,
    Horn,
    Pick,
    Reroll,
    GateLevel,
    GateRepair,
    TowerLevel,
    FieldCare,
    VolleyLevel,
    Volley,
    Upgrade,
}

internal readonly record struct BotAction(BotActionKind Kind, string? OfferId = null);

/// <summary>
/// The unattended buyer. It buys foot and archers 2:1 (the first affordable offer of the class that is behind) while the garrison is under the ceiling, and sounds the horn
/// once nothing can be bought and half a minute of the phase has passed, which is what proves the early-start bonus. An open boon pick is answered first
/// (phase2-plan.md section 2): a reroll when one is banked and every card has a drawback, else the first card without a drawback, else the first card.
/// A test run also spends a banked reroll on any offer while it has used none (the plan's note that a 5-wave run proves a reroll at clear 3 needs it), which only
/// an unattended run ever does.
/// The fortifications follow phase3-plan.md D27, one level of each kind a stretch between waves: a repair first when a gate is broken or has lost a tenth of its maximum, then gate level N at
/// clear N (to 3), the Elven Volley at 1 in the preparation and 2 from clear 1, the towers at clear + 1 (to 3), and field care at clears 1 to 3. A run with a <c>start_gold</c> buys these before its packs;
/// any other run buys them from what its packs left. A row the view shows unavailable (not offered, not carried, at its top, used, or posts that bust the cap) is never asked for, so a build that offers
/// nothing decides exactly as one with no fortifications. In a wave it arms the Elven Volley whenever the view shows it ready (<see cref="BotActionKind.Volley"/>).
/// Gold that no pack and no fortification wants buys an upgrade (#1161): the first row in shop.json order that the gold reaches (<see cref="BotActionKind.Upgrade"/>, the step id in
/// <c>OfferId</c>), so an unattended run proves the step before its horn.
/// </summary>
internal static class ShopBot
{
    private const int GarrisonCeiling = 170;
    private const float HornAfterSeconds = 30f;

    /// <summary>The share of a gate's maximum lost that earns a repair (the plan's 0.1).</summary>
    private const float RepairShare = 0.1f;

    /// <summary>The last level of each kind the schedule buys.</summary>
    private const int TopLevel = 3;

    internal static BotAction Decide(RunView view, float secondsInPhase)
    {
        // In a wave the only thing the bot does is fire the Elven Volley whenever the view shows it ready (D27).
        if (!view.ShopOpen)
            return new BotAction(view.Phase == RunPhase.Wave && view.Volleys.Ready ? BotActionKind.Volley : BotActionKind.Wait);

        if (view.BoonOffer.Count > 0)
        {
            if (view.Rerolls > 0 && (view.RerollsUsed == 0 || view.BoonOffer.All(c => c.HasDrawback)))
                return new BotAction(BotActionKind.Reroll);

            return new BotAction(BotActionKind.Pick, (view.BoonOffer.FirstOrDefault(c => !c.HasDrawback) ?? view.BoonOffer[0]).Id);
        }

        if (view.Fort.BuysFirst && Fortification(view) is { } first)
            return first;

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

        if (!view.Fort.BuysFirst && Fortification(view) is { } afterPacks)
            return afterPacks;

        var upgrade = view.Upgrades.FirstOrDefault(r => r.Affordable > 0);
        if (upgrade != null)
            return new BotAction(BotActionKind.Upgrade, upgrade.Step.Id);

        return secondsInPhase >= HornAfterSeconds ? new BotAction(BotActionKind.Horn) : new BotAction(BotActionKind.Wait);
    }

    /// <summary>The first fortification the schedule wants that the view shows available and the gold reaches, or null.</summary>
    private static BotAction? Fortification(RunView view)
    {
        var fort = view.Fort;
        var cleared = view.WavesCleared;
        bool Wants(FortRow row, int target) => row.Available && row.Level < target && row.Price <= view.Gold;

        if (fort.Repair.Available && fort.Repair.Price <= view.Gold && fort.Gates.Any(g => g.Destroyed || g.MissingShare >= RepairShare))
            return new BotAction(BotActionKind.GateRepair);
        if (Wants(fort.Gate, Math.Min(cleared, TopLevel)))
            return new BotAction(BotActionKind.GateLevel);
        if (Wants(fort.Volley, cleared == 0 ? 1 : 2))
            return new BotAction(BotActionKind.VolleyLevel);
        if (Wants(fort.Towers, Math.Min(cleared + 1, TopLevel)))
            return new BotAction(BotActionKind.TowerLevel);
        if (cleared >= 1 && cleared <= TopLevel && fort.Care.Available && fort.Care.Price <= view.Gold)
            return new BotAction(BotActionKind.FieldCare);

        return null;
    }
}
