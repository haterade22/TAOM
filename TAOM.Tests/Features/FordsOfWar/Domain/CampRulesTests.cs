using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>Camp alive counts, the cleared event and who gets the reward.</summary>
[TestClass]
public class CampRulesTests
{
    private static int Owner(MatchSide side, int lane) => (side == MatchSide.West ? 10 : 20) + lane;

    // FordsTestLayout lanes run along y = 0 (lane 0) and y = 50 (lane 1).
    private static MapLayout Layout() => FordsTestLayout.Create() with
    {
        Camps = new List<CampSlot>
        {
            new("camp_a", new Vec3f(500f, 10f, 0f), "wolf", 3, 120),
            new("camp_b", new Vec3f(400f, 45f, 0f), "bear", 2, 80),
            new("camp_empty", new Vec3f(300f, 25f, 0f), "", 0, 0),
        },
    };

    private static CampRules Rules() => new(Layout(), Owner);

    [TestMethod]
    public void New_AliveCountStartsAtTheCampCount()
    {
        var rules = Rules();

        Assert.AreEqual(3, rules.Alive("camp_a"));
        Assert.AreEqual(2, rules.Alive("camp_b"));
        Assert.IsFalse(rules.IsCleared("camp_a"));
    }

    [TestMethod]
    public void Alive_UnknownCamp_IsZero()
    {
        Assert.AreEqual(0, Rules().Alive("nowhere"));
        Assert.IsFalse(Rules().IsCleared("nowhere"));
    }

    [TestMethod]
    public void NearestLane_IsTheLaneClosestToTheCamp()
    {
        var rules = Rules();

        Assert.AreEqual(0, rules.NearestLane("camp_a"));
        Assert.AreEqual(1, rules.NearestLane("camp_b"));
        Assert.AreEqual(-1, rules.NearestLane("nowhere"));
    }

    [TestMethod]
    public void NearestLane_ATie_GoesToTheLowerLane()
    {
        var rules = Rules();

        Assert.AreEqual(0, rules.NearestLane("camp_empty"));
    }

    [TestMethod]
    public void OnUnitKilled_NotTheLastUnit_ReducesTheCountAndReturnsNull()
    {
        var rules = Rules();

        var cleared = rules.OnUnitKilled("camp_a", MatchSide.West);

        Assert.IsNull(cleared);
        Assert.AreEqual(2, rules.Alive("camp_a"));
    }

    [TestMethod]
    public void OnUnitKilled_TheLastUnit_ClearsTheCampAndPaysTheLaneOwnerOfTheKiller()
    {
        var rules = Rules();
        rules.OnUnitKilled("camp_a", MatchSide.East);
        rules.OnUnitKilled("camp_a", MatchSide.West);

        var cleared = rules.OnUnitKilled("camp_a", MatchSide.West);

        Assert.IsNotNull(cleared);
        Assert.AreEqual("camp_a", cleared!.CampId);
        Assert.AreEqual(MatchSide.West, cleared.Side);
        Assert.AreEqual(Owner(MatchSide.West, 0), cleared.Slot);
        Assert.AreEqual(120, cleared.Reward);
        Assert.IsTrue(rules.IsCleared("camp_a"));
        Assert.AreEqual(0, rules.Alive("camp_a"));
    }

    [TestMethod]
    public void OnUnitKilled_ClearedByEast_PaysTheEastSlotOfTheNearestLane()
    {
        var rules = Rules();
        rules.OnUnitKilled("camp_b", MatchSide.West);

        var cleared = rules.OnUnitKilled("camp_b", MatchSide.East);

        Assert.AreEqual(MatchSide.East, cleared!.Side);
        Assert.AreEqual(Owner(MatchSide.East, 1), cleared.Slot);
        Assert.AreEqual(80, cleared.Reward);
    }

    [TestMethod]
    public void OnUnitKilled_LastKillerUnknown_ClearsWithoutAReward()
    {
        var rules = Rules();
        rules.OnUnitKilled("camp_b", MatchSide.West);

        var cleared = rules.OnUnitKilled("camp_b", null);

        Assert.IsNotNull(cleared);
        Assert.IsNull(cleared!.Side);
        Assert.AreEqual(-1, cleared.Slot);
        Assert.AreEqual(0, cleared.Reward);
        Assert.IsTrue(rules.IsCleared("camp_b"));
    }

    [TestMethod]
    public void OnUnitKilled_AfterTheCampIsCleared_ReturnsNullAndNeverGoesNegative()
    {
        var rules = Rules();
        rules.OnUnitKilled("camp_b", MatchSide.West);
        rules.OnUnitKilled("camp_b", MatchSide.West);

        var again = rules.OnUnitKilled("camp_b", MatchSide.East);

        Assert.IsNull(again);
        Assert.AreEqual(0, rules.Alive("camp_b"));
    }

    [TestMethod]
    public void OnUnitKilled_UnknownCamp_IsIgnored()
    {
        Assert.IsNull(Rules().OnUnitKilled("nowhere", MatchSide.West));
    }

    [TestMethod]
    public void OnUnitKilled_CampWithNoUnits_NeverClears()
    {
        var rules = Rules();

        Assert.IsNull(rules.OnUnitKilled("camp_empty", MatchSide.West));
        Assert.IsFalse(rules.IsCleared("camp_empty"));
    }

    [TestMethod]
    public void OnUnitKilled_CampsAreIndependent()
    {
        var rules = Rules();
        rules.OnUnitKilled("camp_a", MatchSide.West);

        Assert.AreEqual(2, rules.Alive("camp_b"));
    }

    [TestMethod]
    public void New_LayoutWithoutLanes_StillTracksCountsAndPaysNobody()
    {
        var rules = new CampRules(Layout() with { Lanes = new List<LaneLayout>() }, Owner);

        rules.OnUnitKilled("camp_b", MatchSide.West);
        var cleared = rules.OnUnitKilled("camp_b", MatchSide.West);

        Assert.AreEqual(-1, rules.NearestLane("camp_b"));
        Assert.AreEqual(-1, cleared!.Slot);
        Assert.AreEqual(0, cleared.Reward);
    }
}
