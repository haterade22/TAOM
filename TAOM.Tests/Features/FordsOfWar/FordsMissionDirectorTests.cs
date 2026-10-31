using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsMissionDirectorTests
{
    private const string EastOuterLane0 = "tower_east_0_0";
    private const string EastInnerLane0 = "tower_east_0_1";
    private const string WestOuterLane0 = "tower_west_0_0";

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

    // --- setup -------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Start_BuildsEveryTowerAndKeepWithItsHitPointsAndPrefab()
    {
        var f = Started();

        Assert.AreEqual(14, f.TowersSpawned.Count);
        Assert.AreEqual(8000, f.TowersSpawned.Single(t => t.Slot.Id == "tower_west_1_0").HitPoints);
        Assert.AreEqual(11000, f.TowersSpawned.Single(t => t.Slot.Id == "tower_east_2_1").HitPoints);
        var keep = f.TowersSpawned.Single(t => t.Slot.Id == "keep_east");
        Assert.AreEqual(18000, keep.HitPoints);
        Assert.AreEqual(TowerPrefab.DefaultKeep, keep.Prefab);
        Assert.AreEqual(TowerPrefab.DefaultTower, f.TowersSpawned.Single(t => t.Slot.Id == "tower_west_0_0").Prefab);
    }

    [TestMethod]
    public void Start_PreparesThreeLanesSetsCorpseFadeAndCompletesDeployment()
    {
        var f = Started();

        f.Battlefield.Received(1).Prepare(3);
        f.Battlefield.Received(1).SetCorpseFadeOutSeconds(4f);
        f.Battlefield.Received(1).CompleteDeployment();
    }

    [TestMethod]
    public void Start_GivesEveryTowerItsGarrisonOfTheSidesFirstArcher()
    {
        var f = Started();

        f.Towers.Received(14).SpawnGunners(Arg.Any<TowerSlot>(), "archer", 2);
        f.Towers.Received(1).SetDestroyedHandler(Arg.Any<System.Action<string>>());
    }

    [TestMethod]
    public void Start_FillsTheSessionWithTheWestSlotAndTheCameraAboveTheWestKeep()
    {
        var f = Started();

        Assert.IsTrue(f.Session.IsActive);
        Assert.AreEqual(MatchSide.West, f.Session.GetView()!.Side);
        Assert.AreEqual(FordsMissionDirector.HumanSlotId, f.Session.GetView()!.Slot);
        var keep = f.TowerSlot("keep_west");
        Assert.IsTrue(f.Session.CameraStart.Z > keep.Position.Z + 20f);
        Assert.IsTrue(f.Session.CameraStart.X < f.Session.CameraLookAt.X);
        Assert.AreEqual(3, f.Session.BoundaryPolygon.Count);
    }

    [TestMethod]
    public void Start_BuildsTheMatchWithAHumanWestSlotAndAnAiEastSlotOnTheChosenProfile()
    {
        var f = Started();

        var view = f.Session.GetView()!;
        Assert.AreEqual(MatchSide.West, view.Side);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, view.OwnedLanes.ToArray());
        Assert.AreEqual(600, view.Gold);
    }

    [TestMethod]
    public void Start_PrepareFails_ReturnsFalseAndBuildsNothing()
    {
        var f = FordsDirectorFixture.Create();
        f.Battlefield.Prepare(Arg.Any<int>()).Returns(false);

        Assert.IsFalse(f.Director.Start());

        Assert.AreEqual(0, f.TowersSpawned.Count);
        Assert.IsFalse(f.Session.IsActive);
        f.Logger.Received().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Start_SceneGivesNoBasePoints_ReturnsFalse()
    {
        var f = FordsDirectorFixture.Create();
        f.Scene.TryGetBasePoints(Arg.Any<float>(), out Arg.Any<Vec3f>(), out Arg.Any<Vec3f>()).Returns(false);

        Assert.IsFalse(f.Director.Start());

        Assert.AreEqual(0, f.TowersSpawned.Count);
    }

    [TestMethod]
    public void Start_TagsLayoutIncomplete_FallsBackToGeneratedWithOneWarning()
    {
        var f = FordsDirectorFixture.Create(mode: MapLayoutMode.Tags);
        MapLayout none;
        string problem;
        f.Scene.TryReadTaggedLayout(Arg.Any<string>(), Arg.Any<CampDefaults>(), out none, out problem)
            .Returns(call =>
            {
                call[2] = null;
                call[3] = "fow_keep_east is missing";
                return false;
            });

        Assert.IsTrue(f.Director.Start());

        Assert.AreEqual(14, f.TowersSpawned.Count);
        f.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("fow_keep_east is missing")));
    }

    [TestMethod]
    public void Start_TagsLayoutComplete_UsesTheScenesOwnLayout()
    {
        var f = FordsDirectorFixture.Create(mode: MapLayoutMode.Tags, lanes: 2);
        var authored = FordsTestLayout.Create();
        MapLayout none;
        string problem;
        f.Scene.TryReadTaggedLayout(Arg.Any<string>(), Arg.Any<CampDefaults>(), out none, out problem)
            .Returns(call =>
            {
                call[2] = authored;
                call[3] = string.Empty;
                return true;
            });

        Assert.IsTrue(f.Director.Start());

        Assert.AreEqual(authored.Towers.Count, f.TowersSpawned.Count);
        f.Battlefield.Received(1).Prepare(2);
        f.Scene.DidNotReceive().TryGetBasePoints(Arg.Any<float>(), out Arg.Any<Vec3f>(), out Arg.Any<Vec3f>());
    }

    [TestMethod]
    public void Start_AnOuterTowerThatCannotBeBuilt_IsLeftOutOfTheMatchWithAWarning()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == EastOuterLane0), Arg.Any<TowerPrefab>(), Arg.Any<int>()).Returns(false);

        Assert.IsTrue(f.Director.Start());

        var towers = f.Session.GetView()!.Lanes.Single(l => l.Index == 0).Towers;
        Assert.IsFalse(towers.Any(t => t.Id == EastOuterLane0));
        Assert.IsTrue(towers.Any(t => t.Id == EastInnerLane0));
        f.Logger.Received().LogWarning(Arg.Is<string>(m => m.Contains(EastOuterLane0)));
    }

    [TestMethod]
    public void Start_AKeepThatCannotBeBuilt_AbortsTheMatch()
    {
        var f = FordsDirectorFixture.Create();
        f.Towers.SpawnTower(Arg.Is<TowerSlot>(s => s.Id == "keep_west"), Arg.Any<TowerPrefab>(), Arg.Any<int>()).Returns(false);

        Assert.IsFalse(f.Director.Start());

        Assert.IsFalse(f.Session.IsActive);
        f.Logger.Received().LogError(Arg.Is<string>(m => m.Contains("keep_west")));
    }

    // --- the tick ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_BeforeStart_DoesNothing()
    {
        var f = FordsDirectorFixture.Create();

        f.Director.Tick(1f);

        f.Battlefield.DidNotReceiveWithAnyArgs().SampleUnits();
    }

    [TestMethod]
    public void Tick_FirstTickWithFreeWaves_SpawnsTheWaveForEveryLaneAtItsOwnBase()
    {
        var f = Started(WithFreeWaves);

        f.Director.Tick(1f);

        Assert.AreEqual(30, f.Spawned.Count);
        Assert.IsTrue(f.Spawned.All(s => s.Troop == "footman"));
        Assert.AreEqual(15, f.Spawned.Count(s => s.Side == MatchSide.West && s.Position.X < 100f));
        Assert.AreEqual(15, f.Spawned.Count(s => s.Side == MatchSide.East && s.Position.X > 900f));
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, f.Spawned.Select(s => s.Lane).Distinct().ToArray());
    }

    [TestMethod]
    public void Tick_MoreRequestsThanTheSpawnBudget_SpreadsThemOverTheNextTicks()
    {
        var f = Started(c => RichPlayers(c) with { Waves = c.Waves with { MaxSpawnsPerTick = 1 } });
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixture.HeroA.HeroId));
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(1, FordsMatchFixture.HeroB.HeroId));

        f.Director.Tick(1f);
        Assert.AreEqual(1, f.Spawned.Count);

        f.Director.Tick(1f);
        Assert.AreEqual(2, f.Spawned.Count);
        CollectionAssert.AreEquivalent(new[] { "hero_a", "hero_b" }, f.Spawned.Select(s => s.Troop).ToArray());
    }

    [TestMethod]
    public void Tick_AFailedSpawn_DropsTheRestOfThatRequestWithOneWarning()
    {
        var f = Started(WithFreeWaves);
        f.Battlefield.Spawn("footman", Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<Vec3f>(), Arg.Any<Vec3f>()).Returns((FordsSpawnedAgent?)null);

        f.Director.Tick(1f);
        f.Director.Tick(1f);

        f.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("footman")));
    }

    [TestMethod]
    public void Tick_AFailedHeroSpawn_TellsTheMatchSoTheGoldComesBackAndTheHeroSlotIsFree()
    {
        var f = Started(RichPlayers);
        var hero = FordsMatchFixture.HeroA.HeroId;
        f.Battlefield.Spawn(hero, Arg.Any<MatchSide>(), Arg.Any<int>(), Arg.Any<Vec3f>(), Arg.Any<Vec3f>()).Returns((FordsSpawnedAgent?)null);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, hero));

        f.Director.Tick(1f);

        var view = f.Session.GetView()!;
        Assert.AreEqual(5000, view.Gold);
        Assert.IsFalse(view.Heroes.Any(h => h.HeroId == hero));
        Assert.IsFalse(view.HeroOffers.Any(o => o.HeroId == hero));
        Assert.AreEqual(CommandResult.UnknownOffer, f.Session.TryBuyHero(0, hero));
    }

    [TestMethod]
    public void Tick_SameCommandOnTwoTicks_IsAppliedOnceAndAChangedOneAgain()
    {
        var f = Started();
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);
        f.Director.Tick(1f);
        f.Battlefield.Received(1).Move(MatchSide.West, 0, Arg.Any<Vec3f>());

        f.Samples.Clear();
        f.Sample(1, MatchSide.West, 0, new Vec3f(300f, -45f, 0f));
        f.Director.Tick(1f);

        f.Battlefield.Received(2).Move(MatchSide.West, 0, Arg.Any<Vec3f>());
    }

    [TestMethod]
    public void Tick_EnemyNearTheFormation_OrdersACharge()
    {
        var f = Started();
        f.Sample(1, MatchSide.West, 0, new Vec3f(500f, -45f, 0f));
        f.Sample(2, MatchSide.East, 0, new Vec3f(510f, -45f, 0f));

        f.Director.Tick(1f);

        f.Battlefield.Received(1).Charge(MatchSide.West, 0);
        f.Battlefield.Received(1).Charge(MatchSide.East, 0);
    }

    [TestMethod]
    public void Tick_FormationBesideAnEnemyTower_OrdersTheAttackOnIt()
    {
        var f = Started();
        var tower = f.TowerSlot(EastOuterLane0);
        f.Battlefield.AttackTower(MatchSide.West, 0, EastOuterLane0).Returns(true);
        f.Sample(1, MatchSide.West, 0, tower.Position + new Vec3f(-5f, 0f, 0f));

        f.Director.Tick(1f);

        f.Battlefield.Received(1).AttackTower(MatchSide.West, 0, EastOuterLane0);
        f.Battlefield.DidNotReceive().Move(MatchSide.West, 0, Arg.Any<Vec3f>());
    }

    [TestMethod]
    public void Tick_TowerHasNoEntity_FallsBackToWalkingToIt()
    {
        var f = Started();
        var tower = f.TowerSlot(EastOuterLane0);
        f.Battlefield.AttackTower(MatchSide.West, 0, EastOuterLane0).Returns(false);
        f.Sample(1, MatchSide.West, 0, tower.Position + new Vec3f(-5f, 0f, 0f));

        f.Director.Tick(1f);

        f.Battlefield.Received(1).Move(MatchSide.West, 0, tower.Position);
    }

    private static IReadOnlyList<Vec3f> LanePath(FordsDirectorFixture f, int lane)
    {
        var layout = (MapLayout)f.Scene.ReceivedCalls().First(c => c.GetMethodInfo().Name == nameof(IFordsSceneAdapter.Fit)).GetArguments()[0]!;
        return layout.Lanes.Single(l => l.Index == lane).Path;
    }

    private static List<Vec3f> MoveTargets(FordsDirectorFixture f, MatchSide side, int lane) =>
        f.Battlefield.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IFordsBattlefieldAdapter.Move)
                && (MatchSide)c.GetArguments()[0]! == side && (int)c.GetArguments()[1]! == lane)
            .Select(c => (Vec3f)c.GetArguments()[2]!)
            .ToList();

    [TestMethod]
    public void Tick_AMoveTheAdapterSkips_IsReplacedByAMoveToTheNearestWaypointOfTheLane()
    {
        var f = Started();
        var path = LanePath(f, 0);
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false, true);
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);

        var moves = MoveTargets(f, MatchSide.West, 0);
        Assert.AreEqual(2, moves.Count, "the skipped target, then the fallback");
        Assert.AreEqual(path.OrderBy(p => p.Distance2D(moves[0])).First(), moves[1]);
    }

    [TestMethod]
    public void Tick_TheFallbackMoveSucceeded_IsRecordedSoItIsNotSentAgain()
    {
        var f = Started();
        var path = LanePath(f, 0);
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false, true);
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);
        f.Director.Tick(1f);

        Assert.AreEqual(2, MoveTargets(f, MatchSide.West, 0).Count);
    }

    [TestMethod]
    public void Tick_AMoveThatFailsEvenAtTheWaypoint_IsTriedAgainOnTheNextTick()
    {
        var f = Started();
        f.Battlefield.Move(MatchSide.West, 0, Arg.Any<Vec3f>()).Returns(false);
        f.Sample(1, MatchSide.West, 0, new Vec3f(50f, -45f, 0f));

        f.Director.Tick(1f);
        f.Director.Tick(1f);

        Assert.AreEqual(4, MoveTargets(f, MatchSide.West, 0).Count, "target and fallback, on each of the two ticks");
    }

    [TestMethod]
    public void Tick_TowerHasNoEntityAndItsSlotIsOffTheNavmesh_WalksToTheNearestLaneWaypoint()
    {
        var f = Started();
        var tower = f.TowerSlot(EastOuterLane0);
        var path = LanePath(f, 0);
        f.Battlefield.AttackTower(MatchSide.West, 0, EastOuterLane0).Returns(false);
        f.Battlefield.Move(MatchSide.West, 0, tower.Position).Returns(false);
        f.Sample(1, MatchSide.West, 0, tower.Position + new Vec3f(-5f, 0f, 0f));

        f.Director.Tick(1f);

        var moves = MoveTargets(f, MatchSide.West, 0);
        CollectionAssert.AreEqual(new[] { tower.Position, path.OrderBy(p => p.Distance2D(tower.Position)).First() }, moves);
    }

    [TestMethod]
    public void Tick_EnemyInRangeOfATower_FiresAVolleyAtItsPosition()
    {
        var f = Started();
        var tower = f.TowerSlot("tower_west_1_0");
        var enemyAt = tower.Position + new Vec3f(12f, 0f, 0f);
        f.Sample(77, MatchSide.East, 1, enemyAt);
        f.Battlefield.PositionOf(new AgentKey(77)).Returns(enemyAt);

        f.Director.Tick(1f);

        f.Towers.Received(1).Fire("tower_west_1_0", enemyAt, 3);
    }

    [TestMethod]
    public void Tick_VolleyTargetAlreadyGone_FiresNothing()
    {
        var f = Started();
        var tower = f.TowerSlot("tower_west_1_0");
        f.Sample(77, MatchSide.East, 1, tower.Position + new Vec3f(12f, 0f, 0f));
        f.Battlefield.PositionOf(new AgentKey(77)).Returns((Vec3f?)null);

        f.Director.Tick(1f);

        f.Towers.DidNotReceiveWithAnyArgs().Fire(default!, default, default);
    }

    [TestMethod]
    public void Tick_RegeneratedHitPoints_AreMirroredOntoTheTowerEntity()
    {
        var f = Started();
        f.ReportsTowerId(EastOuterLane0);
        f.Director.AdjustTowerDamage(new object(), null, false, 100);

        f.Director.Tick(10f);
        f.Director.Tick(10f);
        f.Director.Tick(10f);

        f.Towers.Received().Heal(EastOuterLane0, Arg.Is<int>(a => a > 0));
    }

    // --- kills, towers, the end ----------------------------------------------------------------------------------------

    private static (AgentKey West, AgentKey East) FirstKeys(FordsDirectorFixture f)
    {
        var west = f.Spawned.Select((s, i) => (s, key: new AgentKey(i + 1))).First(x => x.s.Side == MatchSide.West).key;
        var east = f.Spawned.Select((s, i) => (s, key: new AgentKey(i + 1))).First(x => x.s.Side == MatchSide.East).key;
        return (west, east);
    }

    [TestMethod]
    public void OnAgentRemoved_KillOfARegisteredEnemy_PaysTheKillersOwnerTheBounty()
    {
        var f = Started(WithFreeWaves);
        f.Director.Tick(1f);
        var (west, east) = FirstKeys(f);
        f.ReportsRemoval(east, west);
        var bounty = new BountyRules(f.Plan.Config.Economy).KillBounty(10);

        f.Director.OnAgentRemoved(new object(), new object(), true);

        Assert.AreEqual(600 + bounty, f.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void OnAgentRemoved_UnitNotRegistered_ChangesNothing()
    {
        var f = Started(WithFreeWaves);
        f.Director.Tick(1f);
        f.Battlefield.TryRemoveUnit(Arg.Any<object>(), Arg.Any<object?>(), Arg.Any<bool>(), out Arg.Any<AgentKey>(), out Arg.Any<AgentKey?>())
            .Returns(false);

        f.Director.OnAgentRemoved(new object(), new object(), true);

        Assert.AreEqual(600, f.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void OnAgentRemoved_OffTheMainThread_IsParkedUntilTheNextTick()
    {
        var f = Started(WithFreeWaves);
        f.Director.Tick(1f);
        var (west, east) = FirstKeys(f);
        f.ReportsRemoval(east, west);
        var bounty = new BountyRules(f.Plan.Config.Economy).KillBounty(10);

        var worker = new Thread(() => f.Director.OnAgentRemoved(new object(), new object(), true));
        worker.Start();
        worker.Join();
        Assert.AreEqual(600, f.Session.GetView()!.Gold);

        f.Director.Tick(0.1f);

        Assert.AreEqual(600 + bounty, f.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void TowerDestroyed_UnlocksTheNextTowerRemovesTheGarrisonAndPaysTheBounty()
    {
        var f = Started();
        f.ReportsTowerId(EastInnerLane0);
        Assert.AreEqual(0, f.Director.AdjustTowerDamage(new object(), null, false, 100), "the inner tower is locked behind the outer one");

        f.DestroyedHandler!(EastOuterLane0);
        f.Director.Tick(1f);

        Assert.AreEqual(100, f.Director.AdjustTowerDamage(new object(), null, false, 100));
        f.Towers.Received(1).KillGunners(EastOuterLane0);
        Assert.AreEqual(900, f.Session.GetView()!.Gold);
        var fell = f.Session.DrainEvents().Single(e => e.Kind == MatchEventKind.TowerDestroyed);
        Assert.AreEqual(EastOuterLane0, fell.Subject);
    }

    [TestMethod]
    public void TowerDestroyed_OnTheMainThread_WaitsForTheNextTickBeforeTouchingTheEngine()
    {
        var f = Started();
        f.ReportsTowerId(EastInnerLane0);

        f.DestroyedHandler!(EastOuterLane0);

        f.Towers.DidNotReceive().KillGunners(Arg.Any<string>());
        Assert.AreEqual(0, f.Director.AdjustTowerDamage(new object(), null, false, 100), "the inner tower unlocks on the tick, not in the hit callback");
        f.Director.Tick(1f);
        f.Towers.Received(1).KillGunners(EastOuterLane0);
    }

    [TestMethod]
    public void TowerDestroyed_ReportedTwice_PaysOnce()
    {
        var f = Started();

        f.DestroyedHandler!(EastOuterLane0);
        f.DestroyedHandler!(EastOuterLane0);
        f.Director.Tick(1f);

        Assert.AreEqual(900, f.Session.GetView()!.Gold);
    }

    [TestMethod]
    public void TowerDestroyed_ATowerAlreadyFallen_NoLongerFiresOnTheNextTick()
    {
        var f = Started();
        var tower = f.TowerSlot(EastOuterLane0);
        f.DestroyedHandler!(EastOuterLane0);
        var enemyAt = tower.Position + new Vec3f(-10f, 0f, 0f);
        f.Sample(5, MatchSide.West, 0, enemyAt);
        f.Battlefield.PositionOf(new AgentKey(5)).Returns(enemyAt);

        f.Director.Tick(1f);

        f.Towers.DidNotReceive().Fire(EastOuterLane0, Arg.Any<Vec3f>(), Arg.Any<int>());
    }

    [TestMethod]
    public void AdjustTowerDamage_RangedHit_UsesTheRangedFactor()
    {
        var f = Started();
        f.ReportsTowerId(EastOuterLane0);

        Assert.AreEqual(15, f.Director.AdjustTowerDamage(new object(), null, true, 100));
    }

    [TestMethod]
    public void AdjustTowerDamage_HeroAttacker_UsesTheHeroFactor()
    {
        var f = Started(RichPlayers);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixture.HeroA.HeroId));
        f.Director.Tick(1f);
        var heroKey = new AgentKey(1);
        var attacker = new object();
        f.Battlefield.TryGetKey(attacker, out Arg.Any<AgentKey>()).Returns(call =>
        {
            call[1] = heroKey;
            return true;
        });
        f.ReportsTowerId(EastOuterLane0);

        Assert.AreEqual(400, f.Director.AdjustTowerDamage(new object(), attacker, false, 100));
    }

    [TestMethod]
    public void AdjustTowerDamage_RegisteredAttackerOnTheTowersOwnSide_IsZero()
    {
        var f = Started(RichPlayers);
        Assert.AreEqual(CommandResult.Ok, f.Session.TryBuyHero(0, FordsMatchFixture.HeroA.HeroId));
        f.Director.Tick(1f);
        var attacker = new object();
        f.Battlefield.TryGetKey(attacker, out Arg.Any<AgentKey>()).Returns(call =>
        {
            call[1] = new AgentKey(1);
            return true;
        });
        f.ReportsTowerId(WestOuterLane0);

        Assert.AreEqual(0, f.Director.AdjustTowerDamage(new object(), attacker, false, 100));
        f.Battlefield.DidNotReceive().SideOf(Arg.Any<object?>());
    }

    [TestMethod]
    public void AdjustTowerDamage_UnregisteredAttackerOnTheTowersOwnTeam_IsZero()
    {
        var f = Started();
        var gunner = new object();
        f.Battlefield.SideOf(gunner).Returns(MatchSide.West);
        f.ReportsTowerId(WestOuterLane0);

        Assert.AreEqual(0, f.Director.AdjustTowerDamage(new object(), gunner, true, 100));
    }

    [TestMethod]
    public void AdjustTowerDamage_UnregisteredAttackerOnTheEnemyTeam_UsesTheTable()
    {
        var f = Started();
        var gunner = new object();
        f.Battlefield.SideOf(gunner).Returns(MatchSide.East);
        f.ReportsTowerId(WestOuterLane0);

        Assert.AreEqual(15, f.Director.AdjustTowerDamage(new object(), gunner, true, 100));
    }

    [TestMethod]
    public void AdjustTowerDamage_AttackerWithNoTeam_UsesTheTable()
    {
        var f = Started();
        f.Battlefield.SideOf(Arg.Any<object?>()).Returns((MatchSide?)null);
        f.ReportsTowerId(WestOuterLane0);

        Assert.AreEqual(15, f.Director.AdjustTowerDamage(new object(), new object(), true, 100));
    }

    [TestMethod]
    public void AdjustTowerDamage_NotAFordsTower_ReturnsTheDamageUnchanged()
    {
        var f = Started();
        f.ReportsTowerId(null);

        Assert.AreEqual(77, f.Director.AdjustTowerDamage(new object(), null, true, 77));
    }

    [TestMethod]
    public void AdjustTowerDamage_BeforeStart_ReturnsTheDamageUnchanged()
    {
        var f = FordsDirectorFixture.Create();
        f.ReportsTowerId(EastOuterLane0);

        Assert.AreEqual(55, f.Director.AdjustTowerDamage(new object(), null, false, 55));
    }

    [TestMethod]
    public void KeepDestroyed_EndsTheMatchForTheOtherSideOnTheNextTick()
    {
        var f = Started();
        Assert.IsNull(f.Director.Winner);

        foreach (var id in new[] { EastOuterLane0, EastInnerLane0, "keep_east" })
            f.DestroyedHandler!(id);
        f.Director.Tick(1f);

        Assert.AreEqual(MatchSide.West, f.Director.Winner);
        Assert.IsTrue(f.Session.DrainEvents().Any(e => e.Kind == MatchEventKind.KeepDestroyed && e.Side == MatchSide.East));
    }

    [TestMethod]
    public void Tick_AfterTheWinnerIsKnown_StopsRunningTheMatch()
    {
        var f = Started(WithFreeWaves);
        foreach (var id in new[] { EastOuterLane0, EastInnerLane0, "keep_east" })
            f.DestroyedHandler!(id);
        f.Director.Tick(1f);
        f.Spawned.Clear();

        f.Director.Tick(60f);

        Assert.AreEqual(0, f.Spawned.Count);
    }

    [TestMethod]
    public void Shutdown_ClearsTheSessionAndTheAdapters()
    {
        var f = Started();

        f.Director.Shutdown();
        f.Director.Shutdown();

        Assert.IsFalse(f.Session.IsActive);
        f.Towers.Received(1).Clear();
        f.Battlefield.Received(1).Clear();
        f.Battlefield.ClearReceivedCalls();
        f.Director.Tick(1f);
        f.Battlefield.DidNotReceiveWithAnyArgs().SampleUnits();
    }
}
