using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The feats that give Dale, Dol Guldur, the Easterlings and Umbar their own way to win.</summary>
[TestClass]
public class FactionFeatsBatchTwoTests
{
    private static readonly FordsMatchConfig C = FordsMatchConfig.Default;

    private static FordsMatch MatchWith(FactionFeats west, IReadOnlyList<CampSlot>? camps = null)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Feats = west };
        var layout = camps == null ? Layout() : Layout() with { Camps = camps };
        return new FordsMatch(Config(), layout, factions, Slots("slow"), new SeededFordsRandom(7));
    }

    private static IReadOnlyDictionary<MatchSide, FactionFeats> West(FactionFeats feats) =>
        new Dictionary<MatchSide, FactionFeats> { [MatchSide.West] = feats };

    private static TowerHit Blow(bool ranged, MatchSide side) =>
        new(side == MatchSide.West ? FordsTestLayout.EastOuter0 : FordsTestLayout.WestOuter0, ranged, UnitRole.Infantry, 100, side);

    [TestMethod]
    public void RangedDamageToTowers_ScalesOnlyTheOwnersMissiles()
    {
        var match = MatchWith(new FactionFeats { RangedDamageToTowers = 4f });
        var plain = MatchWith(new FactionFeats());

        var strong = match.AdjustTowerDamage(Blow(true, MatchSide.West), out _);
        var weak = plain.AdjustTowerDamage(Blow(true, MatchSide.West), out _);

        Assert.AreEqual(weak * 4, strong);
        Assert.AreEqual(plain.AdjustTowerDamage(Blow(false, MatchSide.West), out _), match.AdjustTowerDamage(Blow(false, MatchSide.West), out _), "melee is unchanged");
        Assert.AreEqual(weak, match.AdjustTowerDamage(Blow(true, MatchSide.East), out _), "the other side's missiles are unchanged");
    }

    [TestMethod]
    public void EnemyTowerFireRate_SlowsOnlyTheOpponentsTowers()
    {
        var feats = West(new FactionFeats { EnemyTowerFireRate = 0.5f });
        var fire = new TowerFireRules(C.Towers, new TowerChain(FordsTestLayout.Create(), C.Towers, feats), feats);
        var west = 0;
        var east = 0;
        for (var i = 0; i < 80; i++)
        {
            var volleys = fire.Tick(0.5f, new[] { Threat(FordsTestLayout.WestOuter0, 1), Threat(FordsTestLayout.EastOuter0, 1) });
            west += volleys.Count(v => v.TowerId == FordsTestLayout.WestOuter0);
            east += volleys.Count(v => v.TowerId == FordsTestLayout.EastOuter0);
        }

        Assert.IsTrue(east > 0);
        Assert.AreEqual(2.0, (double)west / east, 0.25, "the enemy's towers fire half as often");
    }

    [TestMethod]
    public void OrderCooldown_ScalesOnlyTheOwnersOrders()
    {
        var book = new LaneOrderBook(C.Lanes, West(new FactionFeats { OrderCooldown = 0.5f }));

        Assert.AreEqual(CommandResult.Ok, book.TrySet(MatchSide.West, 0, LaneOrder.Hold, 100f));
        Assert.AreEqual(CommandResult.Ok, book.TrySet(MatchSide.East, 0, LaneOrder.Hold, 100f));

        Assert.AreEqual(C.Lanes.OrderCooldownSeconds * 0.5f, book.CooldownLeft(MatchSide.West, 0, 100f), 0.001f);
        Assert.AreEqual(C.Lanes.OrderCooldownSeconds, book.CooldownLeft(MatchSide.East, 0, 100f), 0.001f);
    }

    [TestMethod]
    public void GetView_OrderCooldown_ShowsTheFactionsOwnCooldown()
    {
        var match = MatchWith(new FactionFeats { OrderCooldown = 0.5f });

        Assert.AreEqual(Config().Lanes.OrderCooldownSeconds * 0.5f, match.GetView(WestSlot).OrderCooldownSeconds, 0.001f);
        Assert.AreEqual(Config().Lanes.OrderCooldownSeconds, match.GetView(EastSlot).OrderCooldownSeconds, 0.001f);
    }

    [TestMethod]
    public void CampReward_PaysTheClearingSideMore_AndTheCampViewSaysSo()
    {
        var camp = new CampSlot("camp_0", new Vec3f(300f, 25f, 0f), "wolf", 1, 250);
        var match = MatchWith(new FactionFeats { CampReward = 3f }, new[] { camp });
        match.RegisterUnit(1, Unit(10, MatchSide.West, WestSlot));
        var gold = match.GetView(WestSlot).Gold;

        Assert.AreEqual(750, match.GetView(WestSlot).Camps!.Single().Reward);
        Assert.AreEqual(250, match.GetView(EastSlot).Camps!.Single().Reward);

        match.OnCampUnitKilled("camp_0", Key(10));

        Assert.AreEqual(gold + 750, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void CollectIncome_WithAFactor_PaysThatShare()
    {
        var rules = new CapturePointRules(C.Capture, C.Economy, new List<CapturePointSlot> { new("point_0", new Vec3f(0f, 0f, 0f)) });
        rules.Tick(25f, new List<CaptureCounts> { new("point_0", 1, 0) });

        Assert.AreEqual(2 * C.Economy.CaptureIncomePerMinute, rules.CollectIncome(MatchSide.West, 60f, 2f));
    }
}
