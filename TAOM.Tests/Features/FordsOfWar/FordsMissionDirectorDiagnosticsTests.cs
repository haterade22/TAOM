using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>What the director adds for the next unattended match: the layout line, off-navmesh refusals, lane state lines and the gunner lookup.</summary>
[TestClass]
public class FordsMissionDirectorDiagnosticsTests
{
    private const string EastOuterLane0 = "tower_east_0_0";

    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void ClearThreadGuard() => MissionThreadGuard.ResetForTests();

    private static void Second(FordsDirectorFixture f) => f.Report.MissionTick(1f, 1.0);

    private static void DropFromTheFit(FordsDirectorFixture f, params string[] ids) =>
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call =>
        {
            var layout = call.Arg<MapLayout>();
            var kept = layout.Towers.Where(t => !ids.Contains(t.Id)).ToList();
            return new FordsLayoutFit(layout with { Towers = kept }, 0, ids.Length, 0);
        });

    // --- the layout line -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Start_ALayoutThatLosesNothing_ReportsOneLayoutLine_WithBothKeepsLocked()
    {
        var f = FordsDirectorFixture.Create();

        Assert.IsTrue(f.Director.Start());

        var layout = f.Report.Run!.Start.Layout;
        Assert.IsTrue(layout.Reported);
        Assert.AreEqual("test_scene", layout.Scene);
        Assert.AreEqual(0, layout.MissingTowerIds.Count);
        Assert.AreEqual(14, layout.BuiltTowerIds.Count);
        Assert.AreEqual(3, layout.LaneWaypoints.Count);
        Assert.IsTrue(layout.LaneWaypoints.All(n => n > 2));
        Assert.IsFalse(layout.KeepOpenWest);
        Assert.IsFalse(layout.KeepOpenEast);
        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[FordsOfWar][Layout]") && m.Contains("scene=test_scene") && m.Contains("missing=-")));
    }

    [TestMethod]
    public void Start_ATowerTheFitterDropped_IsListedMissing_AndCheck4LayoutFailsNamingIt()
    {
        var f = FordsDirectorFixture.Create();
        DropFromTheFit(f, EastOuterLane0);

        Assert.IsTrue(f.Director.Start());

        CollectionAssert.AreEqual(new[] { EastOuterLane0 }, f.Report.Run!.Start.Layout.MissingTowerIds);
        var part = FordsVerdicts.Judge(new FordsSessionEvidence(), f.Report.Run)[3].Parts.Single(p => p.Name == "layout");
        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, EastOuterLane0);
    }

    [TestMethod]
    public void Start_ATowerTheEngineCouldNotBuild_IsListedMissing()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == "tower_west_2_1"), Arg.Any<TowerPrefab>(), Arg.Any<int>()).Returns(false);

        Assert.IsTrue(f.Director.Start());

        CollectionAssert.AreEqual(new[] { "tower_west_2_1" }, f.Report.Run!.Start.Layout.MissingTowerIds);
        Assert.IsFalse(f.Report.Run.Start.Layout.BuiltTowerIds.Contains("tower_west_2_1"));
    }

    [TestMethod]
    public void Start_ALaneWithNoTowersLeft_OpensItsKeepAtStart_AndCheck4LayoutNamesTheSide()
    {
        var f = FordsDirectorFixture.Create();
        DropFromTheFit(f, "tower_west_1_0", "tower_west_1_1");

        Assert.IsTrue(f.Director.Start());

        var layout = f.Report.Run!.Start.Layout;
        Assert.IsTrue(layout.KeepOpenWest, "a lane with no towers counts as lost, which unlocks the keep");
        Assert.IsFalse(layout.KeepOpenEast);
        var part = FordsVerdicts.Judge(new FordsSessionEvidence(), f.Report.Run)[3].Parts.Single(p => p.Name == "layout");
        Assert.AreEqual(FordsResult.Fail, part.Result);
        StringAssert.Contains(part.Numbers, "keepOpen=West");
        StringAssert.Contains(part.Numbers, "tower_west_1_0");
    }

    // --- off-navmesh refusals ------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_AMoveTheAdapterRefuses_IsCountedOffNavmeshOnce_WithTheFirstTargetAndTheTimeOfTheRefusal()
    {
        var f = FordsDirectorFixture.Create();
        Vec3f? first = null;
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(call =>
        {
            first ??= call.Arg<Vec3f>();
            return false;
        });
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);

        var stream = f.Report.Run!.Lanes[0];
        Assert.AreEqual(1, stream.OffNavmesh, "the target and the waypoint tried in its place are one refused command");
        Assert.IsNotNull(first);
        Assert.AreEqual(first!.Value.X, stream.FirstOffNavmeshX!.Value, 0.01);
        Assert.AreEqual(first.Value.Y, stream.FirstOffNavmeshY!.Value, 0.01);
        Assert.AreEqual(0, f.Report.Run.Lanes[1].OffNavmesh, "other streams were not refused");
    }

    [TestMethod]
    public void Tick_TargetRefusedButWaypointAccepted_CountsOffNavmeshOnceWithTheTarget()
    {
        var f = FordsDirectorFixture.Create();
        var moves = new System.Collections.Generic.List<Vec3f>();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(call =>
        {
            moves.Add(call.Arg<Vec3f>());
            return moves.Count > 1;
        });
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);
        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);

        var stream = f.Report.Run!.Lanes[0];
        Assert.AreEqual(1, stream.OffNavmesh, "the target was refused once; the waypoint that took its place does not hide that");
        Assert.AreEqual(moves[0].X, stream.FirstOffNavmeshX!.Value, 0.01);
        Assert.AreEqual(moves[0].Y, stream.FirstOffNavmeshY!.Value, 0.01);
        Assert.AreEqual(0, stream.CommandsRefused.Values.Sum(), "the command itself was given, through the fallback");
    }

    [TestMethod]
    public void Tick_ARefusedMoveRetriedOnLaterTicks_IsNotCountedAgain()
    {
        var f = FordsDirectorFixture.Create();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false);
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);
        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);
        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);

        var stream = f.Report.Run!.Lanes[0];
        Assert.AreEqual(1, stream.OffNavmesh);
        Assert.AreEqual(1, stream.CommandsRefused.Values.Sum(), "the refusal itself was already counted once per command");
        f.Battlefield.Received(6).Move(MatchSide.West, 0, Arg.Any<Vec3f>());
    }

    [TestMethod]
    public void Tick_ANewlyRefusedCommand_RecordsTheMissionTimeOfTheLastRefusal()
    {
        var f = FordsDirectorFixture.Create();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false);
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));
        f.Report.MissionTick(7f, 1.0);

        f.Director.Tick(1f);

        Assert.AreEqual(7.0, f.Report.Run!.Lanes[0].LastOffNavmeshMt!.Value, 1e-6);
    }

    [TestMethod]
    public void Tick_MovesTheAdapterAccepts_CountNoRefusal()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);

        Assert.IsTrue(f.Report.Run!.Lanes.All(l => l.OffNavmesh == 0));
        Assert.IsTrue(f.Report.Run.Lanes[0].CommandsGiven.Count > 0, "and the command that was given is counted");
    }

    // --- the lane state line -------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_WritesALaneStateLinePerStream_AtMostEveryFiveMissionSeconds()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));
        f.Sample(2, MatchSide.West, 0, new Vec3f(120f, 20f, 0f));

        Second(f);
        f.Director.Tick(1f);
        var logged = f.Logger.ReceivedCalls().Count(c => c.GetArguments().FirstOrDefault() is string m && m.Contains("[FordsOfWar][LaneState]"));
        Assert.AreEqual(6, logged, "3 lanes on both sides");

        for (var i = 0; i < 4; i++)
        {
            Second(f);
            f.Director.Tick(1f);
        }

        Assert.AreEqual(6, f.Logger.ReceivedCalls().Count(c => c.GetArguments().FirstOrDefault() is string m && m.Contains("[FordsOfWar][LaneState]")),
            "four more seconds add none");

        Second(f);
        f.Director.Tick(1f);

        Assert.AreEqual(12, f.Logger.ReceivedCalls().Count(c => c.GetArguments().FirstOrDefault() is string m && m.Contains("[FordsOfWar][LaneState]")),
            "the sixth mission second is five seconds after the first");
    }

    [TestMethod]
    public void Tick_TheLaneStateLine_PrintsTheRulesDecisionWithItsLeadGroupAndBranch()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));
        f.Sample(2, MatchSide.West, 0, new Vec3f(120f, 20f, 0f));
        var lines = new System.Collections.Generic.List<string>();
        f.Logger.When(l => l.LogInfo(Arg.Is<string>(m => m.Contains("[LaneState]")))).Do(c => lines.Add(c.Arg<string>()));

        Second(f);
        f.Director.Tick(1f);

        var west0 = lines.Single(l => l.Contains("side=West lane=0 "));
        StringAssert.Contains(west0, "order=Push live=2 branch=");
        StringAssert.Contains(west0, "center=(110.0,10.0) lead=");
        StringAssert.Contains(west0, "tower=tower_east_0_0 towerDist=");
        StringAssert.Contains(west0, " cmd=");
        var east1 = lines.Single(l => l.Contains("side=East lane=1 "));
        StringAssert.Contains(east1, "live=0 branch=None center=-");
    }

    [TestMethod]
    public void Tick_ARefusedMoveThatFellBackToAWaypoint_TheLaneStateLineShowsThePointThatWasSent()
    {
        var f = FordsDirectorFixture.Create();
        var moves = new System.Collections.Generic.List<Vec3f>();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(call =>
        {
            moves.Add(call.Arg<Vec3f>());
            return moves.Count > 1;
        });
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));
        var lines = new System.Collections.Generic.List<string>();
        f.Logger.When(l => l.LogInfo(Arg.Is<string>(m => m.Contains("[LaneState]")))).Do(c => lines.Add(c.Arg<string>()));

        Second(f);
        f.Director.Tick(1f);

        Assert.AreEqual(2, moves.Count, "the rules' target was refused, then the nearest waypoint was sent");
        var west0 = lines.Single(l => l.Contains("side=West lane=0 "));
        StringAssert.Contains(west0, " sent=" + FordsLogLine.Point(moves[1]));
    }

    [TestMethod]
    public void Tick_AnAttackTowerTheEntityCannotTake_IsLoggedAsTheMoveItFellBackTo()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        var tower = f.TowerSlot(EastOuterLane0);
        f.Sample(1, MatchSide.West, 0, new Vec3f(tower.Position.X - 8f, tower.Position.Y, 0f));
        var lines = new System.Collections.Generic.List<string>();
        f.Logger.When(l => l.LogInfo(Arg.Is<string>(m => m.Contains("[LaneState]")))).Do(c => lines.Add(c.Arg<string>()));

        Second(f);
        f.Director.Tick(1f);

        var west0 = lines.Single(l => l.Contains("side=West lane=0 "));
        StringAssert.Contains(west0, "branch=AttackTower");
        StringAssert.EndsWith(west0, " cmd=MoveTo sent=" + FordsLogLine.Point(tower.Position));
    }

    [TestMethod]
    public void Tick_TheLaneStateLine_HasOneFormatForEveryStream_TheDecisionThenCmdAndSent()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));
        var lines = new System.Collections.Generic.List<string>();
        f.Logger.When(l => l.LogInfo(Arg.Is<string>(m => m.Contains("[LaneState]")))).Do(c => lines.Add(c.Arg<string>()));

        Second(f);
        f.Director.Tick(1f);

        Assert.AreEqual(6, lines.Count);
        foreach (var line in lines)
        {
            StringAssert.Contains(line, " branch=");
            StringAssert.Matches(line, new System.Text.RegularExpressions.Regex(@" cmd=\w+ sent=\S+$"));
            Assert.IsFalse(line.Contains("nextTower=") || line.Contains(" applied="), "the old fallback shape and token are gone: " + line);
        }
    }

    // --- the layout planner --------------------------------------------------------------------------------------------

    [TestMethod]
    public void Start_AFitTheFitterRejects_RefusesWithOneError_AndPreparesNothing()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call =>
            new FordsLayoutFit(call.Arg<MapLayout>(), 0, 0, 0) { Rejected = true, Reason = "lane 1 has no tower left for West" });

        Assert.IsFalse(f.Director.Start());

        f.Logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("refused") && m.Contains("lane 1 has no tower left for West")));
        f.Battlefield.DidNotReceive().Prepare(Arg.Any<int>());
    }

    [TestMethod]
    public void Start_LogsThePlannerAttempt_OneLinePerLane_AndEverySlidTower()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>(), 1, 0, 0)
        {
            Lanes = new[]
            {
                new FordsLaneFitStats(0, 40, 40, 1, 0, 14.5f, 0), new FordsLaneFitStats(1, 40, 39, 0, 1, 26f, 1),
                new FordsLaneFitStats(2, 40, 40, 0, 0, 12f, 0),
            },
            MovedTowers = new[] { new FordsTowerMove("tower_west_1_0", 12.4f) },
        });

        Assert.IsTrue(f.Director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[FordsOfWar][Layout] attempt spread x1.00") && m.Contains("=> pass")));
        f.Logger.Received(1).LogInfo(Arg.Is<string>(m =>
            m.Contains("[FordsOfWar][Layout] lane=1 waypoints=40 kept=39 moved=0 dropped=1 maxGap=26.0 badSegments=1")));
        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[FordsOfWar][Layout] tower tower_west_1_0 slid 12.4 m")));
    }

    [TestMethod]
    public void Start_NoCandidatePassesTheLimits_PlaysTheBestAttemptWithOneWarning()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call => new FordsLayoutFit(call.Arg<MapLayout>(), 0, 0, 0)
        {
            Lanes = new[] { new FordsLaneFitStats(0, 40, 40, 0, 0, 12f, 20) },
        });

        Assert.IsTrue(f.Director.Start());

        f.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("no layout passed") && m.Contains("playing the best attempt")));
        f.Battlefield.Received(1).Prepare(3);
    }

    // --- an AI on both sides (autorun only) ----------------------------------------------------------------------------

    [TestMethod]
    public void Start_APlanWithWestAi_GivesTheWestSlotAnAiCommander_AndSaysSo()
    {
        var f = FordsDirectorFixture.Create();
        var director = new FordsMissionDirector(f.Plan with { WestAi = true }, f.Battlefield, f.Towers, f.Camps, f.Scene, f.Session, f.Logger,
            new SeededFordsRandom(1), f.Report);

        Assert.IsTrue(director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("match started") && m.Contains("an AI commands both sides")));
    }

    [TestMethod]
    public void Start_APlanWithoutWestAi_KeepsTheWestSlotHuman()
    {
        var f = FordsDirectorFixture.Create();

        Assert.IsTrue(f.Director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("match started")));
        f.Logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("an AI commands both sides")));
    }

    // --- tower gunners -------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Start_BindsTheGunnerAndAttackerLookupsToTheReport_AndShutdownUnbindsThem()
    {
        var f = FordsDirectorFixture.Create();
        var gunner = new object();
        var enemy = new object();
        f.Towers.TowerIdOfGunner(gunner).Returns(EastOuterLane0);
        f.Battlefield.SideOf(enemy).Returns(MatchSide.West);
        Assert.IsTrue(f.Director.Start());

        f.Report.GunnerHit(gunner, enemy, false);
        f.Report.GunnerHit(new object(), enemy, false);

        Assert.AreEqual(1, f.Report.Run!.Towers[EastOuterLane0].GunnerBlowsMelee);

        f.Director.Shutdown();
        f.Report.GunnerHit(gunner, enemy, false);

        Assert.AreEqual(1, f.Report.Run.Towers[EastOuterLane0].GunnerBlowsMelee);
    }

    [TestMethod]
    public void AdjustTowerDamage_ReportsTheAttackersSideAndLane_AndTheDamageThatWasApplied()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Samples.Clear();
        var attacker = new object();
        f.Towers.TowerIdOf(Arg.Any<object?>()).Returns(EastOuterLane0);
        f.Battlefield.TryGetKey(attacker, out Arg.Any<AgentKey>()).Returns(call => { call[1] = new AgentKey(1); return true; });
        f.Battlefield.SideOf(attacker).Returns(MatchSide.West);
        f.Director.Tick(1f);
        f.Report.MissionTick(1f, 1.0);

        var applied = f.Director.AdjustTowerDamage(new object(), attacker, false, 40, 0);

        var tower = f.Report.Run!.Towers[EastOuterLane0];
        Assert.AreEqual(1, tower.BlowsBySide[(int)MatchSide.West]);
        Assert.IsTrue(applied > 0);
        Assert.AreEqual(applied, (int)tower.AppliedDamage[(int)TowerBlowClass.Other], "an attacker with no unit record is class Other, and its applied damage is recorded");
    }

    [TestMethod]
    public void Tick_AHealFromTheRules_IsRecordedAsHealedHitPoints()
    {
        var f = FordsDirectorFixture.Create(c => c with { Towers = c.Towers with { RegenPerSecond = 50f, RegenDelaySeconds = 0f } });
        f.Towers.TowerIdOf(Arg.Any<object?>()).Returns(EastOuterLane0);
        Assert.IsTrue(f.Director.Start());
        f.Director.AdjustTowerDamage(new object(), null, true, 100, 0);

        for (var i = 0; i < 5; i++)
        {
            f.Report.MissionTick(1f, 1.0);
            f.Director.Tick(1f);
        }

        f.Towers.Received().Heal(EastOuterLane0, Arg.Any<int>());
        Assert.IsTrue(f.Report.Run!.Towers[EastOuterLane0].HealedTotal > 0);
    }

    [TestMethod]
    public void Tick_EveryProbe_RecordsTheLowestRulesHitPointsOfEachStandingTower()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.TowerIdOf(Arg.Any<object?>()).Returns(EastOuterLane0);
        Assert.IsTrue(f.Director.Start());
        f.Director.AdjustTowerDamage(new object(), null, true, 100, 0);

        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);

        var min = f.Report.Run!.Towers[EastOuterLane0].MinRulesHp;
        Assert.IsNotNull(min);
        Assert.IsTrue(min < f.TowersSpawned.Single(t => t.Slot.Id == EastOuterLane0).HitPoints, "lower than full after a blow");
    }

    [TestMethod]
    public void Start_AMatchPlan_LogsTheEffectiveTowerConfigOnce()
    {
        var f = FordsDirectorFixture.Create();

        Assert.IsTrue(f.Director.Start());

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[FordsOfWar][Tower] ") && m.Contains(" config towerHp=")));
    }

    [TestMethod]
    public void Tick_AStreamMarchingWithItsUnitsShortOfTheTower_IsJudgedAndNotPast()
    {
        var f = FordsDirectorFixture.Create();
        Assert.IsTrue(f.Director.Start());
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));

        f.Report.MissionTick(1f, 1.0);
        f.Director.Tick(1f);

        var stream = f.Report.Run!.Lanes[0];
        Assert.IsTrue(stream.MarchSamples >= 1, "West lane 0 has a unit far from any tower, so it marches and the probe judges it");
        Assert.AreEqual(0, stream.PastSamples);
    }
}
