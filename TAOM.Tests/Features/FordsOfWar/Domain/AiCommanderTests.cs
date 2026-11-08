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

    [TestMethod]
    public void IsDue_IsFalseBeforeTheInterval_TrueAtIt_AndFalseAgainAfterADecision()
    {
        var ai = Ai();
        Assert.IsFalse(ai.IsDue(4.9f));
        Assert.IsTrue(ai.IsDue(5f));

        ai.Decide(FordsMatchViewForIsDue(), 5f);

        Assert.IsFalse(ai.IsDue(9f));
        Assert.IsTrue(ai.IsDue(10f));
    }

    private static MatchView FordsMatchViewForIsDue() => FordsMatchFixture.Create().GetView(FordsMatchFixture.EastSlot);

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
        var match = Create(Threshold40(), Slots("slow"));
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
    // The threshold tests below count units against 40 (35 percent of it is 14), so they pin it instead of the shipped default.
    private static FordsMatchConfig Threshold40() => Config(c => c with { Lanes = c.Lanes with { AssaultThreshold = 40 } });

    private static (FordsMatch Match, AiCommander Ai) PushedAtTheThreshold()
    {
        var match = Create(Threshold40(), Slots("slow"));
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

    // Seize: the same order a person gives, from the same view.

    private static FordsMatch SeizeMatch()
    {
        var layout = Layout() with { Camps = new List<CampSlot> { new("camp_0", new Vec3f(300f, 25f, 0f), "wolf", 2, 250) } };
        return new FordsMatch(Threshold40(), layout, Factions(), Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<AiAction> SeizeOrders(List<AiAction> actions) =>
        actions.Where(a => a.Kind == AiActionKind.SetOrder && a.Order == LaneOrder.Seize).ToList();

    [TestMethod]
    public void Decide_OpenObjectiveAndAFreeLane_SeizesWithThatLane()
    {
        var match = SeizeMatch();
        match.Tick(Snap(0.5f, new[] { Live(MatchSide.East, 0, 20), Live(MatchSide.East, 1, 20) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 5f).ToList();

        var seize = SeizeOrders(actions).Single();
        Assert.AreEqual(1, seize.Lane, "lane 0 is the assault lane, lane 1 is free");
    }

    [TestMethod]
    public void Decide_FreeLaneTooSmall_DoesNotSeize()
    {
        var match = SeizeMatch();
        match.Tick(Snap(0.5f, new[] { Live(MatchSide.East, 0, 20), Live(MatchSide.East, 1, 3) }));

        Assert.AreEqual(0, SeizeOrders(Ai().Decide(match.GetView(EastSlot), 5f).ToList()).Count);
    }

    [TestMethod]
    public void Decide_FreeLaneUnderPressure_DoesNotSeize()
    {
        var match = SeizeMatch();
        match.Tick(Snap(0.5f, new[] { Live(MatchSide.East, 0, 20), Live(MatchSide.East, 1, 20) },
            new[] { Threat(FordsTestLayout.EastOuter1, 2) }));

        Assert.AreEqual(0, SeizeOrders(Ai().Decide(match.GetView(EastSlot), 5f).ToList()).Count);
    }

    [TestMethod]
    public void Decide_ALaneAlreadySeizing_IsLeftAloneAndNoSecondLaneStarts()
    {
        var match = SeizeMatch();
        match.TrySetLaneOrder(EastSlot, 1, LaneOrder.Seize);
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 20), Live(MatchSide.East, 1, 20) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 30f).ToList();

        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 1));
        Assert.AreEqual(0, SeizeOrders(actions).Count);
    }

    [TestMethod]
    public void Decide_ASeizingLaneOutnumberedAtItsTower_GoesBackToPush()
    {
        var match = SeizeMatch();
        match.TrySetLaneOrder(EastSlot, 1, LaneOrder.Seize);
        match.Tick(Snap(25f, new[] { Live(MatchSide.East, 0, 20), Live(MatchSide.East, 1, 5) },
            new[] { Threat(FordsTestLayout.EastOuter1, 8) }));

        var actions = Ai().Decide(match.GetView(EastSlot), 30f).ToList();

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.SetOrder && a.Lane == 1 && a.Order == LaneOrder.Push));
    }

    // Regroup under your own tower: a lane outside the assault that the units near its lead badly outvalue falls back, and returns once the odds ease.

    /// <summary>One lane of a hand-made view. Own, Enemy and Count are the values near the lead; OwnLive and EnemyLive are the whole-lane headcounts the assault pick reads.</summary>
    private sealed record RegroupLane(int Index, LaneOrder Order, float Own, float Enemy, int Count, float Cooldown = 0f, bool AtTower = false,
        int OwnLive = 10, int EnemyLive = 10);

    private static RegroupLane Assault() => new(0, LaneOrder.Push, 100f, 50f, 3, OwnLive: 30, EnemyLive: 5);

    private static MatchView RegroupView(params RegroupLane[] set)
    {
        var view = Create(Config(), Slots("slow")).GetView(EastSlot);
        var lanes = view.Lanes
            .Select(l => set.Where(x => x.Index == l.Index)
                .Select(x => l with
                {
                    Order = x.Order, OwnLive = x.OwnLive, EnemyLive = x.EnemyLive, CooldownLeft = x.Cooldown,
                    NearOwnValue = x.Own, NearEnemyValue = x.Enemy, NearEnemyCount = x.Count, AtOwnTower = x.AtTower,
                })
                .FirstOrDefault() ?? l)
            .ToList();
        return view with { Lanes = lanes };
    }

    private static RegroupLane Pressed(LaneOrder order = LaneOrder.Push, float cooldown = 0f) =>
        new(1, order, 100f, 250f, 12, cooldown);

    private static AiAction? OrderFor(IEnumerable<AiAction> actions, int lane) =>
        actions.FirstOrDefault(a => a.Kind == AiActionKind.SetOrder && a.Lane == lane);

    /// <summary>The AI sends lane 1 back at 5 s, as the first step of a sequence.</summary>
    private static AiCommander ARegroupingAi()
    {
        var ai = Ai();
        var first = OrderFor(ai.Decide(RegroupView(Assault(), Pressed()), 5f), 1);
        Assert.AreEqual(LaneOrder.FallBack, first?.Order);
        return ai;
    }

    [TestMethod]
    public void Decide_NonAssaultLaneOutvaluedPastTheFallBackRatio_OrdersFallBack()
    {
        var order = OrderFor(Ai().Decide(RegroupView(Assault(), Pressed()), 5f), 1);

        Assert.IsNotNull(order);
        Assert.AreEqual(LaneOrder.FallBack, order!.Order);
        Assert.AreEqual("[FordsOfWar][Ai] regroup side=East lane=1 own=100.0 enemy=250.0 n=12", order.Note);
    }

    [TestMethod]
    public void Decide_NonAssaultLaneOutvaluedBelowTheFallBackRatio_KeepsItsOrder()
    {
        var view = RegroupView(Assault(), Pressed() with { Enemy = 150f });

        Assert.IsNull(OrderFor(Ai().Decide(view, 5f), 1));
    }

    // Headcount alone does not send a lane back: a full 40-unit elven lane against 100 is level near its lead.
    [TestMethod]
    public void Decide_LaneOutnumberedInHeadcountButLevelNearItsLead_IsNotOrderedToFallBack()
    {
        var view = RegroupView(Assault(), Pressed() with { Enemy = 100f, OwnLive = 40, EnemyLive = 100 });

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    // An empty lane has nobody to regroup.
    [TestMethod]
    public void Decide_ALaneWithNoValueNearItsLead_IsNotOrderedToFallBack()
    {
        var view = RegroupView(Assault(), Pressed() with { Own = 0f });

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    // A skirmish of a few soldiers is not an outnumbered lane, however the values compare.
    [TestMethod]
    public void Decide_ASmallSkirmishPastTheRatio_IsNotOrderedToFallBack()
    {
        var view = RegroupView(Assault(), Pressed() with { Count = AiCommander.RegroupMinEnemy - 1 });

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    [TestMethod]
    public void Decide_TheMinimumEnemyCountPastTheRatio_OrdersFallBack()
    {
        var view = RegroupView(Assault(), Pressed() with { Count = AiCommander.RegroupMinEnemy });

        Assert.AreEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsNeverOrderedToFallBack()
    {
        var view = RegroupView(Assault() with { Own = 100f, Enemy = 250f, Count = 12 }, Pressed() with { Enemy = 400f });

        var actions = Ai().Decide(view, 5f);

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(actions, 0)?.Order);
        Assert.AreEqual(LaneOrder.FallBack, OrderFor(actions, 1)?.Order);
    }

    [TestMethod]
    public void Decide_ASeizingLaneIsNeverOrderedToFallBack()
    {
        var view = RegroupView(Assault(), Pressed(LaneOrder.Seize));

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    [TestMethod]
    public void Decide_OutvaluedLaneOnCooldown_GetsNoOrder()
    {
        var view = RegroupView(Assault(), Pressed(cooldown: 3f));

        Assert.IsNull(OrderFor(Ai().Decide(view, 5f), 1));
    }

    [TestMethod]
    public void Decide_RegroupingLaneBetweenTheRatios_StaysOnFallBack()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 130f }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_RegroupingLaneAtTheReturnRatio_GoesBackToPush()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 110f }), 10f);

        var order = OrderFor(actions, 1);
        Assert.AreEqual(LaneOrder.Push, order?.Order);
        Assert.AreEqual("[FordsOfWar][Ai] regroup end side=East lane=1 own=100.0 enemy=110.0 n=12", order!.Note);
    }

    [TestMethod]
    public void Decide_RegroupingLaneAtItsTowerWithNobodyFollowing_GoesBackToPush()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { AtTower = true, Count = 0, Enemy = 0f }), 10f);

        Assert.AreEqual(LaneOrder.Push, OrderFor(actions, 1)?.Order);
    }

    [TestMethod]
    public void Decide_RegroupingLaneAtItsTowerWithEnemiesStillNear_StaysOnFallBack()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { AtTower = true, Count = 2 }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_RegroupingLaneWithNobodyFollowingButNotYetAtItsTower_StaysOnFallBack()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { AtTower = false, Count = 0, Enemy = 0f }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_RegroupingLaneReadyToReturnButOnCooldown_WaitsForTheCooldown()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack, cooldown: 4f) with { Enemy = 80f }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_AFallBackTheAiDidNotOrder_IsNeverReleased()
    {
        var view = RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 80f });

        Assert.IsNull(OrderFor(Ai().Decide(view, 5f), 1));
    }

    [TestMethod]
    public void Decide_APlayerFallBackAfterTheAiReleasedItsOwn_IsNeverReleased()
    {
        var ai = ARegroupingAi();
        var release = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 80f }), 10f);
        Assert.AreEqual(LaneOrder.Push, OrderFor(release, 1)?.Order);
        ai.Decide(RegroupView(Assault(), Pressed() with { Enemy = 80f }), 15f);

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 80f }), 20f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    // The AI asks for Push but the match refuses it (the order is still FallBack on the next visit): the lane is still the AI's to release.
    [TestMethod]
    public void Decide_ReleaseNotYetAccepted_IsAskedForAgain()
    {
        var ai = ARegroupingAi();
        var view = RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 80f });
        Assert.AreEqual(LaneOrder.Push, OrderFor(ai.Decide(view, 10f), 1)?.Order);

        Assert.AreEqual(LaneOrder.Push, OrderFor(ai.Decide(view, 15f), 1)?.Order);
    }

    // The AI sends a lane back, a person takes it to Seize, the AI visits, then the person sends it back: that last one is the person's.
    [TestMethod]
    public void Decide_AiFallBackThenPersonSeizeThenPersonFallBack_IsNeverReleased()
    {
        var ai = ARegroupingAi();
        ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.Seize) with { Enemy = 80f }), 10f);

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 80f }), 15f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_AssaultPick_SkipsALaneOnFallBack()
    {
        var view = RegroupView(Assault() with { Order = LaneOrder.FallBack }, new RegroupLane(1, LaneOrder.Push, 50f, 50f, 1));

        var actions = Ai().Decide(view, 5f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.BuyPack && a.Lane == 1));
        Assert.IsFalse(actions.Any(a => a.Kind == AiActionKind.BuyPack && a.Lane == 0));
    }

    [TestMethod]
    public void Decide_EveryLaneOnFallBackWithABuyCycle_StillBuysTheNextCycleEntry()
    {
        var view = RegroupView(Assault() with { Order = LaneOrder.FallBack }, new RegroupLane(1, LaneOrder.FallBack, 50f, 50f, 1));

        var actions = Ai().Decide(view with { AiBuyCycle = new[] { "inf" } }, 5f);

        Assert.IsTrue(actions.Any(a => a.Kind == AiActionKind.BuyPack));
    }

    [TestMethod]
    public void Tick_AiRegroupOrder_ReportsItsLogLineAndSetsTheLaneToFallBack()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(1, MatchSide.East, EastSlot, lane: 1, value: 100));
        for (var i = 0; i < 12; i++)
            match.RegisterUnit(1, Unit(100 + i, MatchSide.West, WestSlot, lane: 1, value: 200));

        var lanes = new[]
        {
            Live(MatchSide.East, 0, 20), Live(MatchSide.West, 0, 5),
            new LaneSnapshot(MatchSide.East, 1, 1, new Vec3f(700f, 50f, 0f), 10f, new[] { new Vec3f(700f, 50f, 0f) }, new[] { Key(1) }),
            new LaneSnapshot(MatchSide.West, 1, 12, new Vec3f(690f, 50f, 0f), 10f,
                Enumerable.Range(0, 12).Select(i => new Vec3f(690f, 50f, 0f)).ToList(), Enumerable.Range(0, 12).Select(i => Key(100 + i)).ToList()),
        };

        var effects = match.Tick(Snap(6f, lanes));

        CollectionAssert.Contains(effects.Notes, "[FordsOfWar][Ai] regroup side=East lane=1 own=100.0 enemy=2400.0 n=12");
        Assert.AreEqual(LaneOrder.FallBack, match.GetView(EastSlot).Lanes[1].Order);
    }

    // The view: value and count of the units within 40 m of the lane's lead, enemy units of any lane.

    private static FordsMatch PressureMatch(float eastLeadX, int westInRange = 12)
    {
        var match = Create(Config(), Slots("slow"));
        for (var i = 0; i < 3; i++)
            match.RegisterUnit(1, Unit(1 + i, MatchSide.East, EastSlot, lane: 1, value: 100));
        match.RegisterUnit(1, Unit(50, MatchSide.East, EastSlot, lane: 1, value: 999));
        for (var i = 0; i < 14; i++)
            match.RegisterUnit(1, Unit(100 + i, MatchSide.West, WestSlot, lane: i % 2, value: 50));

        var east = new List<Vec3f> { new(eastLeadX, 50f, 0f), new(eastLeadX, 50f, 0f), new(eastLeadX, 50f, 0f), new(eastLeadX + 300f, 50f, 0f) };
        var west = Enumerable.Range(0, 14).Select(i => i < westInRange ? new Vec3f(eastLeadX - 30f, 50f, 0f) : new Vec3f(100f, 50f, 0f)).ToList();
        var even = Enumerable.Range(0, 14).Where(i => i % 2 == 0).ToList();
        var odd = Enumerable.Range(0, 14).Where(i => i % 2 == 1).ToList();
        match.Tick(Snap(0.5f, new[]
        {
            new LaneSnapshot(MatchSide.East, 1, 4, new Vec3f(eastLeadX, 50f, 0f), 10f, east, new[] { Key(1), Key(2), Key(3), Key(50) }),
            new LaneSnapshot(MatchSide.West, 0, 7, new Vec3f(eastLeadX, 0f, 0f), 10f, even.Select(i => west[i]).ToList(), even.Select(i => Key(100 + i)).ToList()),
            new LaneSnapshot(MatchSide.West, 1, 7, new Vec3f(eastLeadX, 50f, 0f), 10f, odd.Select(i => west[i]).ToList(), odd.Select(i => Key(100 + i)).ToList()),
        }));
        return match;
    }

    [TestMethod]
    public void View_LaneWithALead_CountsValueAndEnemiesWithinThePressureRadiusOfTheLead()
    {
        var lane = PressureMatch(700f).GetView(EastSlot).Lanes[1];

        Assert.AreEqual(300f, lane.NearOwnValue, "the far straggler is outside the radius");
        Assert.AreEqual(600f, lane.NearEnemyValue, "12 West units at 50, from either lane");
        Assert.AreEqual(12, lane.NearEnemyCount);
    }

    [TestMethod]
    public void View_EnemyBeyondThePressureRadius_IsNotCounted()
    {
        var lane = PressureMatch(700f, westInRange: 0).GetView(EastSlot).Lanes[1];

        Assert.AreEqual(0, lane.NearEnemyCount);
        Assert.AreEqual(0f, lane.NearEnemyValue);
    }

    [TestMethod]
    public void View_LaneWithoutPositions_HasNoPressureNumbers()
    {
        var match = Create(Config(), Slots("slow"));
        match.Tick(Snap(0.5f, new[] { Live(MatchSide.East, 1, 10), Live(MatchSide.West, 1, 20) }));

        var lane = match.GetView(EastSlot).Lanes[1];

        Assert.AreEqual(0f, lane.NearOwnValue);
        Assert.AreEqual(0f, lane.NearEnemyValue);
        Assert.AreEqual(0, lane.NearEnemyCount);
        Assert.IsFalse(lane.AtOwnTower);
    }

    [TestMethod]
    public void View_LeadWithinFireRangeOfTheOuterOwnTower_IsAtOwnTower_AndBeyondIsNot()
    {
        Assert.IsTrue(PressureMatch(660f).GetView(EastSlot).Lanes[1].AtOwnTower, "East outer tower at 640");
        Assert.IsFalse(PressureMatch(700f).GetView(EastSlot).Lanes[1].AtOwnTower, "60 m from it");
    }

    // The release rule: favourable contact, a lane that reached its tower with nobody behind it, or a lane with nobody left.

    [TestMethod]
    public void Decide_RegroupingLaneWithNobodyLeft_GoesBackToPush()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Own = 0f, OwnLive = 0, Enemy = 0f, Count = 0 }), 10f);

        Assert.AreEqual(LaneOrder.Push, OrderFor(actions, 1)?.Order);
    }

    // The lane outran the enemy but is not home: with nobody in contact the ratio proves nothing.
    [TestMethod]
    public void Decide_RegroupingLaneWithNoEnemyNearAndAwayFromItsTower_StaysOnFallBack()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(), Pressed(LaneOrder.FallBack) with { Enemy = 0f, Count = 0, AtTower = false }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    // On a retreat the lead is the rear guard: the first few pursuers are not contact, so the lane keeps going home.
    [TestMethod]
    public void Decide_RegroupingLaneAwayFromItsTowerWithAFewPursuersNear_StaysOnFallBack()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(),
            Pressed(LaneOrder.FallBack) with { Enemy = 20f, Count = AiCommander.RegroupMinEnemy - 1, AtTower = false }), 10f);

        Assert.IsNull(OrderFor(actions, 1));
    }

    [TestMethod]
    public void Decide_RegroupingLaneWithTheMinimumEnemyCountAtTheReturnRatio_GoesBackToPush()
    {
        var ai = ARegroupingAi();

        var actions = ai.Decide(RegroupView(Assault(),
            Pressed(LaneOrder.FallBack) with { Enemy = 110f, Count = AiCommander.RegroupMinEnemy }), 10f);

        Assert.AreEqual(LaneOrder.Push, OrderFor(actions, 1)?.Order);
    }

    // The same through the match: the enemy is beyond 40 m and the lead is 60 m from its tower, so the lane stays back.
    [TestMethod]
    public void Tick_RegroupingLaneWithTheEnemyBeyondThePressureRadius_StaysOnFallBack()
    {
        var match = Create();
        match.RegisterUnit(1, Unit(1, MatchSide.East, EastSlot, lane: 1, value: 100));
        for (var i = 0; i < 12; i++)
            match.RegisterUnit(1, Unit(100 + i, MatchSide.West, WestSlot, lane: 1, value: 200));

        LaneSnapshot[] Lanes(float westX) => new[]
        {
            Live(MatchSide.East, 0, 20), Live(MatchSide.West, 0, 5),
            new LaneSnapshot(MatchSide.East, 1, 1, new Vec3f(700f, 50f, 0f), 10f, new[] { new Vec3f(700f, 50f, 0f) }, new[] { Key(1) }),
            new LaneSnapshot(MatchSide.West, 1, 12, new Vec3f(westX, 50f, 0f), 10f,
                Enumerable.Range(0, 12).Select(i => new Vec3f(westX, 50f, 0f)).ToList(), Enumerable.Range(0, 12).Select(i => Key(100 + i)).ToList()),
        };

        match.Tick(Snap(6f, Lanes(690f)));
        Assert.AreEqual(LaneOrder.FallBack, match.GetView(EastSlot).Lanes[1].Order);

        match.Tick(Snap(60f, Lanes(300f)));
        var lane = match.GetView(EastSlot).Lanes[1];

        Assert.AreEqual(0, lane.NearEnemyCount);
        Assert.IsFalse(lane.AtOwnTower);
        Assert.AreEqual(LaneOrder.FallBack, lane.Order);
    }

    // Pressure: heroes weigh a fixed amount, and the units physically near the lead count whichever lane they belong to.

    private sealed record FieldUnit(MatchSide Side, int Lane, int Key, int Value, float X, UnitRole Role = UnitRole.Infantry);

    private static FordsMatch Field(params FieldUnit[] units)
    {
        var match = Create(Config(), Slots("slow"));
        foreach (var u in units)
            match.RegisterUnit(1, Unit(u.Key, u.Side, u.Side == MatchSide.East ? EastSlot : WestSlot, u.Lane, u.Value, u.Role,
                u.Role == UnitRole.Hero ? "hero_" + u.Key : null));

        var lanes = units.GroupBy(u => (u.Side, u.Lane)).Select(g => new LaneSnapshot(g.Key.Side, g.Key.Lane, g.Count(),
            new Vec3f(g.First().X, 50f, 0f), 10f, g.Select(u => new Vec3f(u.X, 50f, 0f)).ToList(), g.Select(u => Key(u.Key)).ToList())).ToList();
        if (!units.Any(u => u.Side == MatchSide.East && u.Lane == 0))
            lanes.Add(Live(MatchSide.East, 0, 100));

        match.Tick(Snap(0.5f, lanes));
        return match;
    }

    private static IEnumerable<FieldUnit> Troops(MatchSide side, int lane, int firstKey, int count, int value, float x) =>
        Enumerable.Range(0, count).Select(i => new FieldUnit(side, lane, firstKey + i, value, x));

    [TestMethod]
    public void View_AnEnemyHeroNearTheLead_CountsAsHeroPressureValueWhateverItsPrice()
    {
        var units = Troops(MatchSide.East, 1, 1, 30, 10, 700f)
            .Concat(Troops(MatchSide.West, 1, 100, 9, 10, 690f))
            .Append(new FieldUnit(MatchSide.West, 1, 200, 2000, 690f, UnitRole.Hero));

        var lane = Field(units.ToArray()).GetView(EastSlot).Lanes[1];

        Assert.AreEqual(9 * 10f + FordsMatch.HeroPressureValue, lane.NearEnemyValue);
        Assert.AreEqual(10, lane.NearEnemyCount);
    }

    [TestMethod]
    public void Decide_OneEnemyHeroPlusNineCheapUnitsAgainstAThirtyUnitLane_DoesNotSendItBack()
    {
        var units = Troops(MatchSide.East, 1, 1, 30, 10, 700f)
            .Concat(Troops(MatchSide.West, 1, 100, 9, 10, 690f))
            .Append(new FieldUnit(MatchSide.West, 1, 200, 2000, 690f, UnitRole.Hero));
        var view = Field(units.ToArray()).GetView(EastSlot);

        Assert.AreNotEqual(LaneOrder.FallBack, OrderFor(Ai().Decide(view, 5f), 1)?.Order);
    }

    [TestMethod]
    public void View_AnOwnHeroNearTheLead_CountsAsHeroPressureValue()
    {
        var units = Troops(MatchSide.East, 1, 1, 2, 10, 700f).Append(new FieldUnit(MatchSide.East, 1, 50, 900, 700f, UnitRole.Hero));

        var lane = Field(units.ToArray()).GetView(EastSlot).Lanes[1];

        Assert.AreEqual(20f + FordsMatch.HeroPressureValue, lane.NearOwnValue);
    }

    [TestMethod]
    public void View_OwnUnitsOfAnotherLaneNearTheLead_CountToo()
    {
        var units = Troops(MatchSide.East, 1, 1, 3, 100, 700f).Concat(Troops(MatchSide.East, 0, 50, 2, 40, 710f)).Concat(Troops(MatchSide.East, 0, 60, 2, 40, 100f));

        var lane = Field(units.ToArray()).GetView(EastSlot).Lanes[1];

        Assert.AreEqual(380f, lane.NearOwnValue, "three of lane 1 and the two of lane 0 within 40 m; the two at 100 m are out");
    }
}
