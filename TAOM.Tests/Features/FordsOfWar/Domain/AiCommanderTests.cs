using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The AI commander on its own: pacing, mistakes, and what it asks for from a given view.</summary>
[TestClass]
public class AiCommanderTests
{
    private static readonly FordsAiProfile Hard = new("hard", 5f, 1.2f, 0f, 400);

    private static AiCommander Ai(FordsAiProfile? profile = null, int seed = 3) =>
        new(EastSlot, profile ?? Hard, new SeededFordsRandom(seed));

    [TestMethod]
    public void Decide_BeforeTheFirstInterval_ReturnsNothing()
    {
        var view = Create().GetView(EastSlot);

        Assert.AreEqual(0, Ai().Decide(view, 4.9f).Count);
    }

    [TestMethod]
    public void Decide_AtTheInterval_ActsOnceThenWaitsForTheNext()
    {
        var view = Create().GetView(EastSlot);
        var ai = Ai();

        var first = ai.Decide(view, 5f);
        var tooSoon = ai.Decide(view, 9f);
        var next = ai.Decide(view, 10f);

        Assert.IsTrue(first.Count > 0);
        Assert.AreEqual(0, tooSoon.Count);
        Assert.IsTrue(next.Count > 0);
    }

    [TestMethod]
    public void Decide_MistakeChanceOne_AlwaysSkips()
    {
        var view = Create().GetView(EastSlot);
        var ai = Ai(new FordsAiProfile("fumble", 5f, 1f, 1f, 300));

        Assert.AreEqual(0, ai.Decide(view, 5f).Count);
        Assert.AreEqual(0, ai.Decide(view, 10f).Count);
        Assert.AreEqual(0, ai.Decide(view, 15f).Count);
    }

    [TestMethod]
    public void Decide_MatchStillInCountdown_ReturnsNothing()
    {
        var view = Create(Config(c => c with { CountdownSeconds = 30f })).GetView(EastSlot);

        Assert.AreEqual(0, Ai().Decide(view, 5f).Count);
    }

    [TestMethod]
    public void Decide_ViewWithNoLanesOrOffers_ReturnsNothing()
    {
        var view = Create().GetView(99);

        Assert.AreEqual(0, Ai().Decide(view, 5f).Count);
    }

    [TestMethod]
    public void Decide_ThreatenedLane_BuysTheCheapestSpearOrInfantryPackFirst()
    {
        var match = Create();
        match.Tick(Snap(0.5f, targets: new[] { Threat(FordsTestLayout.EastOuter1, 6) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        var first = actions.First();
        Assert.AreEqual(AiActionKind.BuyPack, first.Kind);
        Assert.AreEqual(1, first.Lane);
        Assert.AreEqual("inf", first.Id);
    }

    [TestMethod]
    public void Decide_ThreatBelowOwnCount_DoesNotDefend()
    {
        var match = Create();
        match.Tick(Snap(0.5f, new[] { Live(MatchSide.East, 1, 10) }, new[] { Threat(FordsTestLayout.EastOuter1, 4) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.BuyPack && a.Id == "inf"));
    }

    [TestMethod]
    public void Decide_WorstThreatenedLaneComesFirst()
    {
        var match = Create();
        var targets = new[] { Threat(FordsTestLayout.EastOuter0, 3), Threat(FordsTestLayout.EastOuter1, 9) };
        match.Tick(Snap(0.5f, targets: targets));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.AreEqual(1, actions.First(a => a.Kind == AiActionKind.BuyPack).Lane);
    }

    [TestMethod]
    public void Decide_NoThreat_HoldsTheAssaultLaneAndBuysThere()
    {
        var match = Create();
        match.Tick(Snap(0.5f));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0 && a.Order == LaneOrder.Hold));
        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.BuyPack && a.Lane == 0));
    }

    [TestMethod]
    public void Decide_AssaultLaneAtTheThreshold_StopsBuyingThere()
    {
        var match = Create(slots: Slots("slow"));
        match.TrySetLaneOrder(EastSlot, 0, LaneOrder.Hold);
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 40) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 30f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0 && a.Order == LaneOrder.Push));
        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.BuyPack && a.Lane == 0));
    }

    [TestMethod]
    public void Decide_LaneOrderOnCooldown_DoesNotRequestAnOrder()
    {
        var match = Create();
        match.TrySetLaneOrder(EastSlot, 0, LaneOrder.Hold);
        match.Tick(Snap(1f, new[] { Live(MatchSide.East, 0, 60) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.SetOrder));
    }

    [TestMethod]
    public void Decide_RichWithAFreeSlot_FieldsTheMostExpensiveAffordableHeroInTheAssaultLane()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 3000 } }));
        match.Tick(Snap(0.5f));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        var hero = actions.Single(a => a.Kind == AiActionKind.BuyHero);
        Assert.AreEqual("hero_a", hero.Id);
        Assert.AreEqual(0, hero.Lane);
    }

    [TestMethod]
    public void Decide_HeroSlotsFull_FieldsNoHero()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 5000 } }));
        match.TryBuyHero(EastSlot, 0, "hero_a");
        match.TryBuyHero(EastSlot, 0, "hero_b");
        match.Tick(Snap(0.5f));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.BuyHero));
    }

    [TestMethod]
    public void Decide_DeadHeroAndPlentyOfGold_BuysItBack()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 3000 } }), Slots("slow"));
        match.TryBuyHero(EastSlot, 0, "hero_a");
        match.Tick(Snap(0.5f));
        match.RegisterUnit(1, Unit(10, MatchSide.East, EastSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(10), null);

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        var back = actions.First(a => a.Kind == AiActionKind.Buyback);
        Assert.AreEqual("hero_a", back.Id);
    }

    [TestMethod]
    public void Decide_DeadHeroButPoor_DoesNotBuyBack()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 800 } }), Slots("slow"));
        match.TryBuyHero(EastSlot, 0, "hero_a");
        match.Tick(Snap(0.5f));
        match.RegisterUnit(1, Unit(10, MatchSide.East, EastSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(10), null);

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.Buyback));
    }

    /// <summary>An East match where hero_a was fielded, dies at 0.5 s (respawn timer ends near 60.5 s), then time moves on.</summary>
    private static FordsMatch DeadHeroMatch(float afterDeath, float respawnBase = 60f)
    {
        var config = Config(c => c with
        {
            Economy = c.Economy with { StartingGold = 3000 },
            Heroes = c.Heroes with { RespawnBaseSeconds = respawnBase },
        });
        var match = Create(config, Slots("slow"));
        match.TryBuyHero(EastSlot, 0, "hero_a");
        match.Tick(Snap(0.5f));
        match.RegisterUnit(1, Unit(10, MatchSide.East, EastSlot, role: UnitRole.Hero, heroId: "hero_a"));
        match.OnUnitKilled(Key(10), null);
        match.Tick(Snap(afterDeath));
        return match;
    }

    [TestMethod]
    public void Decide_HeroRespawnWellAhead_BuysItBack()
    {
        var match = DeadHeroMatch(44f);

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.Buyback && a.Id == "hero_a"));
    }

    [TestMethod]
    public void Decide_ViewSaysTheBuybackIsRefused_DoesNotBuyItBack()
    {
        var view = DeadHeroMatch(44f).GetView(EastSlot);
        var refused = view with { Heroes = view.Heroes.Select(h => h with { CanBuyback = false }).ToList() };

        var actions = Ai().Decide(refused, 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.Buyback));
    }

    [TestMethod]
    public void Decide_HeroRespawnInItsLastQuarter_DoesNotBuyItBack()
    {
        var match = DeadHeroMatch(46f);

        var actions = Ai().Decide(match.GetView(EastSlot), 5f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.Buyback));
    }

    [TestMethod]
    public void Decide_ShortRespawnDelay_UsesTheTenSecondFloor()
    {
        var ai = Ai();
        var early = DeadHeroMatch(8f, respawnBase: 20f);
        var late = DeadHeroMatch(12f, respawnBase: 20f);

        var buys = ai.Decide(early.GetView(EastSlot), 5f);
        var skips = ai.Decide(late.GetView(EastSlot), 10f);

        Assert.IsTrue(buys.Any(a => a.Kind == AiActionKind.Buyback));
        Assert.IsFalse(skips.Any(a => a.Kind == AiActionKind.Buyback));
    }

    /// <summary>East lane 0 is the assault lane (no threat, best ratio). Orders are applied the way the match would.</summary>
    private static (FordsMatch Match, AiCommander Ai) PushedAtTheThreshold()
    {
        var match = Create(slots: Slots("slow"));
        match.TrySetLaneOrder(EastSlot, 0, LaneOrder.Hold);
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 40) }));
        var ai = Ai();
        var first = ai.Decide(match.GetView(EastSlot), 5f);
        Assert.IsTrue(first.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0 && a.Order == LaneOrder.Push));
        match.TrySetLaneOrder(EastSlot, 0, LaneOrder.Push);
        return (match, ai);
    }

    [TestMethod]
    public void Decide_AssaultPushedAtTheThreshold_KeepsPushingWhileTheLaneStaysAboveTheFloor()
    {
        var (match, ai) = PushedAtTheThreshold();
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 30) }));

        var actions = ai.Decide(match.GetView(EastSlot), 10f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0));
    }

    [TestMethod]
    public void Decide_AssaultLaneFallsBelowThirtyFivePercent_HoldsToRebuild()
    {
        var (match, ai) = PushedAtTheThreshold();
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 13) }));

        var actions = ai.Decide(match.GetView(EastSlot), 10f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0 && a.Order == LaneOrder.Hold));
    }

    [TestMethod]
    public void Decide_LaneAtExactlyThirtyFivePercent_KeepsPushing()
    {
        var (match, ai) = PushedAtTheThreshold();
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 14) }));

        var actions = ai.Decide(match.GetView(EastSlot), 10f);

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0));
    }

    [TestMethod]
    public void Decide_TargetTowerFalls_ReleasesTheAssaultAndHoldsToRebuild()
    {
        var (match, ai) = PushedAtTheThreshold();
        match.OnTowerDestroyed(FordsTestLayout.WestOuter0, null);
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 30) }));

        var actions = ai.Decide(match.GetView(EastSlot), 10f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 0 && a.Order == LaneOrder.Hold));
    }

    [TestMethod]
    public void Decide_SameSeedAndViews_GiveTheSameActions()
    {
        var view = Create().GetView(EastSlot);
        var easy = new FordsAiProfile("easy", 12f, 0.8f, 0.5f, 200);
        var a = Ai(easy, 21);
        var b = Ai(easy, 21);

        for (var t = 12f; t < 400f; t += 12f)
            CollectionAssert.AreEqual(a.Decide(view, t).ToList(), b.Decide(view, t).ToList());
    }

    private static MatchView.UnitOfferView Pack(string offerId, string troopId, UnitRole role, int price) =>
        new(offerId, troopId, role, 5, price, true);

    private static IEnumerable<IReadOnlyList<T>> BothOrders<T>(params T[] items) =>
        new[] { (IReadOnlyList<T>)items.ToList(), items.Reverse().ToList() };

    [TestMethod]
    public void Decide_AssaultBuyWithEqualPrices_PicksTheSameTroopWhateverTheDeckOrder()
    {
        var view = Create().GetView(EastSlot);
        var chosen = new List<string>();

        foreach (var order in BothOrders(Pack("o_b", "troop_b", UnitRole.Infantry, 50), Pack("o_a", "troop_a", UnitRole.Ranged, 50)))
        {
            var actions = Ai().Decide(view with { UnitOffers = order.ToList() }, 5f);
            chosen.Add(actions.Single(a => a.Kind == AiActionKind.BuyPack).Id);
        }

        CollectionAssert.AreEqual(new[] { "o_a", "o_a" }, chosen);
    }

    [TestMethod]
    public void Decide_DefendBuyWithEqualPrices_PicksTheSameTroopWhateverTheDeckOrder()
    {
        var match = Create();
        match.Tick(Snap(0.5f, targets: new[] { Threat(FordsTestLayout.EastOuter1, 6) }));
        var view = match.GetView(EastSlot);
        var chosen = new List<string>();

        foreach (var order in BothOrders(Pack("o_b", "troop_b", UnitRole.Spear, 60), Pack("o_a", "troop_a", UnitRole.Infantry, 60)))
        {
            var actions = Ai().Decide(view with { UnitOffers = order.ToList() }, 5f);
            chosen.Add(actions.First(a => a.Kind == AiActionKind.BuyPack && a.Lane == 1).Id);
        }

        CollectionAssert.AreEqual(new[] { "o_a", "o_a" }, chosen);
    }

    [TestMethod]
    public void Decide_HeroOffersWithEqualPrices_PicksTheSameHeroWhateverTheDeckOrder()
    {
        var match = Create(Config(c => c with { Economy = c.Economy with { StartingGold = 3000 } }));
        match.Tick(Snap(0.5f));
        var view = match.GetView(EastSlot);
        var chosen = new List<string>();

        foreach (var order in BothOrders(new MatchView.HeroOfferView("hero_b", 500, true, false), new MatchView.HeroOfferView("hero_a", 500, true, false)))
        {
            var actions = Ai().Decide(view with { HeroOffers = order.ToList() }, 5f);
            chosen.Add(actions.Single(a => a.Kind == AiActionKind.BuyHero).Id);
        }

        CollectionAssert.AreEqual(new[] { "hero_a", "hero_a" }, chosen);
    }

    [TestMethod]
    public void Decide_TwoDeadHeroesAtTheSameBuybackPrice_BuysTheSameOneWhateverTheListOrder()
    {
        var view = DeadHeroMatch(44f).GetView(EastSlot);
        var dead = view.Heroes.Single(h => h.State == HeroState.Dead);
        var one = view with { HeroSlotsFree = 1 };
        var chosen = new List<string>();

        foreach (var order in BothOrders(dead with { HeroId = "hero_b" }, dead with { HeroId = "hero_a" }))
        {
            var actions = Ai().Decide(one with { Heroes = order.ToList() }, 5f);
            chosen.Add(actions.Single(a => a.Kind == AiActionKind.Buyback).Id);
        }

        CollectionAssert.AreEqual(new[] { "hero_a", "hero_a" }, chosen);
    }
}
