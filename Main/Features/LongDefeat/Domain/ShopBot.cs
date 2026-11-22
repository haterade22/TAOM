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
    Hero,
    Errand,
    EndErrand,
    Gear,
    Engine,
}

/// <summary>One decision. <c>OfferId</c> names what the kind acts on (an offer, a boon, a step, a hero, a place, a gear set, an engine kind), and <c>Site</c> the yard site of an engine (-1 for every other kind).</summary>
internal readonly record struct BotAction(BotActionKind Kind, string? OfferId = null, int Site = -1);

/// <summary>
/// The unattended buyer. It buys foot and archers 2:1 (the first affordable offer of the class that is behind) while the garrison is under the ceiling, and sounds the horn
/// once nothing can be bought and half a minute of the phase has passed, which is what proves the early-start bonus. An open boon pick is answered first
/// (phase2-plan.md section 2): a reroll when one is banked and every card has a drawback, else the first card without a drawback, else the first card.
/// A test run also spends a banked reroll on any offer while it has used none (the plan's note that a 5-wave run proves a reroll at clear 3 needs it), which only
/// an unattended run ever does.
/// The fortifications follow phase3-plan.md D27, one level of each kind a stretch between waves: a repair first when a gate is broken or has lost a tenth of its maximum, then gate level N at
/// clear N (to 3), the Elven Volley at 1 in the preparation and 2 from clear 1, the towers at clear + 1 (to 3), and field care at clears 1 to 3, bought only when the defenders are hurt enough for the heal to show (<see cref="CareProof"/>, #1143) and nobody is still to spawn (a pack or a hero joins at full health before the heal and thins what is missing). A run with a <c>start_gold</c> buys these before its packs;
/// any other run buys them from what its packs left. A row the view shows unavailable (not offered, not carried, at its top, used, or posts that bust the cap) is never asked for, so a build that offers
/// nothing decides exactly as one with no fortifications. In a wave it arms the Elven Volley whenever the view shows it ready (<see cref="BotActionKind.Volley"/>).
/// Gold that no pack and no fortification wants buys an upgrade (#1161): the first row in shop.json order that the gold reaches (<see cref="BotActionKind.Upgrade"/>, the step id in
/// <c>OfferId</c>). The rows open when the last muster batch stands; a horn before that leaves the first upgrade to the next intermission.
/// The two places follow phase4-plan.md D44: the first armour set from the preparation, the class's weapons set and one engine at the first free site from clear 1, each after the fortifications
/// (a run with a <c>start_gold</c>) or after the packs (any other run). The bot cannot stand at a place by itself, so it opens an errand (<see cref="BotActionKind.Errand"/>), waits while the lord
/// walks, buys when the view shows him there, and never sounds the horn while the errand is open. An errand starts only when its walk budget fits in the time left, so one that cannot fit moves to
/// the next clear, and a place an errand failed to reach is left alone until the next wave.
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

        var errand = view.Locations.Errand;
        if (errand != LdPlaceKind.None)
        {
            if (WantedAt(view, errand) is not { } wanted)
                return new BotAction(BotActionKind.EndErrand);

            return view.Locations.Near == errand ? wanted : new BotAction(BotActionKind.Wait);
        }

        if (view.Fort.BuysFirst && (Fortification(view) ?? Place(view)) is { } first)
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

        if (!view.Fort.BuysFirst && (Fortification(view) ?? Place(view)) is { } afterPacks)
            return afterPacks;

        // A hero of the shop (#1325), once no pack fits: the first one for sale that the gold reaches and the cap has room for.
        var hero = view.Heroes.FirstOrDefault(h => h.State == HeroState.ForSale && h.Price <= view.Gold && held + 1 <= view.GarrisonCap);
        if (hero != null)
            return new BotAction(BotActionKind.Hero, hero.Offer.Id);

        var upgrade = view.Upgrades.FirstOrDefault(r => r.Affordable > 0);
        if (upgrade != null)
            return new BotAction(BotActionKind.Upgrade, upgrade.Step.Id);

        return secondsInPhase >= HornAfterSeconds ? new BotAction(BotActionKind.Horn) : new BotAction(BotActionKind.Wait);
    }

    /// <summary>
    /// What the schedule wants at a place now: the buy itself when the lord already stands there, else the errand to it, when its budget fits in the time left and no errand failed to reach it
    /// in this stretch. The armoury comes before the yard. Null when neither place has anything the gold reaches.
    /// </summary>
    private static BotAction? Place(RunView view)
    {
        if (!view.Locations.LordStands)
            return null;

        foreach (var place in Places)
        {
            if (WantedAt(view, place) is not { } wanted)
                continue;
            if (view.Locations.Near == place)
                return wanted;
            if (!view.Locations.Unreached.Contains(place) && view.SecondsLeft >= SurvivalRun.ErrandBudgetSeconds)
                return new BotAction(BotActionKind.Errand, place.ToString());
        }

        return null;
    }

    private static readonly LdPlaceKind[] Places = { LdPlaceKind.Armoury, LdPlaceKind.Yard };

    /// <summary>
    /// The buy the schedule wants at a place and the gold reaches, or null (D44). The armoury, for a lord in his own body: the first armour set, then from clear 1 the first weapons set. The yard, from
    /// clear 1: one engine for the run, the first kind the gold reaches at the first free site.
    /// </summary>
    private static BotAction? WantedAt(RunView view, LdPlaceKind place)
    {
        var loc = view.Locations;
        if (place == LdPlaceKind.Armoury && loc.OwnBody)
        {
            var armour = loc.Gear.FirstOrDefault(r => r.Set.Kind == GearKind.Armour);
            var weapons = view.WavesCleared >= 1 ? loc.Gear.FirstOrDefault(r => r.Set.Kind == GearKind.Weapons) : null;
            var set = new[] { armour, weapons }.FirstOrDefault(r => r != null && r.State == GearState.ForSale && r.Price <= view.Gold);
            return set != null ? new BotAction(BotActionKind.Gear, set.Set.Id) : null;
        }

        if (place == LdPlaceKind.Yard && view.WavesCleared >= 1 && loc.Sites.All(s => s.State == SiteState.Free))
        {
            var site = loc.Sites.FirstOrDefault();
            var kind = site?.Kinds.FirstOrDefault(k => k.Price <= view.Gold);
            return kind != null ? new BotAction(BotActionKind.Engine, kind.Kind, site!.Site.Index) : null;
        }

        return null;
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
        if (cleared >= 1 && cleared <= TopLevel && fort.Care.Available && fort.Care.Price <= view.Gold && view.DefendersPending == 0 && CareProof.WorthBuying(fort.CareShare, fort.DefenderHealthMean))
            return new BotAction(BotActionKind.FieldCare);

        return null;
    }
}
