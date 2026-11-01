using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Domain;

/// <summary>The one-line text of a lane decision, which the run log prints for every lane every few seconds.</summary>
[TestClass]
public class LaneDecisionTests
{
    private static LaneDecision March() => new(MatchSide.West, 0, LaneBranch.March, LaneOrder.Push, 40,
        new Vec3f(300.04f, 10f, 0f), new Vec3f(310f, 12f, 0f), 310f, 35, float.MaxValue, "tower_east_0_0", 330f, 340.5f, false, 4,
        LaneCommand.MoveTo(new Vec3f(400f, 0f, 0f)));

    [TestMethod]
    public void ToLogBody_KnownLocalCounts_AppendNearAndSiege()
    {
        var decision = March() with { NearAttackers = 30, NearDefenders = 20, Siege = true };

        StringAssert.EndsWith(decision.ToLogBody(), " near=30/20 siege=true");
        StringAssert.EndsWith((decision with { Siege = false }).ToLogBody(), " near=30/20 siege=false");
    }

    [TestMethod]
    public void ToLogBody_MarchingLane_NamesEveryField()
    {
        Assert.AreEqual(
            "order=Push live=40 branch=March center=(300.0,10.0) lead=(310.0,12.0) leadArc=310.0 leadCount=35 enemy=- "
            + "tower=tower_east_0_0 towerDist=330.0 centerTowerDist=340.5 locked=false wp=4 target=(400.0,0.0)",
            March().ToLogBody());
    }

    [TestMethod]
    public void ToLogBody_AttackingLane_ShowsTheEnemyDistanceAndTheTowerIdAsTheTarget()
    {
        var decision = new LaneDecision(MatchSide.East, 2, LaneBranch.AttackTower, LaneOrder.Push, 5, new Vec3f(1f, 2f, 3f),
            new Vec3f(1f, 2f, 3f), 1f, 5, 12.5f, "keep_east", 10f, 20f, true, -1, LaneCommand.AttackTower("keep_east"));

        Assert.AreEqual(
            "order=Push live=5 branch=AttackTower center=(1.0,2.0) lead=(1.0,2.0) leadArc=1.0 leadCount=5 enemy=12.5 "
            + "tower=keep_east towerDist=10.0 centerTowerDist=20.0 locked=true wp=- target=keep_east",
            decision.ToLogBody());
    }

    [TestMethod]
    public void ToLogBody_HoldingLane_ShowsTheHoldPointAsTheTarget()
    {
        var decision = March() with { Branch = LaneBranch.Hold, Order = LaneOrder.Hold, WaypointIndex = -1,
            Command = LaneCommand.HoldAt(new Vec3f(500f, 0f, 0f)) };

        StringAssert.Contains(decision.ToLogBody(), "branch=Hold");
        StringAssert.Contains(decision.ToLogBody(), "wp=- target=(500.0,0.0)");
    }

    [TestMethod]
    public void ToLogBody_NaNOrInfiniteNumbers_PrintDashesLikeTheReportHelpers()
    {
        var decision = March() with { Center = new Vec3f(float.NaN, 1f, 0f), NearestEnemyDistance = float.NaN,
            NextEnemyTowerDistance = float.PositiveInfinity };

        var text = decision.ToLogBody();

        StringAssert.Contains(text, "center=- ");
        StringAssert.Contains(text, "enemy=- ");
        StringAssert.Contains(text, "towerDist=- ");
        Assert.IsFalse(text.Contains("NaN") || text.Contains("Infinity"), text);
    }

    [TestMethod]
    public void ToLogBody_EmptyLane_PrintsDashesForWhatIsNotThere()
    {
        var decision = new LaneDecision(MatchSide.West, 1, LaneBranch.None, LaneOrder.Hold, 0, null, null, 0f, 0, float.MaxValue,
            null, float.MaxValue, float.MaxValue, false, -1, LaneCommand.None);

        Assert.AreEqual(
            "order=Hold live=0 branch=None center=- lead=- leadArc=0.0 leadCount=0 enemy=- tower=- towerDist=- "
            + "centerTowerDist=- locked=false wp=- target=-",
            decision.ToLogBody());
    }

    [TestMethod]
    public void ToLogBody_UnderACommaDecimalCulture_StillUsesDots()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            StringAssert.Contains(March().ToLogBody(), "center=(300.0,10.0)");
            StringAssert.Contains(March().ToLogBody(), "towerDist=330.0");
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }
}
