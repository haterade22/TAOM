using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>
/// A tower range feat reaches every rule that reads a tower's fire range, so the fire, the local counts, the siege and the hold point agree.
/// A held lane stands back by the extra reach: a lane on Hold under fire only dies, and it has no order that answers the fire.
/// </summary>
[TestClass]
public class TowerRangeFeatTests
{
    private static readonly FordsMatchConfig C = FordsMatchConfig.Default;
    private static readonly float Base = C.Towers.FireRange;

    private static IReadOnlyDictionary<MatchSide, FactionFeats> EastRange(float factor) =>
        new Dictionary<MatchSide, FactionFeats> { [MatchSide.East] = new() { TowerRange = factor } };

    private static TowerChain Chain(float factor = 1.5f) => new(FordsTestLayout.Create(), C.Towers, EastRange(factor));

    private static LaneSituation Situation(LaneOrder order, float towerDistance, int attackers = -1, int defenders = -1, float nearestEnemy = float.MaxValue) =>
        new(MatchSide.West, 0, order, 10, new Vec3f(100f, 0f, 0f), nearestEnemy, FordsTestLayout.EastOuter0, towerDistance, false,
            null, 0f, 0, float.MaxValue, attackers, defenders);

    [TestMethod]
    public void FireRange_ScalesWithTheOwnersFeatOnly()
    {
        var chain = Chain();

        Assert.AreEqual(Base * 1.5f, chain.FireRange(FordsTestLayout.EastOuter0), 0.001f);
        Assert.AreEqual(Base, chain.FireRange(FordsTestLayout.WestOuter0), 0.001f);
    }

    [TestMethod]
    public void Tick_AnEnemyPastTheBaseRange_OnlyTheLongRangeOwnerFires()
    {
        var chain = Chain();
        var fire = new TowerFireRules(C.Towers, chain, EastRange(1.5f));
        var past = Base * 1.25f;

        var volleys = fire.Tick(1f, new[] { Threat(FordsTestLayout.EastOuter0, 1, past), Threat(FordsTestLayout.WestOuter0, 1, past) });

        CollectionAssert.AreEqual(new[] { FordsTestLayout.EastOuter0 }, volleys.Select(v => v.TowerId).ToArray());
    }

    [TestMethod]
    public void Tick_AnEnemyPastTheLongRange_NoTowerFires()
    {
        var fire = new TowerFireRules(C.Towers, Chain(), EastRange(1.5f));

        var volleys = fire.Tick(1f, new[] { Threat(FordsTestLayout.EastOuter0, 1, Base * 1.6f) });

        Assert.AreEqual(0, volleys.Count);
    }

    [TestMethod]
    public void Evaluate_ALeadPastTheBaseRange_StartsASiegeOnlyAgainstALongRangeTower()
    {
        var layout = FordsTestLayout.Create();
        var plain = new LaneMarchRules(C.Lanes, C.Towers, layout, new TowerChain(layout, C.Towers));
        var longRange = new LaneMarchRules(C.Lanes, C.Towers, layout, Chain());
        var situation = Situation(LaneOrder.Push, Base * 1.25f, 30, 10, nearestEnemy: 5f);

        Assert.IsFalse(plain.Evaluate(situation).Siege);
        Assert.IsTrue(longRange.Evaluate(situation).Siege);
    }

    [TestMethod]
    public void Decide_HoldAgainstALongRangeTower_StandsBackByTheExtraReach()
    {
        var layout = FordsTestLayout.Create();
        // 160 m short of the tower at x 640 is arc 480, which rounds back to the vertex at x 400. Four times the range adds 135 m: arc 345, the vertex at x 300.
        var lanes = C.Lanes with { HoldDistanceBeforeTower = 160f };
        var plain = new LaneMarchRules(lanes, C.Towers, layout, new TowerChain(layout, C.Towers));
        var longRange = new LaneMarchRules(lanes, C.Towers, layout, Chain(4f));
        var situation = Situation(LaneOrder.Hold, 400f);

        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(400f, 0f, 0f)), plain.Decide(situation));
        Assert.AreEqual(LaneCommand.HoldAt(new Vec3f(300f, 0f, 0f)), longRange.Decide(situation));
    }

    private static FordsMatch MatchWith(FactionFeats east)
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.East] = factions[MatchSide.East] with { Feats = east };
        return new FordsMatch(Config(), Layout(), factions, Slots("slow"), new SeededFordsRandom(7));
    }

    [TestMethod]
    public void FireRange_OnTheMatch_ReadsTheOwnersFeat()
    {
        var match = MatchWith(new FactionFeats { TowerRange = 1.5f });

        Assert.AreEqual(Base * 1.5f, match.FireRange(FordsTestLayout.EastOuter0), 0.001f);
        Assert.AreEqual(Base, match.FireRange(FordsTestLayout.WestOuter0), 0.001f);
    }

    [TestMethod]
    public void Tick_LocalCounts_UseTheRangeOfTheTowersOwner()
    {
        var match = MatchWith(new FactionFeats { TowerRange = 1.5f });
        var position = new Vec3f(640f - Base * 1.25f, 0f, 0f);

        match.Tick(Snap(1f, new[] { new LaneSnapshot(MatchSide.West, 0, 1, position, float.MaxValue, new List<Vec3f> { position }) }));

        var decision = match.LastDecisions.Single(d => d.Side == MatchSide.West && d.Lane == 0);
        Assert.AreEqual(1, decision.NearAttackers);
    }

    [TestMethod]
    public void GetView_AssaultThreshold_IsTheSameShareOfTheFactionsLaneCap()
    {
        var c = Config();
        var lanes = Layout().Lanes.Count;
        var cap = c.Waves.MaxLivePerLanePerSide;

        Assert.AreEqual(c.Lanes.AssaultThreshold, MatchWith(new FactionFeats()).GetView(EastSlot).AssaultThreshold);
        Assert.AreEqual(c.Lanes.AssaultThreshold * (cap - 60 / lanes) / cap, MatchWith(new FactionFeats { UnitCapBonus = -60 }).GetView(EastSlot).AssaultThreshold);
        Assert.AreEqual(c.Lanes.AssaultThreshold * (cap + 60 / lanes) / cap, MatchWith(new FactionFeats { UnitCapBonus = 60 }).GetView(EastSlot).AssaultThreshold);
    }
}
