using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>What makes each faction play differently: its feats, Gondor's rotating regions and Mordor's creature call.</summary>
[TestClass]
public class FactionIdentityTests
{
    private static readonly UnitOffer Warg = new("wargs", "warg", UnitRole.Cavalry, 3, 90);
    private static readonly UnitOffer TrollCall = new("troll_call", "troll", UnitRole.Siege, 1, 200);

    private static RegionBand Region(string id) =>
        new(id, "{=x}" + id, new List<UnitOffer> { new(id + "_a", id + "_troop_a", UnitRole.Infantry, 5, 50), new(id + "_b", id + "_troop_b", UnitRole.Ranged, 5, 60) });

    private static readonly OfferRotation Rotation = new(30f, 150, new List<RegionBand> { Region("r1"), Region("r2"), Region("r3") });

    private static FordsMatch Match(FactionFeats? westFeats = null, FactionFeats? eastFeats = null, OfferRotation? rotation = null, CreatureCall? creatures = null,
        FordsMatchConfig? config = null, int seed = 7, string? eastProfile = "slow")
    {
        var factions = Factions();
        var west = factions[MatchSide.West] with { Feats = westFeats, Rotation = rotation, Creatures = creatures };
        var east = factions[MatchSide.East] with { Feats = eastFeats };
        return new FordsMatch(config ?? Config(), Layout(), new Dictionary<MatchSide, FactionCatalog> { [MatchSide.West] = west, [MatchSide.East] = east },
            Slots(eastProfile), new SeededFordsRandom(seed));
    }

    // Feats.

    [TestMethod]
    public void TowerHitPoints_ScaleTheOwnersTowersOnly()
    {
        var match = Match(new FactionFeats { TowerHitPoints = 1.5f });
        var view = match.GetView(WestSlot);

        var west = Tower(view, FordsTestLayout.WestOuter0);
        var east = Tower(view, FordsTestLayout.EastOuter0);

        Assert.AreEqual(east.MaxHitPoints * 3 / 2, west.MaxHitPoints);
        Assert.AreEqual(west.MaxHitPoints, west.HitPoints);
    }

    [TestMethod]
    public void DamageToTowers_ScalesTheAttackersBlows()
    {
        var match = Match(new FactionFeats { DamageToTowers = 2f });

        Assert.AreEqual(200, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 100, MatchSide.West)));
        Assert.AreEqual(100, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.WestOuter0, false, UnitRole.Infantry, 100, MatchSide.East)));
    }

    [TestMethod]
    public void MountedDamageToTowers_ScalesCavalryAndHorseArchersOnly()
    {
        var match = Match(new FactionFeats { MountedDamageToTowers = 0.25f });

        Assert.AreEqual(25, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Cavalry, 100, MatchSide.West)));
        Assert.AreEqual(25, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.HorseArcher, 100, MatchSide.West)));
        Assert.AreEqual(100, match.AdjustTowerDamage(new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Infantry, 100, MatchSide.West)));
    }

    [TestMethod]
    public void AFeatOfZero_DealsNoDamageAtAll_AndSaysWhy()
    {
        var match = Match(new FactionFeats { MountedDamageToTowers = 0f });
        var hit = new TowerHit(FordsTestLayout.EastOuter0, false, UnitRole.Cavalry, 100, MatchSide.West);

        var damage = match.AdjustTowerDamage(hit, out var reason);

        Assert.AreEqual(0, damage);
        Assert.AreEqual(TowerDamageReason.FactionFeat, reason);
    }

    [TestMethod]
    public void HeroSlotBonus_ShowsInTheFactionsFreeSlots()
    {
        var match = Match(new FactionFeats { HeroSlotBonus = 1 });

        Assert.AreEqual(Config().Heroes.MaxAlivePerSide + 1, match.GetView(WestSlot).HeroSlotsFree);
        Assert.AreEqual(Config().Heroes.MaxAlivePerSide, Match().GetView(WestSlot).HeroSlotsFree);
    }

    [TestMethod]
    public void TowerFireRate_FasterOwnerFiresMoreVolleys()
    {
        var match = Match(new FactionFeats { TowerFireRate = 2f });
        var volleys = 0;
        var east = 0;
        for (var i = 0; i < 40; i++)
        {
            var effects = match.Tick(Snap(0.5f, targets: new[] { Threat(FordsTestLayout.WestOuter0, 1), Threat(FordsTestLayout.EastOuter0, 1) }));
            volleys += effects.Volleys.Count(v => v.TowerId == FordsTestLayout.WestOuter0);
            east += effects.Volleys.Count(v => v.TowerId == FordsTestLayout.EastOuter0);
        }

        Assert.IsTrue(east > 0);
        Assert.IsTrue(volleys >= east * 2 - 1, $"west {volleys} east {east}");
    }

    [TestMethod]
    public void TowerFireRate_Zero_NeverFires()
    {
        var match = Match(new FactionFeats { TowerFireRate = 0f });

        var effects = match.Tick(Snap(1f, targets: new[] { Threat(FordsTestLayout.WestOuter0, 1) }));

        Assert.AreEqual(0, effects.Volleys.Count);
    }

    [TestMethod]
    public void Income_ScalesTheSidesPassiveGold()
    {
        var plain = Match();
        var rich = Match(new FactionFeats { Income = 2f });
        var before = plain.GetView(WestSlot).Gold;

        plain.Tick(Snap(60f));
        rich.Tick(Snap(60f));

        Assert.AreEqual((plain.GetView(WestSlot).Gold - before) * 2, rich.GetView(WestSlot).Gold - before);
    }

    [TestMethod]
    public void Bounty_ScalesTheKillersPay()
    {
        var plain = Match();
        var rich = Match(new FactionFeats { Bounty = 2f });
        foreach (var match in new[] { plain, rich })
        {
            match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot, value: 200));
            match.RegisterUnit(2, Unit(20, MatchSide.East, EastSlot, value: 200));
        }

        var before = plain.GetView(WestSlot).Gold;
        plain.OnUnitKilled(Key(20), Key(10));
        rich.OnUnitKilled(Key(20), Key(10));

        Assert.AreEqual((plain.GetView(WestSlot).Gold - before) * 2, rich.GetView(WestSlot).Gold - before);
    }

    [TestMethod]
    public void WaveInterval_AFasterSideGetsMoreFreeWaves()
    {
        var plain = new WaveScheduler(WithFreeWaves(Config()).Waves, Factions(), 2, (_, _) => WestSlot);
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Feats = new FactionFeats { WaveInterval = 0.5f } };
        var fast = new WaveScheduler(WithFreeWaves(Config()).Waves, factions, 2, (_, _) => WestSlot);

        int Count(WaveScheduler scheduler)
        {
            var units = 0;
            for (var i = 0; i < 400; i++)
                units += scheduler.Tick(1f, 0f, (_, _) => 0, _ => 0).Where(r => r.Side == MatchSide.West).Sum(r => r.Count);
            return units;
        }

        var fastUnits = Count(fast);
        var plainUnits = Count(plain);
        Assert.IsTrue(fastUnits > plainUnits * 3 / 2, $"fast {fastUnits} plain {plainUnits}");
    }

    [TestMethod]
    public void UnitCapBonus_RaisesTheSidesAndTheLanesCap()
    {
        var waves = Config().Waves with { MaxLivePerSide = 10, MaxLivePerLanePerSide = 10, MaxSpawnsPerTick = 100 };
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Feats = new FactionFeats { UnitCapBonus = 10 } };
        var scheduler = new WaveScheduler(waves, factions, 2, (_, _) => WestSlot);

        scheduler.TryQueuePack(MatchSide.West, 0, WestSlot, new UnitOffer("big", "t", UnitRole.Infantry, 50, 1));
        scheduler.TryQueuePack(MatchSide.East, 0, EastSlot, new UnitOffer("big", "t", UnitRole.Infantry, 50, 1));
        var requests = scheduler.Tick(1f, 0f, (side, _) => side == MatchSide.West ? 10 : 10, side => side == MatchSide.West ? 10 : 10);

        Assert.IsTrue(requests.Where(r => r.Side == MatchSide.West).Sum(r => r.Count) > 0, "West has room from its bonus");
        Assert.AreEqual(0, requests.Where(r => r.Side == MatchSide.East).Sum(r => r.Count), "East is at its cap");
    }

    [TestMethod]
    public void GetView_CarriesTheFactionAndItsTraits()
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Traits = new List<FactionTrait> { new("{=x}Strong", true), new("{=y}Weak", false) } };
        var match = new FordsMatch(Config(), Layout(), factions, Slots(), new SeededFordsRandom(1));

        var view = match.GetView(WestSlot);

        Assert.AreEqual("gondor", view.FactionId);
        Assert.AreEqual(2, view.Traits!.Count);
        Assert.IsTrue(view.Traits[0].Strength);
    }

    // The region deck.

    [TestMethod]
    public void Deck_ShowsEveryRegionOncePerCycle_AndTheNextIsWhatComes()
    {
        var deck = new RegionDeck(Rotation, new SeededFordsRandom(3));
        var seen = new List<string> { deck.Current.Id };

        for (var i = 0; i < Rotation.Regions.Count - 1; i++)
        {
            var next = deck.Next.Id;
            deck.Tick(30f);
            Assert.AreEqual(next, deck.Current.Id);
            seen.Add(deck.Current.Id);
        }

        CollectionAssert.AreEquivalent(new[] { "r1", "r2", "r3" }, seen);
    }

    [TestMethod]
    public void Deck_ANewCycleNeverRepeatsTheCurrentRegionAtOnce()
    {
        foreach (var seed in Enumerable.Range(0, 40))
        {
            var deck = new RegionDeck(Rotation, new SeededFordsRandom(seed));
            for (var i = 0; i < 30; i++)
            {
                var current = deck.Current.Id;
                deck.Tick(30f);
                Assert.AreNotEqual(current, deck.Current.Id, $"seed {seed} step {i}");
            }
        }
    }

    [TestMethod]
    public void Deck_TimerCountsDownAndRestarts()
    {
        var deck = new RegionDeck(Rotation, new SeededFordsRandom(3));
        var first = deck.Current.Id;

        deck.Tick(29f);

        Assert.AreEqual(first, deck.Current.Id);
        Assert.AreEqual(1f, deck.SecondsLeft, 0.001f);

        deck.Tick(2f);

        Assert.AreNotEqual(first, deck.Current.Id);
        Assert.AreEqual(29f, deck.SecondsLeft, 0.001f);
    }

    [TestMethod]
    public void Deck_Call_ShowsThatRegionAndRestartsTheTimer_AndTheNextStaysDifferent()
    {
        var deck = new RegionDeck(Rotation, new SeededFordsRandom(3));
        deck.Tick(10f);
        var target = Rotation.Regions.First(r => r.Id != deck.Current.Id).Id;

        Assert.IsTrue(deck.Call(target));

        Assert.AreEqual(target, deck.Current.Id);
        Assert.AreEqual(30f, deck.SecondsLeft, 0.001f);
        Assert.AreNotEqual(target, deck.Next.Id);
        Assert.IsFalse(deck.Call("nowhere"));
    }

    [TestMethod]
    public void Offers_AreTheCoreThenTheCurrentRegion_InFixedSlots()
    {
        var match = Match(rotation: Rotation);
        var core = Factions()[MatchSide.West].Units.Count;

        var view = match.GetView(WestSlot);

        Assert.AreEqual(core + 2, view.UnitOffers.Count);
        var region = view.Region!;
        StringAssert.StartsWith(view.UnitOffers[core].OfferId, region.Id);

        match.Tick(Snap(31f));

        var after = match.GetView(WestSlot);
        Assert.AreEqual(core + 2, after.UnitOffers.Count);
        Assert.AreNotEqual(region.Id, after.Region!.Id);
        StringAssert.StartsWith(after.UnitOffers[core].OfferId, after.Region.Id);
    }

    [TestMethod]
    public void TryBuyPack_AnOfferFromAnotherRegion_IsRefused()
    {
        var match = Match(rotation: Rotation);
        var absent = Rotation.Regions.First(r => r.Id != match.GetView(WestSlot).Region!.Id).Units[0].OfferId;

        Assert.AreEqual(CommandResult.UnknownOffer, match.TryBuyPack(WestSlot, 0, absent));
    }

    [TestMethod]
    public void TryCallRegion_PaysTheFeeAndShowsTheRegion()
    {
        var match = Match(rotation: Rotation);
        var view = match.GetView(WestSlot);
        var target = view.Region!.Choices.First(c => c.Id != view.Region.Id).Id;

        Assert.AreEqual(CommandResult.Ok, match.TryCallRegion(WestSlot, target));

        var after = match.GetView(WestSlot);
        Assert.AreEqual(target, after.Region!.Id);
        Assert.AreEqual(view.Gold - 150, after.Gold);
    }

    [TestMethod]
    public void TryCallRegion_TheRegionAlreadyShown_IsFree()
    {
        var match = Match(rotation: Rotation);
        var view = match.GetView(WestSlot);

        Assert.AreEqual(CommandResult.Ok, match.TryCallRegion(WestSlot, view.Region!.Id));

        Assert.AreEqual(view.Gold, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void TryCallRegion_RefusesWithoutGold_ForAnUnknownRegion_AndForAFactionWithoutRegions()
    {
        var poor = Match(rotation: Rotation with { CallFee = 999999 });
        var view = poor.GetView(WestSlot);
        var target = view.Region!.Choices.First(c => c.Id != view.Region.Id).Id;

        Assert.AreEqual(CommandResult.NotEnoughGold, poor.TryCallRegion(WestSlot, target));
        Assert.AreEqual(CommandResult.UnknownOffer, poor.TryCallRegion(WestSlot, "nowhere"));
        Assert.AreEqual(CommandResult.UnknownOffer, Match().TryCallRegion(WestSlot, "r1"));
        Assert.IsNull(Match().GetView(WestSlot).Region);
    }

    // The creature call.

    private static readonly CreatureCall Beasts = new(30f, new List<UnitOffer> { Warg, TrollCall });

    [TestMethod]
    public void TryCallCreature_PaysQueuesAndStartsTheSharedTimer()
    {
        var match = Match(creatures: Beasts);
        var gold = match.GetView(WestSlot).Gold;

        Assert.AreEqual(CommandResult.Ok, match.TryCallCreature(WestSlot, 1, "wargs"));

        var view = match.GetView(WestSlot);
        Assert.AreEqual(gold - 90, view.Gold);
        Assert.AreEqual(30f, view.Creatures!.CooldownLeft, 0.001f);
        Assert.AreEqual(CommandResult.OnCooldown, match.TryCallCreature(WestSlot, 1, "troll_call"), "the timer is shared");

        var spawned = match.Tick(Snap(1f)).Spawns;
        Assert.IsTrue(spawned.Any(s => s.TroopId == "warg" && s.Lane == 1 && s.Side == MatchSide.West));
    }

    [TestMethod]
    public void TryCallCreature_ReadyAgainAfterTheTimer()
    {
        var match = Match(creatures: Beasts, config: Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } }));
        match.TryCallCreature(WestSlot, 0, "wargs");

        match.Tick(Snap(31f));

        Assert.AreEqual(0f, match.GetView(WestSlot).Creatures!.CooldownLeft, 0.001f);
        Assert.AreEqual(CommandResult.Ok, match.TryCallCreature(WestSlot, 0, "troll_call"));
    }

    [TestMethod]
    public void TryCallCreature_RefusesWithoutGold_AnUnknownCreature_AndAFactionWithoutTheCall()
    {
        var match = Match(creatures: new CreatureCall(30f, new List<UnitOffer> { Warg with { PackPrice = 999999 } }));

        Assert.AreEqual(CommandResult.NotEnoughGold, match.TryCallCreature(WestSlot, 0, "wargs"));
        Assert.AreEqual(0f, match.GetView(WestSlot).Creatures!.CooldownLeft, "a refused call starts no timer");
        Assert.AreEqual(CommandResult.UnknownOffer, match.TryCallCreature(WestSlot, 0, "dragon"));
        Assert.AreEqual(CommandResult.UnknownOffer, Match().TryCallCreature(WestSlot, 0, "wargs"));
        Assert.IsNull(Match().GetView(WestSlot).Creatures);
    }

    [TestMethod]
    public void GoldTotals_SayWhatEachPurchasePaidFor()
    {
        var match = Match(creatures: Beasts);

        match.TryCallCreature(WestSlot, 0, "wargs");
        match.TryBuyHero(WestSlot, 0, HeroB.HeroId);

        var text = match.GoldTotals().Single(g => g.Slot == WestSlot).Breakdown;
        StringAssert.Contains(text, "creatures=" + Warg.PackPrice);
        StringAssert.Contains(text, "heroes=" + HeroB.Price);
        StringAssert.Contains(text, "units=0");
    }

    [TestMethod]
    public void TheAi_DoesNotCallWhileTheTimerRuns()
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { Creatures = Beasts, AiBuyCycle = new[] { "beast:troll_call" } };
        var match = new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } }), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
        match.TryCallCreature(EastSlot, 0, "wargs");
        match.Tick(Snap(0.5f));

        var ai = new AiCommander(EastSlot, new FordsAiProfile("hard", 5f, 1.2f, 0f, 400), new SeededFordsRandom(3));

        Assert.IsFalse(ai.Decide(match.GetView(EastSlot), 5f).Any(a => a.Kind == AiActionKind.CallCreature));
    }
}
