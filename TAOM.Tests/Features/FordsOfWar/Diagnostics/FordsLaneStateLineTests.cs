using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

[TestClass]
public class FordsLaneStateLineTests
{
    private static LaneDecision Decision(LaneBranch branch, LaneCommand command, string? towerId = "tower_east_0_0") =>
        new(MatchSide.West, 0, branch, LaneOrder.Push, 40, new Vec3f(300f, 10f, 0f), new Vec3f(310f, 12f, 0f), 310f, 35, float.MaxValue,
            towerId, 330f, 340.5f, false, branch == LaneBranch.March ? 4 : -1, command);

    [TestMethod]
    public void Format_AMoveTheDirectorSent_PrintsTheRulesDecisionThenCmdAndSent()
    {
        var decision = Decision(LaneBranch.March, LaneCommand.MoveTo(new Vec3f(400f, 0f, 0f)));
        var facts = new FordsLaneStateFacts(LaneCommand.MoveTo(new Vec3f(396f, 2f, 0f)), decision);

        Assert.AreEqual(decision.ToLogBody() + " cmd=MoveTo sent=(396.0,2.0)", FordsLaneStateLine.Format(facts));
    }

    [TestMethod]
    public void Format_AnAttackTowerThatFellBackToAMove_ShowsTheMoveItWasSentAs()
    {
        var decision = Decision(LaneBranch.AttackTower, LaneCommand.AttackTower("tower_east_0_0"));
        var facts = new FordsLaneStateFacts(LaneCommand.MoveTo(new Vec3f(500f, 5f, 0f)), decision);

        var text = FordsLaneStateLine.Format(facts);

        StringAssert.Contains(text, "branch=AttackTower");
        StringAssert.EndsWith(text, " cmd=MoveTo sent=(500.0,5.0)");
    }

    [TestMethod]
    public void Format_AnAttackTowerThatWasSent_NamesItsTowerInsteadOfACoordinate()
    {
        var decision = Decision(LaneBranch.AttackTower, LaneCommand.AttackTower("tower_west_1_0"), "tower_west_1_0");
        var facts = new FordsLaneStateFacts(LaneCommand.AttackTower("tower_west_1_0"), decision);

        StringAssert.EndsWith(FordsLaneStateLine.Format(facts), " cmd=AttackTower sent=tower_west_1_0");
    }

    [TestMethod]
    public void Format_NothingSentYet_PrintsDashesNotZeros()
    {
        var facts = new FordsLaneStateFacts(null, Decision(LaneBranch.None, LaneCommand.None, null));

        StringAssert.EndsWith(FordsLaneStateLine.Format(facts), " cmd=None sent=-");
    }

    [TestMethod]
    public void Format_ACommandWithoutATarget_Charge_PrintsNoTarget()
    {
        var facts = new FordsLaneStateFacts(LaneCommand.Charge, Decision(LaneBranch.Charge, LaneCommand.Charge));

        StringAssert.EndsWith(FordsLaneStateLine.Format(facts), " cmd=Charge sent=-");
    }

    [TestMethod]
    public void Format_UsesTheInvariantCulture()
    {
        var old = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var facts = new FordsLaneStateFacts(LaneCommand.MoveTo(new Vec3f(1.5f, 2.5f, 0f)), Decision(LaneBranch.March, LaneCommand.None));

            StringAssert.Contains(FordsLaneStateLine.Format(facts), "sent=(1.5,2.5)");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = old;
        }
    }

    [TestMethod]
    public void LayoutFacts_Of_ListsBuiltAndMissingTowers_WaypointsPerLane_AndTheKeepLocks()
    {
        var layout = FordsTestLayout.Create();
        var expected = layout.Towers.Select(t => t.Id).Concat(new[] { "tower_east_9_9" }).ToList();
        var keeps = layout.Towers.Where(t => t.Kind == TowerKind.Keep)
            .Select(t => new MatchView.TowerView(t.Id, t.Owner, t.Lane, t.Order, t.Kind, 10, 10, t.Owner == MatchSide.East, false)).ToList();
        var view = new MatchView(1, MatchSide.West, MatchPhase.Live, 0f, 0, 5, 3, new List<int>(), new List<MatchView.LaneView>(), keeps,
            new List<MatchView.HeroView>(), new List<MatchView.UnitOfferView>(), new List<MatchView.HeroOfferView>(),
            new List<MatchView.CapturePointView>(), null);

        var facts = FordsLayoutFacts.Of(layout, expected, view);

        Assert.AreEqual(layout.SceneName, facts.Scene);
        CollectionAssert.AreEqual(layout.Lanes.Select(l => l.Path.Count).ToList(), facts.LaneWaypoints.ToList());
        CollectionAssert.AreEqual(new[] { "tower_east_9_9" }, facts.MissingTowerIds.ToList());
        CollectionAssert.AreEqual(layout.Towers.Select(t => t.Id).ToList(), facts.BuiltTowerIds.ToList());
        Assert.IsTrue(facts.KeepOpenWest, "the west keep's view says Locked = false");
        Assert.IsFalse(facts.KeepOpenEast, "the east keep's view says Locked = true");
        Assert.AreEqual(layout.Lanes[0].Path[0], facts.WestBase);
        Assert.AreEqual(layout.Lanes[0].Path[layout.Lanes[0].Path.Count - 1], facts.EastBase);
    }

    [TestMethod]
    public void LayoutFacts_Describe_NamesSceneBasesWaypointsTowersAndOpenKeeps()
    {
        var facts = new FordsLayoutFacts("scene_a", new Vec3f(10.4f, 20f, 0f), new Vec3f(900f, 5f, 0f), new[] { 25, 24, 26 },
            new[] { "keep_west", "tower_east_0_0" }, new[] { "tower_east_1_0", "tower_east_1_1" }, true, false);

        Assert.AreEqual("scene=scene_a west=(10.4,20.0) east=(900.0,5.0) waypoints=0:25,1:24,2:26 built=keep_west,tower_east_0_0 "
            + "missing=tower_east_1_0,tower_east_1_1 keepOpenAtStart=West:true,East:false", facts.Describe());
    }

    [TestMethod]
    public void LayoutFacts_Describe_NothingMissing_PrintsADash()
    {
        var facts = new FordsLayoutFacts("s", null, null, new int[0], new[] { "keep_west" }, new string[0], false, false);

        StringAssert.Contains(facts.Describe(), "west=- east=- waypoints=- built=keep_west missing=- ");
    }
}
