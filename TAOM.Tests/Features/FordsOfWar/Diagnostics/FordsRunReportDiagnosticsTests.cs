using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Scenarios;

namespace TAOM.Tests.Features.FordsOfWar.Diagnostics;

/// <summary>
/// The evidence that lets one unattended match prove or refute each check 9 cause from the log and the JSON alone: every lane
/// command counted past the 30 line log cap, off-navmesh refusals, the throttled lane state line, alarm and contact seconds, blows
/// on tower gunners and the layout line.
/// </summary>
[TestClass]
public class FordsRunReportDiagnosticsTests
{
    private static readonly FordsMatchSetup Setup = new("scene_a", "gondor", "mordor", "hard", 2);
    private static readonly FordsMatchFacts Facts = new(60f, 8f, 12f, 2, 150, 200);

    private ScenarioLogger _log = null!;
    private FordsRunReport _report = null!;

    [TestInitialize]
    public void Init()
    {
        _log = new ScenarioLogger();
        _report = new FordsRunReport(_log);
        StartedRun();
    }

    private static List<TowerSlot> Towers() => new()
    {
        new TowerSlot("keep_west", MatchSide.West, -1, 99, TowerKind.Keep, new Vec3f(0, 0, 0), 0f),
        new TowerSlot("keep_east", MatchSide.East, -1, 99, TowerKind.Keep, new Vec3f(900, 0, 0), 0f),
        new TowerSlot("east_outer_0", MatchSide.East, 0, 0, TowerKind.Tower, new Vec3f(600, 0, 0), 0f),
    };

    private void StartedRun()
    {
        _report.BeginRun("menu", Setup);
        _report.MatchStarted(Facts, Towers(), 3);
    }

    private List<string> Lines(string topic) => _log.Lines.Where(l => l.Contains("[FordsOfWar][" + topic + "]")).ToList();

    // Streams are stored West first: West 0, West 1, East 0, East 1.
    private FordsLaneEvidence Stream(MatchSide side, int lane) => _report.Run!.Lanes[(int)side * Facts.Lanes + lane];

    /// <summary>A 1 Hz look at which the stream's applied command is an AttackTower order at <paramref name="target"/> (null: it has none).</summary>
    private void Order(MatchSide side, int lane, string? target) =>
        _report.Field(new FordsFieldSample(new[] { new FordsLaneSample(side, lane, 10, 100f, false, 0, -1f, null, 0, target, 0, 0) },
            new FordsTowerSample[0], new FordsPointSample[0], 0, 10, 0));

    [TestMethod]
    public void LaneCommand_PastTheLogCap_CountsEveryKindPerStream()
    {
        var kinds = new[] { LaneCommandKind.MoveTo, LaneCommandKind.HoldAt, LaneCommandKind.Charge, LaneCommandKind.AttackTower };

        for (var i = 0; i < 200; i++)
            _report.LaneCommand(MatchSide.East, 1, kinds[i % 4], i % 10 != 0);

        var lane = Stream(MatchSide.East, 1);
        Assert.AreEqual(30, Lines("Lane").Count, "the log cap is unchanged");
        Assert.AreEqual(40, lane.CommandsGiven["MoveTo"], "50 MoveTo calls; i = 0, 20, 40 ... 180 are refused");
        Assert.AreEqual(10, lane.CommandsRefused["MoveTo"]);
        Assert.AreEqual(50, lane.CommandsGiven["HoldAt"]);
        Assert.AreEqual(40, lane.CommandsGiven["Charge"], "i = 10, 30 ... 190 are refused");
        Assert.AreEqual(10, lane.CommandsRefused["Charge"]);
        Assert.AreEqual(50, lane.CommandsGiven["AttackTower"]);
        Assert.IsFalse(lane.CommandsRefused.ContainsKey("AttackTower"));
        Assert.AreEqual("AttackTower", lane.LastCommand, "the last command given, as before");
    }

    [TestMethod]
    public void LaneCommand_TheFirstGivenMissionTime_IsKeptPerKind_AndARefusalNeverSetsIt()
    {
        _report.MissionTick(3f, 5.0);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.MoveTo, true);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.AttackTower, false);
        _report.MissionTick(4f, 5.0);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.MoveTo, true);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.AttackTower, true);

        var lane = Stream(MatchSide.West, 0);
        Assert.AreEqual(3.0, lane.FirstGivenMt["MoveTo"], 1e-6);
        Assert.AreEqual(7.0, lane.FirstGivenMt["AttackTower"], 1e-6, "the refusal at mt 3 does not count as given");
        Assert.IsFalse(lane.FirstGivenMt.ContainsKey("Charge"));
        Assert.AreEqual(1, lane.CommandsRefused["AttackTower"]);
    }

    [TestMethod]
    public void LaneCommand_AStreamOutsideTheMatchLanes_IsIgnored()
    {
        _report.LaneCommand(MatchSide.West, 7, LaneCommandKind.Charge, true);

        Assert.IsTrue(_report.Run!.Lanes.All(l => l.CommandsGiven.Count == 0));
    }

    [TestMethod]
    public void OffNavmesh_CountsPerStream_AndKeepsTheFirstRefusedCoordinates()
    {
        _report.OffNavmesh(MatchSide.East, 1, 744.4f, 356.2f);
        _report.OffNavmesh(MatchSide.East, 1, 10f, 20f);
        _report.OffNavmesh(MatchSide.West, 0, 1f, 2f);

        var east = Stream(MatchSide.East, 1);
        Assert.AreEqual(2, east.OffNavmesh);
        Assert.AreEqual(744.4, east.FirstOffNavmeshX!.Value, 0.01);
        Assert.AreEqual(356.2, east.FirstOffNavmeshY!.Value, 0.01);
        Assert.AreEqual(1, Stream(MatchSide.West, 0).OffNavmesh);
        Assert.AreEqual(0, Stream(MatchSide.West, 1).OffNavmesh);
        var lines = Lines("Lane").Where(l => l.Contains("offNavmesh")).ToList();
        Assert.AreEqual(2, lines.Count, "one line per stream, at its first refusal");
        StringAssert.Contains(lines[0], "side=East lane=1 x=744.4 y=356.2");
    }

    [TestMethod]
    public void LaneState_LogsAtMostOncePerFiveMissionSeconds_PerStream()
    {
        var due = 0;
        for (var second = 0; second < 20; second++)
        {
            _report.MissionTick(1f, 5.0);
            var mt = _report.MissionSeconds;
            if (_report.LaneStateDue(MatchSide.West, 0, mt))
                due++;
            _report.LaneState(MatchSide.West, 0, mt, "order=Push live=5");
        }

        var lines = Lines("LaneState");
        Assert.AreEqual(4, lines.Count, "mt 1, 6, 11 and 16");
        Assert.AreEqual(4, due, "LaneStateDue agrees with what is written");
        StringAssert.Contains(lines[0], "side=West lane=0 order=Push live=5");
        StringAssert.Contains(lines[1], "mt=6.0");
    }

    [TestMethod]
    public void LaneState_EachStreamHasItsOwnClock_AndANewRunStartsFresh()
    {
        _report.MissionTick(1f, 5.0);

        _report.LaneState(MatchSide.West, 0, 1.0, "a");
        _report.LaneState(MatchSide.West, 1, 1.0, "b");
        _report.LaneState(MatchSide.East, 0, 1.0, "c");
        _report.LaneState(MatchSide.West, 0, 3.0, "again too soon");

        Assert.AreEqual(3, Lines("LaneState").Count);

        StartedRun();
        _report.LaneState(MatchSide.West, 0, 1.0, "fresh");

        Assert.AreEqual(4, Lines("LaneState").Count);
    }

    [TestMethod]
    public void Window_SummaryCarriesCompactLaneCommandCountsAndTheAlarmShare()
    {
        _report.Stage("live");
        for (var i = 0; i < 40; i++)
            _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.MoveTo, true);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.HoldAt, true);
        _report.LaneCommand(MatchSide.West, 0, LaneCommandKind.Charge, true);
        for (var i = 0; i < 3; i++)
            _report.LaneCommand(MatchSide.East, 1, LaneCommandKind.AttackTower, true);
        _report.LaneCommand(MatchSide.East, 1, LaneCommandKind.MoveTo, false);

        for (var i = 0; i < 30; i++)
        {
            if (_report.MissionTick(1f, 8.0))
                _report.Window(120, 90);
        }

        var summary = Lines("Summary").Single();
        StringAssert.Contains(summary, "cmds=W0:40/1/1/0!0 E1:0/0/0/3!1");
        StringAssert.Matches(summary, new System.Text.RegularExpressions.Regex(@"checks=[PFRN]{16}$"));
    }

    [TestMethod]
    public void Window_SummaryAlarmShare_IsADashBeforeAnyUnitIsSeen_AndPerStreamAfter()
    {
        for (var i = 0; i < 30; i++)
        {
            if (_report.MissionTick(1f, 8.0))
                _report.Window(0, 0);
        }

        StringAssert.Contains(Lines("Summary").Single(), " alarmed=- ");

        _report.Field(new FordsFieldSample(new[] { new FordsLaneSample(MatchSide.West, 1, 8, 10f, false, 0, -1f, null, 6) },
            new FordsTowerSample[0], new FordsPointSample[0], 8, 0, 0));
        _report.MissionTick(2f, 5.0);
        _report.Field(new FordsFieldSample(new[] { new FordsLaneSample(MatchSide.West, 1, 8, 10f, false, 0, -1f, null, 6) },
            new FordsTowerSample[0], new FordsPointSample[0], 8, 0, 0));
        for (var i = 0; i < 28; i++)
        {
            if (_report.MissionTick(1f, 8.0))
                _report.Window(0, 0);
        }

        StringAssert.Contains(Lines("Summary").Last(), " alarmed=W1:0.75 ");
    }

    [TestMethod]
    public void Field_MergesAlarmedUnitSeconds_AndTheLadderOfAStreamOrderedAtATower()
    {
        FordsFieldSample Sample(int live, int alarmed, string? target, int flagged, int contact) => new(
            new[] { new FordsLaneSample(MatchSide.East, 1, live, 100f, false, 0, -1f, null, alarmed, target, flagged, contact) },
            new[] { new FordsTowerSample("east_outer_0", 5) },
            new FordsPointSample[0], 0, live, 0);

        _report.MissionTick(1f, 5.0);
        _report.Field(Sample(10, 10, "west_outer_0", 1, 3));
        _report.MissionTick(2f, 5.0);
        _report.Field(Sample(10, 5, "west_outer_0", 2, 4));

        var lane = Stream(MatchSide.East, 1);
        Assert.AreEqual(20.0, lane.UnitSeconds, 1e-6, "10 live units for the 2 s since the first sample");
        Assert.AreEqual(10.0, lane.AlarmedUnitSeconds, 1e-6);
        Assert.AreEqual("west_outer_0", lane.AttackTarget);
        Assert.AreEqual(20.0, lane.AttackUnitSeconds, 1e-6, "the stream's units while it was ordered at the tower");
        Assert.AreEqual(4.0, lane.AttackFlaggedUnitSeconds, 1e-6);
        Assert.AreEqual(8.0, lane.AttackContactUnitSeconds, 1e-6);
    }

    [TestMethod]
    public void Field_AStreamNotOrderedAtATower_AddsNothingToTheLadder()
    {
        var sample = new FordsFieldSample(new[] { new FordsLaneSample(MatchSide.East, 1, 10, 100f, false, 0, -1f, null, 0, null, 5, 5) },
            new FordsTowerSample[0], new FordsPointSample[0], 0, 10, 0);

        _report.MissionTick(1f, 5.0);
        _report.Field(sample);
        _report.MissionTick(2f, 5.0);
        _report.Field(sample);

        var lane = Stream(MatchSide.East, 1);
        Assert.IsNull(lane.AttackTarget);
        Assert.AreEqual(0.0, lane.AttackUnitSeconds);
        Assert.AreEqual(0.0, lane.AttackFlaggedUnitSeconds);
        Assert.AreEqual(0.0, lane.AttackContactUnitSeconds);
    }

    private void EnemyLookups(object gunner, object enemy, object friend)
    {
        _report.BindGunnerLookup(agent => ReferenceEquals(agent, gunner) ? "east_outer_0" : null);
        _report.BindAttackerLookup(agent => ReferenceEquals(agent, enemy) ? new FordsAttacker(MatchSide.West, 1)
            : ReferenceEquals(agent, friend) ? new FordsAttacker(MatchSide.East, 0) : null);
    }

    [TestMethod]
    public void GunnerHit_AStreamOrderedAtTheTower_CountsOnlyBlowsFromEnemiesOfTheTowersOwner_BySideAndByStream()
    {
        var gunner = new object();
        var enemy = new object();
        var friend = new object();
        EnemyLookups(gunner, enemy, friend);
        Order(MatchSide.West, 1, "east_outer_0");

        _report.GunnerHit(gunner, enemy, false);
        _report.GunnerHit(gunner, enemy, false);
        _report.GunnerHit(gunner, enemy, true);
        _report.GunnerHit(gunner, friend, false);
        _report.GunnerHit(gunner, new object(), false);
        _report.GunnerHit(new object(), enemy, true);
        _report.GunnerHit(null, enemy, true);

        var tower = _report.Run!.Towers["east_outer_0"];
        Assert.AreEqual(2, tower.GunnerBlowsMelee);
        Assert.AreEqual(1, tower.GunnerBlowsRanged);
        Assert.AreEqual(3, tower.GunnerBlowsBySide[(int)MatchSide.West]);
        Assert.AreEqual(0, tower.GunnerBlowsBySide[(int)MatchSide.East], "a blow from the tower's own side is not counted");
        Assert.AreEqual(3, Stream(MatchSide.West, 1).GunnerBlows);
        Assert.AreEqual(0, Stream(MatchSide.East, 0).GunnerBlows);
        Assert.AreEqual(0, _report.Run.Towers["keep_east"].GunnerBlowsMelee);
    }

    [TestMethod]
    public void GunnerHit_AnAttackerThatIsNotALaneUnit_CountsOnTheTowerButOnNoStream()
    {
        var gunner = new object();
        var enemy = new object();
        _report.BindGunnerLookup(a => ReferenceEquals(a, gunner) ? "east_outer_0" : null);
        _report.BindAttackerLookup(a => ReferenceEquals(a, enemy) ? new FordsAttacker(MatchSide.West) : null);

        _report.GunnerHit(gunner, enemy, false);

        Assert.AreEqual(1, _report.Run!.Towers["east_outer_0"].GunnerBlowsBySide[(int)MatchSide.West]);
        Assert.IsTrue(_report.Run.Lanes.All(l => l.GunnerBlows == 0));
    }

    [TestMethod]
    public void GunnerHit_AStreamOrderedAtAnotherTower_CountsOnTheTowerButNotOnItsLadder()
    {
        var gunner = new object();
        var enemy = new object();
        EnemyLookups(gunner, enemy, new object());
        Order(MatchSide.West, 1, "keep_east");

        _report.GunnerHit(gunner, enemy, false);

        Assert.AreEqual(1, _report.Run!.Towers["east_outer_0"].GunnerBlowsMelee, "the tower's own count does not depend on any order");
        Assert.AreEqual(0, Stream(MatchSide.West, 1).GunnerBlows, "the stream was ordered at keep_east, not at the gunner's tower");
    }

    [TestMethod]
    public void GunnerHit_WithNoLookupBound_OrAfterItIsCleared_CountsNothing()
    {
        var gunner = new object();
        var enemy = new object();

        _report.GunnerHit(gunner, enemy, false);
        EnemyLookups(gunner, enemy, new object());
        _report.BindGunnerLookup(null);
        _report.GunnerHit(gunner, enemy, false);

        Assert.AreEqual(0, _report.Run!.Towers["east_outer_0"].GunnerBlowsMelee);
    }

    [TestMethod]
    public void GunnerHit_FromManyThreads_LosesNoBlow()
    {
        var gunner = new object();
        var enemy = new object();
        EnemyLookups(gunner, enemy, new object());

        Parallel.For(0, 8, _ =>
        {
            for (var i = 0; i < 500; i++)
                _report.GunnerHit(gunner, enemy, i % 2 == 0);
        });

        var tower = _report.Run!.Towers["east_outer_0"];
        Assert.AreEqual(2000, tower.GunnerBlowsMelee);
        Assert.AreEqual(2000, tower.GunnerBlowsRanged);
    }

    [TestMethod]
    public void TowerBlow_CountsTheBlowOnTheTowerBySide_AndOnTheLadderOfTheStreamOrderedAtThatTower()
    {
        Order(MatchSide.West, 1, "east_outer_0");

        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 8, new FordsAttacker(MatchSide.West, 1));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Locked, 10, 0, new FordsAttacker(MatchSide.West, 1));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, TowerDamageReason.Applied, 5, 5, null);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Hero, TowerDamageReason.Applied, 5, 5, new FordsAttacker(MatchSide.West));

        var tower = _report.Run!.Towers["east_outer_0"];
        Assert.AreEqual(3, tower.BlowsBySide[(int)MatchSide.West]);
        Assert.AreEqual(0, tower.BlowsBySide[(int)MatchSide.East]);
        Assert.AreEqual(1, tower.SideUnknownApplied);
        var stream = Stream(MatchSide.West, 1);
        Assert.AreEqual(2, stream.TowerBlows);
        Assert.AreEqual(1, stream.AppliedBlows);
    }

    [TestMethod]
    public void TowerBlow_AStreamOrderedAtTowerA_WhoseUnitsStrikeTowerB_CountsNothingOnItsLadder()
    {
        Order(MatchSide.West, 1, "east_outer_0");

        _report.TowerBlow("keep_east", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 8, new FordsAttacker(MatchSide.West, 1));
        _report.TowerBlow("keep_east", TowerBlowClass.Melee, TowerDamageReason.Locked, 10, 0, new FordsAttacker(MatchSide.West, 1));

        var stream = Stream(MatchSide.West, 1);
        Assert.AreEqual(0, stream.TowerBlows);
        Assert.AreEqual(0, stream.AppliedBlows);
        Assert.AreEqual(2, _report.Run!.Towers["keep_east"].BlowsBySide[(int)MatchSide.West], "the tower's own count still hears both blows");
    }

    [TestMethod]
    public void TowerBlow_AStreamWhoseOrderTheLookNoLongerSees_CountsNothingOnItsLadder()
    {
        Order(MatchSide.West, 1, "east_outer_0");
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 8, new FordsAttacker(MatchSide.West, 1));

        Order(MatchSide.West, 1, null);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 8, new FordsAttacker(MatchSide.West, 1));

        Assert.AreEqual(1, Stream(MatchSide.West, 1).TowerBlows, "only the blow struck under the order");
        Assert.AreEqual("east_outer_0", Stream(MatchSide.West, 1).AttackTarget, "the last target stays on the ladder for the report");
    }

    [TestMethod]
    public void TowerBlow_AppliedDamage_IsSummedPerClass_ForBlowsThatTookHitPoints()
    {
        var by = new FordsAttacker(MatchSide.West, 0);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 8, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 10, 4, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, TowerDamageReason.Applied, 5, 5, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Locked, 99, 0, by);

        var applied = _report.Run!.Towers["east_outer_0"].AppliedDamage;
        Assert.AreEqual(12, applied[(int)TowerBlowClass.Melee]);
        Assert.AreEqual(5, applied[(int)TowerBlowClass.Ranged]);
        Assert.AreEqual(0, applied[(int)TowerBlowClass.Hero]);
    }

    [TestMethod]
    public void TowerBlow_MeleeRawDamage_KeepsCountMinMedianAndMax_AndLeavesOtherClassesOut()
    {
        var by = new FordsAttacker(MatchSide.West, 0);
        foreach (var raw in new[] { 100, 0, 10, 0, 5 })
            _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, raw == 0 ? TowerDamageReason.RawZero : TowerDamageReason.Applied, raw, raw, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Ranged, TowerDamageReason.Applied, 1000, 1000, by);

        var summary = _report.Run!.Towers["east_outer_0"].MeleeRawSummary();

        Assert.AreEqual(5, summary.Count);
        Assert.AreEqual(0, summary.Min);
        Assert.AreEqual(5.0, summary.Median, 1e-9);
        Assert.AreEqual(100, summary.Max);
    }

    [TestMethod]
    public void TowerBlow_AWeaponlessMeleeBlow_IsCountedButLeftOutOfTheRawDamageSummary()
    {
        var by = new FordsAttacker(MatchSide.West, 0);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Weaponless, 0, 0, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.RawZero, 0, 0, by);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 12, 12, by);

        var tower = _report.Run!.Towers["east_outer_0"];
        var summary = tower.MeleeRawSummary();

        Assert.AreEqual(1, tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.Weaponless], "the blow is still counted by its reason");
        Assert.AreEqual(2, summary.Count, "a kick has no weapon damage to summarize; a real zero-damage swing does");
        Assert.AreEqual(0, summary.Min);
        Assert.AreEqual(12, summary.Max);
    }

    [TestMethod]
    public void TowerBlow_AnOwnSideMeleeBlow_IsCountedButLeftOutOfTheRawDamageSummary()
    {
        var by = new FordsAttacker(MatchSide.West, 0);
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.OwnSide, 0, 0, new FordsAttacker(MatchSide.East, 0));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.OwnSide, 25, 0, new FordsAttacker(MatchSide.East, 0));
        _report.TowerBlow("east_outer_0", TowerBlowClass.Melee, TowerDamageReason.Applied, 12, 12, by);

        var tower = _report.Run!.Towers["east_outer_0"];
        var summary = tower.MeleeRawSummary();

        Assert.AreEqual(2, tower.Blows[(int)TowerBlowClass.Melee, (int)TowerDamageReason.OwnSide], "the blows are still counted by their reason");
        Assert.AreEqual(1, summary.Count, "friendly blows are not the enemy's melee damage");
        Assert.AreEqual(12, summary.Min);
        Assert.AreEqual(12, summary.Max);
    }

    [TestMethod]
    public void TowerBlow_MeleeRawDamage_WithNoMeleeBlow_SummarizesToZeroes()
    {
        var summary = _report.Run!.Towers["east_outer_0"].MeleeRawSummary();

        Assert.AreEqual(0, summary.Count);
        Assert.AreEqual(0.0, summary.Median);
    }

    [TestMethod]
    public void TowerHealed_SumsWhatRegenerationGaveBack_AndIgnoresAnUnknownTower()
    {
        _report.TowerHealed("east_outer_0", 30);
        _report.TowerHealed("east_outer_0", 12);
        _report.TowerHealed("not_a_tower", 99);

        Assert.AreEqual(42, _report.Run!.Towers["east_outer_0"].HealedTotal);
    }

    [TestMethod]
    public void TowerRulesHp_SeveralLooks_KeepsTheLowestSeen()
    {
        Assert.IsNull(_report.Run!.Towers["east_outer_0"].MinRulesHp);

        _report.TowerRulesHp("east_outer_0", 900);
        _report.TowerRulesHp("east_outer_0", 500);
        _report.TowerRulesHp("east_outer_0", 700);
        _report.TowerRulesHp("not_a_tower", 1);

        Assert.AreEqual(500, _report.Run.Towers["east_outer_0"].MinRulesHp);
    }

    [TestMethod]
    public void TowerConfig_LogsTheEffectiveNumbersOnce_AndKeepsThemOnTheRun()
    {
        var config = FordsMatchConfig.Default.Towers with { TowerHitPoints = 800000, RegenPerSecond = 2.5f, RegenDelaySeconds = 30f, ThrustFloorFactor = 0.3f };

        _report.TowerConfig(config);

        var line = Lines("Tower").Single(l => l.Contains(" config "));
        StringAssert.Contains(line, "towerHp=800000 innerHp=" + config.InnerTowerHitPoints + " keepHp=" + config.KeepHitPoints);
        StringAssert.Contains(line, "regenPerSecond=2.500 regenDelaySeconds=30.0");
        StringAssert.Contains(line, "thrustFloor=0.300");
        StringAssert.Contains(line, "rangedFactor=" + config.RangedDamageFactor.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        StringAssert.Contains(line, "heroFactor=");
        StringAssert.Contains(line, "siegeFactor=");
        StringAssert.Contains(line, _report.Run!.Start.TowerConfig);
    }

    /// <summary>A 1 Hz look at which the stream is marching on <paramref name="tower"/> (null: it is not) with <paramref name="past"/> of 10 units past it.</summary>
    private void Marching(MatchSide side, int lane, string? tower, int past) =>
        _report.Field(new FordsFieldSample(new[] { new FordsLaneSample(side, lane, 10, 100f, false, 0, -1f, null, 0, null, 0, 0, tower, past) },
            new FordsTowerSample[0], new FordsPointSample[0], 0, 10, 0));

    [TestMethod]
    public void Field_AMarchingStreamWithMostUnitsPastTheTowerOnTwoLooksInARow_IsAFailingRunWithTheFirstTimeAndTheWorstShare()
    {
        _report.MissionTick(3f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 2);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 6);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 9);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 3);

        var stream = Stream(MatchSide.West, 0);
        Assert.AreEqual(4, stream.MarchSamples, "every look at a marching stream is judged");
        Assert.AreEqual(2, stream.PastSamples, "6 and 9 of 10 are more than half; 2 and 3 are not");
        Assert.AreEqual(2, stream.MaxPastRun);
        Assert.AreEqual(0.9, stream.MaxPastShare, 1e-9);
        Assert.AreEqual("tower_east_0_0", stream.PastTower);
        Assert.AreEqual(5.0, stream.FirstPastMt!.Value, 1e-9, "the second of the two looks in a row");
        Assert.AreEqual(1, Lines("Lane").Count(l => l.Contains("bypass side=West lane=0")), "logged once per stream");
    }

    [TestMethod]
    public void Field_ALookThatIsNotAMarchOrNotMostlyPast_BreaksTheRun()
    {
        Marching(MatchSide.West, 0, "tower_east_0_0", 8);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, null, 0);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 8);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 5);
        _report.MissionTick(1f, 5.0);
        Marching(MatchSide.West, 0, "tower_east_0_0", 8);

        var stream = Stream(MatchSide.West, 0);
        Assert.AreEqual(4, stream.MarchSamples, "the look with no march is not judged");
        Assert.AreEqual(3, stream.PastSamples, "the three looks with 8 of 10 past");
        Assert.AreEqual(1, stream.MaxPastRun, "exactly half past (5 of 10) is not more than half and resets the run");
        Assert.IsNull(stream.FirstPastMt);
        Assert.AreEqual(0, Lines("Lane").Count(l => l.Contains("bypass side=")));
    }

    [TestMethod]
    public void ExceptionSwallowed_CountsWhileTheRunIsLive_AndKeepsTheFirstTypeAndSite()
    {
        _report.ExceptionsWatched();

        _report.ExceptionSwallowed("IndexOutOfRangeException", "Patch37.Tick");
        _report.ExceptionSwallowed("NullReferenceException", "Patch12.Other");

        var e = _report.Run!.Exceptions;
        Assert.IsTrue(e.Watching);
        Assert.AreEqual(2, e.Count);
        Assert.AreEqual("IndexOutOfRangeException", e.FirstType);
        Assert.AreEqual("Patch37.Tick", e.FirstSite);
        Assert.IsTrue(Lines("Exceptions").Any(l => l.Contains("swallowed type=IndexOutOfRangeException site=\"Patch37.Tick\"")));
    }

    [TestMethod]
    public void ExceptionSwallowed_AfterTheMissionClosed_IsNotCounted()
    {
        _report.ExceptionsWatched();
        _report.MissionClosed();

        _report.ExceptionSwallowed("IndexOutOfRangeException", "Patch37.Tick");

        Assert.AreEqual(0, _report.Run!.Exceptions.Count);
    }

    [TestMethod]
    public void Layout_LogsOneLine_AndStoresTheFactsOnTheRun()
    {
        var facts = new FordsLayoutFacts("scene_a", new Vec3f(0, 0, 0), new Vec3f(900, 0, 0), new[] { 25, 24 },
            new[] { "keep_west", "keep_east" }, new[] { "tower_east_1_0" }, false, true);

        _report.Layout(facts);

        var lines = Lines("Layout");
        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "scene=scene_a");
        StringAssert.Contains(lines[0], "missing=tower_east_1_0");
        var layout = _report.Run!.Start.Layout;
        Assert.IsTrue(layout.Reported);
        Assert.AreEqual("scene_a", layout.Scene);
        CollectionAssert.AreEqual(new[] { 25, 24 }, layout.LaneWaypoints);
        CollectionAssert.AreEqual(new[] { "tower_east_1_0" }, layout.MissingTowerIds);
        Assert.IsTrue(layout.KeepOpenEast);
        Assert.IsFalse(layout.KeepOpenWest);
    }
}
