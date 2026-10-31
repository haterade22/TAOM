using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

/// <summary>What the director tells the run report: the evidence each seam leaves, without touching how the match plays.</summary>
[TestClass]
public class FordsMissionDirectorReportTests
{
    private const string EastOuterLane0 = "tower_east_0_0";
    private const string EastInnerLane0 = "tower_east_0_1";

    [TestInitialize]
    public void ResetThreadGuard() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void ClearThreadGuard() => MissionThreadGuard.ResetForTests();

    private static FordsDirectorFixture Started(System.Func<FordsMatchConfig, FordsMatchConfig>? tweak = null)
    {
        var fixture = FordsDirectorFixture.Create(tweak);
        Assert.IsTrue(fixture.Director.Start());
        return fixture;
    }

    private static FordsMatchConfig WithFreeWaves(FordsMatchConfig c) =>
        c with { Waves = c.Waves with { FreeWaveIntervalSeconds = 40f } };

    private static FordsMatchConfig RichPlayers(FordsMatchConfig c) =>
        c with { Economy = c.Economy with { StartingGold = 5000 } };

    private static long Blows(FordsDirectorFixture f, string towerId, TowerBlowClass cls, TowerDamageReason reason) =>
        f.Report.Run!.Towers[towerId].Blows[(int)cls, (int)reason];

    private static void Second(FordsDirectorFixture f) => f.Report.MissionTick(1f, 1.0);

    // --- setup -------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Start_ReportsTowersBuiltAgainstExpected_AndMatchStarted()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == EastOuterLane0), Arg.Any<TowerPrefab>(), Arg.Any<int>()).Returns(false);

        Assert.IsTrue(f.Director.Start());

        var run = f.Report.Run!;
        Assert.AreEqual(13, run.Start.TowersBuilt);
        Assert.AreEqual(14, run.Start.TowersExpected);
        Assert.AreEqual(2, run.Start.KeepsBuilt);
        Assert.AreEqual(3, run.Facts!.Lanes);
        Assert.AreEqual(f.Plan.Config.Towers.FireRange, run.Facts.FireRange);
        Assert.IsTrue(run.Stages.Any(s => s.Name == "matchStarted"));
    }

    [TestMethod]
    public void Start_TheFactsCarryTheDistanceBetweenTheTwoKeeps()
    {
        var f = Started();

        var west = f.TowerSlot("keep_west");
        var east = f.TowerSlot("keep_east");
        Assert.IsTrue(east.Position.Distance2D(west.Position) > 100f);
        Assert.AreEqual(east.Position.Distance2D(west.Position), f.Report.Run!.Facts!.BaseDistance, 0.01f);
    }

    [TestMethod]
    public void Start_AFitterThatDropsATower_StillCountsItAsExpected_AndTheTowersPartFails()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call =>
        {
            var layout = call.Arg<MapLayout>();
            var kept = layout.Towers.Where(t => t.Id != EastOuterLane0).ToList();
            return new FordsLayoutFit(layout with { Towers = kept }, 0, 1, 0);
        });

        Assert.IsTrue(f.Director.Start());

        var run = f.Report.Run!;
        Assert.AreEqual(14, run.Start.TowersExpected, "counted from the layout before the fitter dropped one");
        Assert.AreEqual(13, run.Start.TowersBuilt);
        var row = FordsVerdicts.Judge(new FordsSessionEvidence(), run)[3];
        Assert.AreEqual(FordsResult.Fail, row.Parts.Single(p => p.Name == "towers").Result);
    }

    [TestMethod]
    public void Start_ReportsMatchStartedBeforeTheHookCanBeInstalled()
    {
        var f = Started();

        // The tower table is bound by the time Start returns, which is before the mission logic installs Patch131.
        Assert.AreEqual(14, f.Report.Run!.Towers.Count);
        f.ReportsTowerId(EastOuterLane0);
        f.Director.AdjustTowerDamage(new object(), null, false, 100);
        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Other, TowerDamageReason.Applied));
    }

    [TestMethod]
    public void Start_PreflightReportsMissingIds()
    {
        var f = FordsDirectorFixture.Create();
        f.Battlefield.CharacterExists("hero_a").Returns(false);

        Assert.IsTrue(f.Director.Start());

        var start = f.Report.Run!.Start;
        CollectionAssert.AreEqual(new[] { "hero_a" }, start.MissingIds);
        Assert.AreEqual(7, start.IdsChecked, "footman, spearman, archer, cave_troll, elite_guard, hero_a, hero_b, each once");
    }

    [TestMethod]
    public void Start_ReportsTheVolleyItem()
    {
        var f = FordsDirectorFixture.Create();
        var info = new FordsVolleyItemInfo("western_javelin_2_t3", 30, 40f, 1000f);
        f.Towers.VolleyItemInfo().Returns(info);

        Assert.IsTrue(f.Director.Start());

        Assert.AreSame(info, f.Report.Run!.Volley.Item);
        Assert.IsTrue(f.Report.Run.Volley.ItemReported);
        Assert.IsFalse(f.Report.Run.Volley.ReachShort);
    }

    // --- the tick ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_FirstLiveTick_StampsLiveOnce()
    {
        var f = Started();

        f.Director.Tick(1f);
        f.Director.Tick(1f);

        Assert.AreEqual(1, f.Report.Run!.Stages.Count(s => s.Name == "live"));
        Assert.IsTrue(f.Report.Run.Live);
    }

    [TestMethod]
    public void Tick_ProbesOncePerMissionSecond_TowerHealthEveryFifth()
    {
        var f = Started();
        f.Towers.HitPoints(Arg.Any<string>()).Returns(100f);
        f.Sample(1, MatchSide.West, 0, new Vec3f(100f, 0f, 0f));
        f.Sample(2, MatchSide.West, 0, new Vec3f(110f, 0f, 0f));

        Second(f);
        f.Director.Tick(1f);
        Assert.AreEqual(2, f.Report.Run!.Perf.PeakLiveWest, "the first tick probes");
        f.Sample(3, MatchSide.West, 0, new Vec3f(120f, 0f, 0f));
        f.Director.Tick(0.1f);
        Assert.AreEqual(2, f.Report.Run.Perf.PeakLiveWest, "no second probe within the same mission second");
        for (var i = 0; i < 3; i++)
        {
            Second(f);
            f.Director.Tick(1f);
        }

        Assert.AreEqual(3, f.Report.Run.Perf.PeakLiveWest);
        f.Towers.DidNotReceiveWithAnyArgs().HitPoints(default!);

        Second(f);
        f.Director.Tick(1f);

        f.Towers.Received(14).HitPoints(Arg.Any<string>());
    }

    [TestMethod]
    public void SpawnOne_NoLane_ReportsSpawnFailed()
    {
        var f = Started();
        var spawnOne = typeof(FordsMissionDirector).GetMethod("SpawnOne", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var request = new SpawnRequest(1, MatchSide.West, 99, "footman", 1, FordsMissionDirector.HumanSlotId, UnitRole.Infantry, false);

        var spawned = (bool)spawnOne.Invoke(f.Director, new object[] { request })!;

        Assert.IsFalse(spawned);
        Assert.AreEqual(1, f.Report.Run!.Spawn.FailedByTroop["footman"]);
        f.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("reason=no-lane")));
    }

    [TestMethod]
    public void HeroSpawn_ReportsTheRequestedLane()
    {
        var f = Started(RichPlayers);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixtureHero));

        f.Director.Tick(1f);

        var spawned = f.Report.Run!.Heroes.Single(h => h.Kind == "spawned");
        Assert.AreEqual(FordsMatchFixtureHero, spawned.HeroId);
        Assert.AreEqual(0, spawned.RequestLane);
        Assert.AreEqual(1, f.Report.Run.Spawn.Spawned);
    }

    [TestMethod]
    public void Tick_ALaneOrderTheEngineRefusesEveryTick_IsReportedOnce()
    {
        var f = Started();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false);
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        for (var i = 0; i < 10; i++)
            f.Director.Tick(1f);

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[Lane]") && m.Contains("side=West lane=0") && m.Contains("given=False")));
    }

    [TestMethod]
    public void Tick_ARefusedOrderThatIsLaterGiven_IsReportedAsGiven()
    {
        var f = Started();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false, false, false, false, true);
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        for (var i = 0; i < 3; i++)
            f.Director.Tick(1f);

        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[Lane]") && m.Contains("side=West lane=0") && m.Contains("given=False")));
        f.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("[Lane]") && m.Contains("side=West lane=0") && m.Contains("given=True")));
    }

    private const string FordsMatchFixtureHero = "hero_a";

    // --- blows ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void AdjustTowerDamage_ReportsClassAndOutcome_AndReturnsTheSameDamage()
    {
        var f = Started(RichPlayers);
        f.ReportsTowerId(EastInnerLane0);
        Assert.AreEqual(0, f.Director.AdjustTowerDamage(new object(), null, false, 100), "locked behind the outer tower");
        Assert.AreEqual(1, Blows(f, EastInnerLane0, TowerBlowClass.Other, TowerDamageReason.Locked));

        f.ReportsTowerId(EastOuterLane0);
        Assert.AreEqual(15, f.Director.AdjustTowerDamage(new object(), null, true, 100));
        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Other, TowerDamageReason.Applied));

        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixtureHero));
        f.Director.Tick(1f);
        var attacker = new object();
        f.Battlefield.TryGetKey(attacker, out Arg.Any<AgentKey>()).Returns(call =>
        {
            call[1] = new AgentKey(1);
            return true;
        });
        Assert.AreEqual(400, f.Director.AdjustTowerDamage(new object(), attacker, false, 100));
        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Hero, TowerDamageReason.Applied));
    }

    [TestMethod]
    public void NoteUnappliedBlow_ReportsRawZeroAndWeaponless()
    {
        var f = Started();
        f.ReportsTowerId(EastOuterLane0);

        f.Director.NoteUnappliedBlow(new object(), null, false, false);
        f.Director.NoteUnappliedBlow(new object(), null, true, false);
        f.Director.NoteUnappliedBlow(new object(), null, true, false);

        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Other, TowerDamageReason.RawZero));
        Assert.AreEqual(2, Blows(f, EastOuterLane0, TowerBlowClass.Other, TowerDamageReason.Weaponless));
    }

    [TestMethod]
    public void NoteUnappliedBlow_Ranged_ReportsRangedClass()
    {
        var f = Started(RichPlayers);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyPack(0, Domain.FordsMatchFixture.Footmen.OfferId));
        f.Director.Tick(1f);
        f.ReportsTowerId(EastOuterLane0);
        var attacker = new object();
        f.Battlefield.TryGetKey(attacker, out Arg.Any<AgentKey>()).Returns(call =>
        {
            call[1] = new AgentKey(1);
            return true;
        });

        f.Director.NoteUnappliedBlow(new object(), attacker, false, false);
        f.Director.NoteUnappliedBlow(new object(), attacker, false, true);

        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Melee, TowerDamageReason.RawZero));
        Assert.AreEqual(1, Blows(f, EastOuterLane0, TowerBlowClass.Ranged, TowerDamageReason.RawZero));
    }

    [TestMethod]
    public void NoteUnappliedBlow_NonTower_ReportsNothing()
    {
        var f = Started();
        f.ReportsTowerId(null);

        f.Director.NoteUnappliedBlow(new object(), null, false, false);

        Assert.IsTrue(f.Report.Run!.Towers.Values.All(t => t.Blows.Cast<long>().All(b => b == 0)));
    }

    // --- kills, events, the end ----------------------------------------------------------------------------------------

    [TestMethod]
    public void OnAgentRemoved_ReportsDeathAndCredit()
    {
        var f = Started(WithFreeWaves);
        f.Director.Tick(1f);
        var west = f.Spawned.Select((s, i) => (s, key: new AgentKey(i + 1))).First(x => x.s.Side == MatchSide.West).key;
        var east = f.Spawned.Select((s, i) => (s, key: new AgentKey(i + 1))).First(x => x.s.Side == MatchSide.East).key;
        f.ReportsRemoval(east, west);

        f.Director.OnAgentRemoved(new object(), new object(), true);

        var kills = f.Report.Run!.Kills;
        Assert.AreEqual(1, kills.DeathsEast);
        Assert.AreEqual(1, kills.Credit["West:Paid"]);
    }

    [TestMethod]
    public void ApplyEffects_HeroAndCaptureEvents_AreReported()
    {
        MapLayout? layout = null;
        var f = FordsDirectorFixture.Create(c => RichPlayers(c) with { Capture = c.Capture with { SecondsToCapture = 2f } });
        f.Scene.Fit(Arg.Any<MapLayout>()).Returns(call =>
        {
            layout = call.Arg<MapLayout>();
            return new FordsLayoutFit(layout, 0, 0, 0);
        });
        Assert.IsTrue(f.Director.Start());
        var point = layout!.CapturePoints.First();
        f.Sample(50, MatchSide.West, 0, point.Position);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixtureHero));

        for (var i = 0; i < 6; i++)
            f.Director.Tick(1f);

        Assert.IsTrue(f.Report.Run!.Heroes.Any(h => h.Kind == "fielded" && h.HeroId == FordsMatchFixtureHero));
        Assert.IsTrue(f.Report.Run.Points[point.Id].Captures >= 1);
    }

    [TestMethod]
    public void Winner_IsReported()
    {
        var f = Started();

        foreach (var id in new[] { EastOuterLane0, EastInnerLane0, "keep_east" })
            f.DestroyedHandler!(id);
        f.Director.Tick(1f);

        var run = f.Report.Run!;
        Assert.AreEqual("West", run.End.Winner);
        Assert.IsNotNull(run.End.KeepFellMt, "the keep's fall is reported by the deferred tower handler");
        Assert.IsNotNull(run.Towers[EastOuterLane0].FellMt);
    }

    [TestMethod]
    public void Start_RecordsTheEngineAgentLimitInTheFacts()
    {
        var f = FordsDirectorFixture.Create();
        f.Battlefield.MaxAgents.Returns(2048);

        Assert.IsTrue(f.Director.Start());

        Assert.AreEqual(2048, f.Report.Run!.Facts!.AgentLimit);
    }

    [TestMethod]
    public void Volley_CountsTheMissilesTheAdapterAdded_AndARefusalAsNoGunner()
    {
        var f = Started();
        var tower = f.TowerSlot("tower_west_1_0");
        var enemyAt = tower.Position + new Vec3f(12f, 0f, 0f);
        f.Sample(77, MatchSide.East, 1, enemyAt);
        f.Battlefield.PositionOf(new AgentKey(77)).Returns(enemyAt);
        f.Towers.Fire("tower_west_1_0", enemyAt, 3).Returns(2);

        f.Director.Tick(1f);

        Assert.AreEqual(2, f.Report.Run!.Volley.Launched, "two of three missiles had an aim");

        f.Towers.Fire("tower_west_1_0", enemyAt, 3).Returns(0);
        f.Director.Tick(10f);

        Assert.AreEqual(2, f.Report.Run.Volley.Launched);
        Assert.IsTrue(f.Report.Run.Volley.NoGunnerOrItem >= 1);
    }
}
