using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A faction's AI buys in its own order, so Mordor can field a horde and Rohan keep some foot soldiers. An entry is an offer id, or a role name that
/// means the dearest offer of that role the gold reaches. Without a cycle the AI buys the dearest offer it can afford.
/// </summary>
[TestClass]
public class AiBuyCycleTests
{
    private static readonly FordsAiProfile Hard = new("hard", 5f, 1.2f, 0f, 100000);

    private static FordsMatch Match(IReadOnlyList<string>? cycle, int gold = 5000)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = cycle };
        return new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = gold } }), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<string> BoughtOver(FordsMatch match, int decisions)
    {
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));
        var bought = new List<string>();
        for (var i = 1; i <= decisions; i++)
        {
            var view = match.GetView(EastSlot);
            bought.AddRange(ai.Decide(view, i * 10f).Where(a => a.Kind == AiActionKind.BuyPack).Select(a => a.Id));
        }

        return bought;
    }

    private static IReadOnlyList<UnitOffer> Offers => Factions()[MatchSide.East].Units;

    [TestMethod]
    public void Decide_WithACycle_BuysInItsOrder_AndWraps()
    {
        var first = Offers[0].OfferId;
        var second = Offers[1].OfferId;
        var match = Match(new[] { first, second, second });

        CollectionAssert.AreEqual(new[] { first, second, second, first }, BoughtOver(match, 4));
    }

    [TestMethod]
    public void Decide_WithoutACycle_BuysTheDearestItCanAfford()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First().OfferId;

        Assert.AreEqual(dearest, BoughtOver(Match(null), 1).Single());
    }

    [TestMethod]
    public void Decide_TheNextEntryCostsTooMuch_SavesForIt_AndBuysNothingCheaper()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First();
        var match = Match(new[] { dearest.OfferId }, gold: dearest.PackPrice - 1);

        CollectionAssert.AreEqual(new string[0], BoughtOver(match, 1));
    }

    [TestMethod]
    public void Decide_ARoleEntry_BuysTheDearestOfThatRoleTheGoldReaches()
    {
        var infantry = Offers.Where(o => o.Role == UnitRole.Infantry).OrderByDescending(o => o.PackPrice).ToList();
        Assert.IsTrue(infantry.Count > 1, "the fixture has two infantry offers");
        var match = Match(new[] { nameof(UnitRole.Infantry) });

        Assert.AreEqual(infantry[0].OfferId, BoughtOver(match, 1).Single());

        var poorer = Match(new[] { nameof(UnitRole.Infantry) }, gold: infantry[0].PackPrice - 1);

        Assert.AreEqual(infantry[1].OfferId, BoughtOver(poorer, 1).Single());
    }

    [TestMethod]
    public void Decide_AnEntryWithNothingOnShow_IsSkipped()
    {
        var first = Offers[0].OfferId;
        var match = Match(new[] { "not_on_show", first });

        Assert.AreEqual(first, BoughtOver(match, 1).Single());
    }

    private static FordsMatch BeastMatch(IReadOnlyList<string> cycle, int gold)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        var beasts = new CreatureCall(120f, new List<UnitOffer> { new("elephant", "t_elephant", UnitRole.Cavalry, 1, 300), new("mumak", "t_mumak", UnitRole.Cavalry, 1, 1000) });
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = cycle, Creatures = beasts };
        return new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = gold } }), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static List<AiAction> Decisions(FordsMatch match, int count)
    {
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));
        var actions = new List<AiAction>();
        for (var i = 1; i <= count; i++)
            actions.AddRange(ai.Decide(match.GetView(EastSlot), i * 10f));
        return actions;
    }

    [TestMethod]
    public void Decide_ABeastEntry_CallsThatBeast_WhenTheGoldReaches()
    {
        var calls = Decisions(BeastMatch(new[] { "beast:mumak" }, 1000), 1).Where(a => a.Kind == AiActionKind.CallCreature).ToList();

        Assert.AreEqual("mumak", calls.Single().Id);
    }

    [TestMethod]
    public void Decide_ABeastThatCostsTooMuch_SavesForIt_AndSpendsNothingElse()
    {
        var actions = Decisions(BeastMatch(new[] { "beast:mumak", Offers[0].OfferId }, 999), 1);

        Assert.AreEqual(0, actions.Count(a => a.Kind == AiActionKind.CallCreature || a.Kind == AiActionKind.BuyPack));
    }

    [TestMethod]
    public void Decide_ABeastOnCooldown_IsSkipped()
    {
        var match = BeastMatch(new[] { "beast:mumak", Offers[0].OfferId }, 5000);
        Assert.AreEqual(CommandResult.Ok, match.TryCallCreature(EastSlot, 0, "elephant"));

        var actions = Decisions(match, 1);

        Assert.AreEqual(0, actions.Count(a => a.Kind == AiActionKind.CallCreature));
        Assert.AreEqual(Offers[0].OfferId, actions.Single(a => a.Kind == AiActionKind.BuyPack).Id);
    }

    [TestMethod]
    public void Decide_WithoutABeastEntry_CallsNoBeast_SoItsGoldReachesTheBuyOrder()
    {
        var actions = Decisions(BeastMatch(new[] { Offers[0].OfferId }, 5000), 1);

        Assert.AreEqual(0, actions.Count(a => a.Kind == AiActionKind.CallCreature));
        Assert.AreEqual(1, actions.Count(a => a.Kind == AiActionKind.BuyPack));
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_BuysNoPack()
    {
        var actions = Decisions(FullLaneMatch(new[] { Offers[0].OfferId }), 1);

        Assert.AreEqual(0, actions.Count(a => a.Kind == AiActionKind.BuyPack));
    }

    private static FordsMatch FullLaneMatch(IReadOnlyList<string> cycle)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { AiBuyCycle = cycle };
        var config = Config(c => c with { Economy = c.Economy with { StartingGold = 5000 }, Lanes = c.Lanes with { AssaultThreshold = 0 } });
        return new FordsMatch(config, Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static readonly FordsAiProfile Spender = new("spender", 5f, 1f, 0f, 100);

    /// <summary>The slot's view with no enemy near, no hero slot free, a threshold of 70, and lane 0 holding <paramref name="first"/> units and every other lane <paramref name="others"/>.</summary>
    private static MatchView LanesHolding(FordsMatch match, int first, int others)
    {
        var view = match.GetView(EastSlot);
        var lanes = view.Lanes.Select(l => l with { OwnLive = l.Index == 0 ? first : others, EnemyLive = 0, Threat = 0 }).ToList();
        return view with { AssaultThreshold = 70, HeroSlotsFree = 0, Lanes = lanes };
    }

    private static List<AiAction> Spent(AiCommander ai, MatchView view, float now) =>
        ai.Decide(view, now).Where(a => a.Kind == AiActionKind.BuyPack || a.Kind == AiActionKind.BuyUpgrade).ToList();

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_SpareGoldBuysTheNextPack_ForTheLaneWithMostRoom()
    {
        var match = Match(new[] { Offers[0].OfferId });
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        var bought = Spent(ai, LanesHolding(match, 80, 10), 10f).Single();

        Assert.AreEqual(AiActionKind.BuyPack, bought.Kind);
        Assert.AreEqual(Offers[0].OfferId, bought.Id);
        Assert.AreNotEqual(0, bought.Lane, "lane 0 is full");
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_SpareGoldMovesTheCycleOn_SoItsUpgradesKeepTheirShare()
    {
        const string upgrade = "melee_damage";
        var match = Match(new[] { Offers[0].OfferId, Offers[1].OfferId, "up:" + upgrade });
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));
        var view = LanesHolding(match, 80, 10);

        Assert.AreEqual(Offers[0].OfferId, Spent(ai, view, 10f).Single().Id);
        Assert.AreEqual(Offers[1].OfferId, Spent(ai, view, 20f).Single().Id);
        var third = Spent(ai, view, 30f).Single();
        Assert.AreEqual(AiActionKind.BuyUpgrade, third.Kind);
        Assert.AreEqual(upgrade, third.Id);
    }

    [TestMethod]
    public void Decide_TheAssaultLaneIsFull_GoldInsideTheReserve_BuysNoPack()
    {
        var price = Offers[0].PackPrice;
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, LanesHolding(Match(new[] { Offers[0].OfferId }, gold: price + 100), 80, 10), 10f).Count, "the price and the reserve of 100 leave nothing spare");
        Assert.AreEqual(1, Spent(ai, LanesHolding(Match(new[] { Offers[0].OfferId }, gold: price + 101), 80, 10), 20f).Count);
    }

    [TestMethod]
    public void Decide_EveryLaneIsFull_SpareGoldWaits()
    {
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, LanesHolding(Match(new[] { Offers[0].OfferId }), 80, 70), 10f).Count);
    }

    [TestMethod]
    public void Decide_TheLaneHasRoom_BuysForTheAssaultLane_WithNoReserve()
    {
        var price = Offers[0].PackPrice;
        var ai = new AiCommander(EastSlot, Spender, new SeededFordsRandom(3));

        var bought = Spent(ai, LanesHolding(Match(new[] { Offers[0].OfferId }, gold: price), 40, 10), 10f).Single();

        Assert.AreEqual(0, bought.Lane, "lane 0 has the best odds, and it has room");
    }

    /// <summary>The slot's view with every lane under threat, lane 1 the worst, and no hero slot free, so no lane can lead an assault.</summary>
    private static MatchView AllLanesThreatened(FordsMatch match)
    {
        var view = match.GetView(EastSlot);
        var lanes = view.Lanes.Select(l => l with { OwnLive = 10, EnemyLive = 50, Threat = l.Index == 1 ? 60 : 30 }).ToList();
        return view with { AssaultThreshold = 70, HeroSlotsFree = 0, Lanes = lanes };
    }

    [TestMethod]
    public void Decide_ALaneUnderThreat_BuysTheCyclesNextEntry_ForThatLane_NotTheCheapest()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First();
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));

        var bought = Spent(ai, AllLanesThreatened(Match(new[] { dearest.OfferId })), 10f).Single();

        Assert.AreEqual(dearest.OfferId, bought.Id);
        Assert.AreEqual(1, bought.Lane, "lane 1 has the worst threat");
    }

    [TestMethod]
    public void Decide_EveryLaneUnderThreat_TheCycleMovesOn_SoASideThatIsBehindStillGetsItsUpgrades()
    {
        const string upgrade = "melee_damage";
        var match = Match(new[] { Offers[0].OfferId, "up:" + upgrade });
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));
        var view = AllLanesThreatened(match);

        Assert.AreEqual(Offers[0].OfferId, Spent(ai, view, 10f).Single().Id);
        var second = Spent(ai, view, 20f).Single();
        Assert.AreEqual(AiActionKind.BuyUpgrade, second.Kind);
        Assert.AreEqual(upgrade, second.Id);
    }

    [TestMethod]
    public void Decide_ALaneUnderThreat_TheNextEntryCostsTooMuch_SavesForIt_AndBuysNothingCheaper()
    {
        var dearest = Offers.OrderByDescending(o => o.PackPrice).First();
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));

        Assert.AreEqual(0, Spent(ai, AllLanesThreatened(Match(new[] { dearest.OfferId }, gold: dearest.PackPrice - 1)), 10f).Count);
    }

    [TestMethod]
    public void Decide_ALaneUnderThreat_ABeastDue_ComesToThatLane()
    {
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));

        var call = ai.Decide(AllLanesThreatened(BeastMatch(new[] { "beast:mumak" }, 1000)), 10f).Single(a => a.Kind == AiActionKind.CallCreature);

        Assert.AreEqual("mumak", call.Id);
        Assert.AreEqual(1, call.Lane);
    }

    [TestMethod]
    public void Decide_ALaneUnderThreat_WithoutACycle_StillBuysTheCheapestFoot()
    {
        var cheapestFoot = Offers.Where(o => o.Role == UnitRole.Spear || o.Role == UnitRole.Infantry).OrderBy(o => o.PackPrice).First();
        var ai = new AiCommander(EastSlot, Hard, new SeededFordsRandom(3));

        var bought = Spent(ai, AllLanesThreatened(Match(null)), 10f).Single();

        Assert.AreEqual(cheapestFoot.OfferId, bought.Id);
        Assert.AreEqual(1, bought.Lane);
    }

    [TestMethod]
    public void Decide_WithABeastEntry_MakesNoOtherCall_SoTheGoldIsNotSpentOnTheCheapestBeast()
    {
        var actions = Decisions(BeastMatch(new[] { "beast:mumak" }, 500), 1);

        Assert.AreEqual(0, actions.Count(a => a.Kind == AiActionKind.CallCreature));
    }

    [TestMethod]
    public void GetView_CarriesTheCycle()
    {
        var cycle = new[] { Offers[0].OfferId };

        CollectionAssert.AreEqual(cycle, Match(cycle).GetView(EastSlot).AiBuyCycle!.ToList());
        Assert.IsNull(Match(null).GetView(EastSlot).AiBuyCycle);
    }
}
