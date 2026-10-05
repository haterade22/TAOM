using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Features.CreatureSiegeRole.Domain;
using TAOM.Tests.Features.SiegeForces;
using static TAOM.Tests.Features.CreatureSiegeRole.CreatureSiegeFixtures;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// The per-mission service: activation, the reconcile pass, the face exclusion, the routing of each creature and the fail-safe.
/// Both adapters are NSubstitute substitutes (SiegeSceneFixture); every engine effect the service relies on is modelled by the
/// substitute, and every native call it makes is a recorded call. The default scene is the TAOM shape: an outer gate at
/// (100, 200) facing +Y, an inner gate 20 m behind it, attackers outside at y = 250 and defenders inside at y = 190.
/// </summary>
[TestClass]
public class CreatureSiegeRoleServiceTests
{
    private SiegeSceneFixture _scene = null!;

    [TestInitialize]
    public void Setup()
    {
        CreatureSiegeSnapshot.Clear();
        _scene = new SiegeSceneFixture();
    }

    [TestCleanup]
    public void Cleanup() => _scene.Dispose();

    private static void AssertAt(SiegePoint actual, float x, float y, float z = 10f)
    {
        Assert.AreEqual(x, actual.X, 1e-3f, $"x of {actual}");
        Assert.AreEqual(y, actual.Y, 1e-3f, $"y of {actual}");
        Assert.AreEqual(z, actual.Z, 1e-3f, $"z of {actual}");
    }

    private const int Cap = CreatureSiegeRules.MaxExcludedFaceGroups;

    // The default scene's two entrances and four ladders fill the cap; its shared bridge (601) is offered last and dropped.
    private static readonly string[] TaomSceneExclusion = { "exclude 1000052", "exclude 1000102", "exclude 333", "exclude 444", "exclude 555", "exclude 666" };

    // --- activation -------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void TryActivate_ASiegeBattle_IsActive_AndPublishesTheSnapshot()
    {
        Assert.IsTrue(_scene.Activate());

        Assert.IsTrue(_scene.Service.IsActive);
        var snapshot = CreatureSiegeSnapshot.Current!;
        Assert.AreSame(_scene.Token, snapshot.MissionToken);
        Assert.IsTrue(snapshot.IsCreatureRace(FakeRaceManager.CaveTroll));
        Assert.IsTrue(snapshot.IsCreatureRace(FakeRaceManager.HillTroll));
        Assert.IsFalse(snapshot.IsCreatureRace(FakeRaceManager.Human), "a human is no creature");
        Assert.IsFalse(snapshot.IsCreatureRace(FakeRaceManager.Warg));
        Assert.AreEqual(2f, snapshot.GateDamageMultiplier);
        Assert.IsTrue(snapshot.IsGateComponent(_scene.OuterGate!.DestructionComponent));
        Assert.IsTrue(snapshot.IsGateComponent(_scene.InnerGate!.DestructionComponent));
        Assert.IsFalse(snapshot.IsGateComponent(new object()));
    }

    [TestMethod]
    public void TryActivate_TheRaceMask_IsBuiltFromTheSharedOversizedRaces_ValidatingBeforeLookup()
    {
        _scene.Activate();

        // Names are looked up only after the race table says they exist; no id is ever asked for its name.
        CollectionAssert.AreEquivalent(new[] { "cave_troll", "hill_troll" }, _scene.Races.NamesAskedForAnId);
        Assert.AreEqual(0, _scene.Races.NameLookups);
    }

    [TestMethod]
    public void TryActivate_NotASiege_IsInert_LogsAtDebug_AndReadsNoGate()
    {
        _scene.IsSiege = false;

        Assert.IsFalse(_scene.Activate());

        Assert.IsFalse(_scene.Service.IsActive);
        Assert.IsNull(CreatureSiegeSnapshot.Current);
        StringAssert.Contains(_scene.Debugs.Single(), "not a siege battle");
        _scene.Mission.DidNotReceive().OuterGateCandidates();
    }

    [TestMethod]
    public void TryActivate_ASallyOutOrReliefForce_IsInert_AndSaysSo()
    {
        _scene.HasSallyOut = true;

        Assert.IsFalse(_scene.Activate());

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        StringAssert.Contains(_scene.Infos.Single(), "sally-out or relief");
        _scene.Mission.DidNotReceive().OuterGateCandidates();
    }

    [TestMethod]
    public void TryActivate_ANetworkClient_IsInert_AndSaysSo()
    {
        _scene.IsClient = true;

        Assert.IsFalse(_scene.Activate());

        StringAssert.Contains(_scene.Infos.Single(), "does not run the AI");
    }

    [TestMethod]
    public void TryActivate_TheSettingOff_IsInert_AndSaysSo()
    {
        Assert.IsFalse(_scene.Activate(roleEnabled: false));

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        StringAssert.Contains(_scene.Infos.Single(), "setting is off");
    }

    [TestMethod]
    public void TryActivate_NoCreatureRaceRegistered_IsInert_AndSaysSo()
    {
        using var scene = new SiegeSceneFixture(FakeRaceManager.HumansOnly());

        Assert.IsFalse(scene.Activate());

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        StringAssert.Contains(scene.Infos.Single(), "no creature race");
    }

    [TestMethod]
    public void TryActivate_NoOuterGate_IsInert_WithOneWarning()
    {
        _scene.OuterGate = null;

        Assert.IsFalse(_scene.Activate());

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        StringAssert.Contains(_scene.Warnings.Single(), "no usable outer gate");
        StringAssert.Contains(_scene.Warnings.Single(), "taom_test_scene");
    }

    [TestMethod]
    public void TryActivate_EveryOuterGateIsHiddenOrDisabled_IsInert_WithOneWarning()
    {
        _scene.OuterCandidates = List(Gate(visible: false), Gate(disabled: true));

        Assert.IsFalse(_scene.Activate());

        Assert.AreEqual(1, _scene.Warnings.Count);
    }

    [TestMethod]
    public void TryActivate_AHiddenInnerGateStub_IsSteppedOver_ForTheNextVisibleGateOfTheSameTag()
    {
        var stubComponent = new object();
        var realComponent = new object();
        _scene.InnerCandidates = List(Gate(name: "stub", visible: false, destruction: stubComponent), Gate(name: "real", y: 180f, destruction: realComponent));

        Assert.IsTrue(_scene.Activate());

        var snapshot = CreatureSiegeSnapshot.Current!;
        Assert.IsTrue(snapshot.IsGateComponent(realComponent));
        Assert.IsFalse(snapshot.IsGateComponent(stubComponent), "the hidden stub is not a gate to break");
    }

    [TestMethod]
    public void TryActivate_NoInnerGateAtAll_StaysActive_WithTheOuterGateOnly()
    {
        _scene.InnerGate = null;

        Assert.IsTrue(_scene.Activate());

        Assert.IsTrue(CreatureSiegeSnapshot.Current!.IsGateComponent(_scene.OuterGate!.DestructionComponent));
    }

    [TestMethod]
    public void TryActivate_AGateWithNoDestructionComponent_IsLeftOutOfTheSnapshot()
    {
        _scene.OuterGate = new SiegeGateReading(new object(), "plain", true, false, 100f, 200f, 10f, 0f, 1f, true, 100f, 198f, 10f, null);

        Assert.IsTrue(_scene.Activate());

        var snapshot = CreatureSiegeSnapshot.Current!;
        Assert.IsTrue(snapshot.IsGateComponent(_scene.InnerGate!.DestructionComponent));
        Assert.IsFalse(snapshot.IsGateComponent(null), "a gate with no component contributes none");
    }

    [TestMethod]
    public void TryActivate_AnInertMission_LeavesAnotherMissionsSnapshotAlone()
    {
        var foreign = new CreatureSiegeSnapshot(new object(), new[] { false, true }, 2f, Array.Empty<object>());
        CreatureSiegeSnapshot.Publish(foreign);
        _scene.IsSiege = false;

        _scene.Activate();

        Assert.AreSame(foreign, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Shutdown_ClearsItsOwnSnapshot_AndStopsTheTick()
    {
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Service.Shutdown();
        _scene.Pass();

        Assert.IsNull(CreatureSiegeSnapshot.Current);
        Assert.IsFalse(_scene.Service.IsActive);
        Assert.AreEqual(0, creature.Calls.Count);
    }

    [TestMethod]
    public void Shutdown_LeavesAnotherMissionsSnapshotAlone()
    {
        _scene.Activate();
        var foreign = new CreatureSiegeSnapshot(new object(), new[] { true }, 2f, Array.Empty<object>());
        CreatureSiegeSnapshot.Publish(foreign);

        _scene.Service.Shutdown();

        Assert.AreSame(foreign, CreatureSiegeSnapshot.Current);
    }

    [TestMethod]
    public void Shutdown_OfAnInertService_IsHarmless_AndTwiceIsToo()
    {
        _scene.IsSiege = false;
        _scene.Activate();

        _scene.Service.Shutdown();
        _scene.Service.Shutdown();

        Assert.IsNull(CreatureSiegeSnapshot.Current);
    }

    // --- when the pass runs -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_AServiceThatNeverActivated_TouchesNothing()
    {
        var creature = _scene.Add();

        _scene.Pass();

        Assert.AreEqual(0, creature.Calls.Count);
        _scene.Mission.DidNotReceive().CollectCreatures(Arg.Any<Func<int, bool>>());
    }

    [TestMethod]
    public void Tick_AnInertService_TouchesNothing()
    {
        _scene.IsSiege = false;
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        Assert.AreEqual(0, creature.Calls.Count);
        _scene.Mission.DidNotReceive().CollectCreatures(Arg.Any<Func<int, bool>>());
    }

    [TestMethod]
    public void Tick_BeforeDeploymentFinishes_IsNotArmed_AndTouchesNoCreatureAndReadsNoLadder()
    {
        // SetScriptedPosition teleports an agent while deployment runs (Agent.cs:2454): nothing may be scripted before it ends.
        _scene.Activate();
        _scene.DeploymentFinished = false;
        var creature = _scene.Add();

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, creature.Calls.Count);
        _scene.Mission.DidNotReceive().CollectCreatures(Arg.Any<Func<int, bool>>());
        _scene.Mission.DidNotReceive().ReadLadderWallIds();
        _scene.Mission.DidNotReceive().ReadTowers();

        _scene.DeploymentFinished = true;
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
    }

    [TestMethod]
    public void Tick_RunsOncePerStride_NotOncePerFrame()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass(0.5f);
        _scene.Service.Tick();
        _scene.Pass(0.25f);

        _scene.Mission.Received(1).CollectCreatures(Arg.Any<Func<int, bool>>());

        _scene.Pass(0.25f);

        _scene.Mission.Received(2).CollectCreatures(Arg.Any<Func<int, bool>>());
    }

    [TestMethod]
    public void Tick_ANaNClock_NeverRunsAPass_UntilTheClockIsRealAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Now = float.NaN;
        _scene.Service.Tick();
        Assert.AreEqual(0, creature.Calls.Count);

        _scene.Now = 100f;
        _scene.Service.Tick();
        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
    }

    [TestMethod]
    public void Tick_AsksTheAdapterForCreaturesWithTheSnapshotsRaceTest()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();

        Assert.IsNotNull(_scene.CollectedWith);
        Assert.IsTrue(_scene.CollectedWith!(FakeRaceManager.CaveTroll));
        Assert.IsTrue(_scene.CollectedWith(FakeRaceManager.HillTroll));
        Assert.IsFalse(_scene.CollectedWith(FakeRaceManager.Human));
        Assert.IsFalse(_scene.CollectedWith(-1));
    }

    // --- the one activation line ------------------------------------------------------------------------------------------

    [TestMethod]
    public void TheFirstArmedPass_LogsOneInfoLine_NamingTheSceneTheGatesTheIdsAndTheAnchors()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();
        _scene.Pass();
        _scene.Pass();

        var line = _scene.Infos.Single(l => l.Contains("active on"));
        StringAssert.Contains(line, "taom_test_scene");
        StringAssert.Contains(line, "outer_gate_a");
        StringAssert.Contains(line, "inner_gate_b");
        StringAssert.Contains(line, "exclusion [1000052, 1000102, 333, 444, 555, 666]");
        StringAssert.Contains(line, "dropped [601]");
        StringAssert.Contains(line, "hold anchor OuterMiddle");
        StringAssert.Contains(line, "courtyard anchor InnerMiddle");
    }

    [TestMethod]
    public void TheActivationLine_IsLoggedEvenWhenNoCreatureHasSpawned()
    {
        _scene.Activate();

        _scene.Pass();

        Assert.AreEqual(1, _scene.Infos.Count(l => l.Contains("active on")));
    }

    [TestMethod]
    public void TheAnchorsInTheActivationLine_SkipACandidateWhoseGroundIsAWallTop()
    {
        _scene.OuterGate = Gate(name: "outer_gate_a", middleZ: 24f, destruction: new object(), handle: new object());
        _scene.Activate();

        _scene.Pass();

        var line = _scene.Infos.Single(l => l.Contains("active on"));
        StringAssert.Contains(line, "hold anchor InnerMiddle", "the outer middle sits on a wall top (24 vs a base of 10)");
    }

    // --- the face exclusion -----------------------------------------------------------------------------------------------

    [TestMethod]
    public void TheFirstPass_ExcludesEveryAICreatureOnce_InTheFixedOrder()
    {
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add(SiegeSide.Defender);

        _scene.Pass();

        CollectionAssert.AreEqual(TaomSceneExclusion, first.Calls.Take(Cap).ToArray());
        CollectionAssert.AreEqual(TaomSceneExclusion, second.Calls.Take(Cap).ToArray());
    }

    [TestMethod]
    public void LaterPasses_NeverExcludeACreatureAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();
        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(Cap, creature.Calls.Count(c => c.StartsWith("exclude", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TheIdsAreReadOnceAtTheFirstArmedPass_AndNeverAgain()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();
        _scene.Pass();
        _scene.Pass();

        _scene.Mission.Received(1).ReadLadderWallIds();
        _scene.Mission.Received(1).ReadTowers();
    }

    [TestMethod]
    public void AReinforcement_IsExcludedWithinTheNextPass_AndTheOthersAreNotExcludedAgain()
    {
        _scene.Activate();
        var early = _scene.Add();
        _scene.Pass();

        var late = _scene.Add();
        _scene.Pass();

        CollectionAssert.AreEqual(TaomSceneExclusion, late.Calls.Take(Cap).ToArray());
        Assert.AreEqual(Cap, early.Calls.Count(c => c.StartsWith("exclude", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ANonAIControlledCreature_IsNeverExcludedOrRouted_UntilTheAIHasIt()
    {
        _scene.Activate();
        var troll = _scene.Add();
        troll.IsAIControlled = false;

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, troll.Calls.Count, "a player's troll or a co-op puppet is nobody's to script");

        troll.IsAIControlled = true;
        _scene.Pass();

        CollectionAssert.AreEqual(TaomSceneExclusion, troll.Calls.Take(Cap).ToArray());
        CollectionAssert.AreEqual(new[] { "strike" }, troll.Moves);
    }

    [TestMethod]
    public void ASceneWithNoLaddersOrTowers_ExcludesNothing_ButStillRoutes()
    {
        _scene.LadderIds = new();
        _scene.Towers = new();
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        Assert.AreEqual(0, creature.Excluded.Count);
        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
    }

    [TestMethod]
    public void ASkippedFaceId_IsWarnedOnce_Each()
    {
        _scene.Towers = new() { new TowerFaces(0, 0), new TowerFaces(1000100, 601) };
        _scene.Activate();
        _scene.Add();

        _scene.Pass();
        _scene.Pass();

        var skipped = _scene.Warnings.Where(w => w.Contains("skipped navmesh face id")).ToList();
        Assert.AreEqual(2, skipped.Count, "the tower's entrance (start 0) and its bridge (0): one warning each, never repeated");
        Assert.IsTrue(skipped.Any(w => w.Contains("TowerEntrance")));
        Assert.IsTrue(skipped.Any(w => w.Contains("TowerBridge")));
    }

    [TestMethod]
    public void ATruncatedList_DroppingALadder_IsWarnedOnce_NamingTheDroppedIds()
    {
        _scene.LadderIds = new() { 333, 444, 555, 666, 777 };
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();
        _scene.Pass();

        var warnings = _scene.Warnings.Where(w => w.Contains("exclusion list cut")).ToList();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "777");
        Assert.AreEqual(Cap, creature.Excluded.Count, "the cap is never exceeded");
    }

    [TestMethod]
    public void TheDefaultTwoTowerFourLadderScene_LogsNoTruncationWarning_ThoughItsBridgeIsDropped()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();

        Assert.AreEqual(0, _scene.Warnings.Count(w => w.Contains("exclusion list cut")));
        Assert.IsFalse(_scene.Warnings.Any(), "the default scene is quiet");
    }

    [TestMethod]
    public void DroppedTowerBridges_AreNotWarned()
    {
        _scene.Towers = new() { new TowerFaces(1000050, 1000053), new TowerFaces(1000100, 1000103) };
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        Assert.AreEqual(0, _scene.Warnings.Count(w => w.Contains("exclusion list cut")), "bridges go last and go quietly");
        CollectionAssert.DoesNotContain(creature.Excluded, 1000053);
        CollectionAssert.DoesNotContain(creature.Excluded, 1000103);
    }

    [TestMethod]
    public void AnExcludedCreatureSeenInALadderQueue_IsWarnedOnce_PerMission()
    {
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add();
        first.InLadderQueue = true;
        second.InLadderQueue = true;

        _scene.Pass();
        _scene.Pass();

        var warnings = _scene.Warnings.Where(w => w.Contains("ladder queue")).ToList();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "excluded");
    }

    // --- attackers --------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void AnAttackerAtAShutOuterGate_StrikesTheOuterGateFromTheFirstSlot()
    {
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
        AssertAt(creature.StrikePoints.Single(), 100f, 202.2f);
        Assert.AreSame(_scene.OuterGate!.Handle, creature.StrikeGates.Single());
    }

    [TestMethod]
    public void AttackersShareTheStrikeSlotsInOrder_OnePlaceEach()
    {
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add();
        var third = _scene.Add();

        _scene.Pass();

        AssertAt(first.StrikePoints.Single(), 100f, 202.2f);
        AssertAt(second.StrikePoints.Single(), 102.8f, 202.2f);
        AssertAt(third.StrikePoints.Single(), 97.2f, 202.2f);
    }

    [TestMethod]
    public void TheSlotsWrapAround_AfterTwelveCreatures()
    {
        _scene.Activate();
        var creatures = Enumerable.Range(0, 13).Select(_ => _scene.Add()).ToList();

        _scene.Pass();

        AssertAt(creatures[12].StrikePoints.Single(), 100f, 202.2f);
        Assert.AreEqual(12, creatures.Take(12).Select(c => (Math.Round(c.StrikePoints.Single().X, 2), Math.Round(c.StrikePoints.Single().Y, 2))).Distinct().Count());
    }

    [TestMethod]
    public void AnAttackerBehindTheInnerGate_StrikesItFromItsOwnSide()
    {
        // The vanilla side rule: the creature's own side of the gate's forward axis, so it never walks through the gate.
        _scene.Activate();
        var creature = _scene.Add(at: new SiegePoint(100f, 170f, 10f));
        creature.NavigationFaceId = 11;

        _scene.Pass();

        Assert.AreSame(_scene.InnerGate!.Handle, creature.StrikeGates.Single());
        AssertAt(creature.StrikePoints.Single(), 100f, 177.8f);
    }

    [TestMethod]
    public void AnAttackerWhosePreferredSlotHasNoPath_IsNotSentThroughTheGate()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = p => !(Math.Abs(p.X - 100f) < 0.01f && Math.Abs(p.Y - 202.2f) < 0.01f);

        _scene.Pass();

        Assert.AreEqual(0, creature.StrikePoints.Count, "the centre slot is the preferred one: nothing else on its own side is left");
        Assert.AreEqual(1, creature.PathQueries.Count);
    }

    [TestMethod]
    public void AnAttackerWhoseOwnSideHasNoPath_NeverGetsAFarSideSlot_EvenIfTheFarSideAnswersYes()
    {
        // The path query only compares navmesh islands, and islands merge when ladders go up: a "yes" for the far side of a
        // shut gate says nothing about the gate being open.
        _scene.Activate();
        _scene.Add();
        var second = _scene.Add();
        second.PathRule = p => p.Y < 200f;

        _scene.Pass();

        Assert.AreEqual(0, second.StrikePoints.Count);
        Assert.IsTrue(second.PathQueries.All(p => p.Y > 200f), "every slot asked about is on the creature's own side");
    }

    [TestMethod]
    public void AnAttackerWhoseSideSlotsAreAllBlocked_CollapsesToTheCentre()
    {
        _scene.Activate();
        _scene.Add();
        var second = _scene.Add();
        second.PathRule = p => Math.Abs(p.X - 100f) < 0.01f;

        _scene.Pass();

        AssertAt(second.StrikePoints.Single(), 100f, 202.2f);
        Assert.AreEqual(2, second.PathQueries.Count, "its own slot, then the centre of its own side");
    }

    [TestMethod]
    public void AnAttackerWithNoReachableSlot_IsNotRouted_AndOneWarningNamesTheScene()
    {
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add();
        first.PathRule = _ => false;
        second.PathRule = _ => false;

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, first.Moves.Count, "never routed, so there is nothing to release");
        Assert.AreEqual(0, second.Moves.Count);
        var warnings = _scene.Warnings.Where(w => w.Contains("no reachable")).ToList();
        Assert.AreEqual(1, warnings.Count, "one per mission, not per creature or per pass");
        StringAssert.Contains(warnings[0], "taom_test_scene");
    }

    [TestMethod]
    public void AnUnplaceableCreature_IsNotPathQueriedAgainOnTheNextPass()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = _ => false;

        _scene.Pass();
        var afterFirst = creature.PathQueries.Count;
        _scene.Pass();

        Assert.IsTrue(afterFirst > 0, "the first pass tried");
        Assert.AreEqual(afterFirst, creature.PathQueries.Count, "the same role failed a pass ago: no new path queries");
    }

    [TestMethod]
    public void AnUnplaceableCreature_RetriesTheSameRoleEveryFourthPass()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = _ => false;

        _scene.Pass();
        var afterFirst = creature.PathQueries.Count;
        for (var i = 0; i < 3; i++) _scene.Pass();
        Assert.AreEqual(afterFirst, creature.PathQueries.Count, "passes 2 to 4 are skipped");

        creature.PathRule = _ => true;
        _scene.Pass();

        Assert.IsTrue(creature.PathQueries.Count > afterFirst, "pass 5 tries again");
        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves, "and places the creature once a slot is reachable");
    }

    [TestMethod]
    public void AnUnplaceableCreature_TriesAgainAtOnceWhenItsRoleDecisionChanges()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = _ => false;
        _scene.Pass();
        var afterFirst = creature.PathQueries.Count;

        // A ram starts working: the decision is now StandOff, not StrikeOuter.
        _scene.Ram = new RamReading(true, false, 3, true);
        _scene.Pass();

        Assert.IsTrue(creature.PathQueries.Count > afterFirst, "a different role is a fresh attempt");
    }

    [TestMethod]
    public void AnUnplaceableCreature_AfterARoleChangeAndBack_IsTriedAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = _ => false;
        _scene.Pass();

        creature.IsFleeing = true;
        _scene.Pass();
        creature.IsFleeing = false;
        var before = creature.PathQueries.Count;
        _scene.Pass();

        Assert.IsTrue(creature.PathQueries.Count > before, "the decision changed in between, so the memory of the failure is gone");
    }

    [TestMethod]
    public void AnUnplaceableCreature_DoesNotHoldBackItsNeighbours()
    {
        _scene.Activate();
        var blocked = _scene.Add();
        var free = _scene.Add();
        blocked.PathRule = _ => false;

        _scene.Pass();
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, free.Moves);
        Assert.AreEqual(0, blocked.Moves.Count);
    }

    [TestMethod]
    public void ARoutedCreatureWhoseSlotBecomesUnreachable_IsReleased_NotLeftScripted()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);

        // The gate falls, the next role needs a new slot, and none can be reached.
        _scene.OuterState = new GateLiveState(true, false, 0f);
        creature.PathRule = _ => false;
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, creature.Moves);
    }

    [TestMethod]
    public void TheRoleChain_OuterGate_ThenInnerGate_ThenTheCourtyard()
    {
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();
        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.Pass();
        _scene.InnerState = new GateLiveState(true, false, 0f);
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "strike", "clearTarget", "hold" }, creature.Moves);
        Assert.AreSame(_scene.OuterGate!.Handle, creature.StrikeGates[0]);
        Assert.AreSame(_scene.InnerGate!.Handle, creature.StrikeGates[1], "the target moves to the inner gate");
        AssertAt(creature.StrikePoints[1], 100f, 182.2f);
        AssertAt(creature.HoldPoints.Single(), 100f, 178f);
    }

    [TestMethod]
    public void LeavingAStrike_ForAHold_ClearsTheCombatTargetFirst()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        _scene.Ram = new RamReading(true, false, 3, true);

        _scene.Pass();

        // The ram starts working: the creature steps back out of its way. The attack target does not follow it there.
        CollectionAssert.AreEqual(new[] { "strike", "clearTarget", "hold" }, creature.Moves);
        Assert.IsFalse(creature.IsAttackingEntity);
    }

    [TestMethod]
    public void RetargetingFromOneGateToTheNext_IsNotAnExit_SoItClearsNothing()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        _scene.OuterState = new GateLiveState(true, false, 0f);

        _scene.Pass();

        Assert.AreEqual(0, creature.Count("clearTarget"));
    }

    [TestMethod]
    public void WhileARamWorks_AnAttackerStandsOffTenMetresOut_AndStrikesOnceItStops()
    {
        _scene.Ram = new RamReading(true, false, 3, true);
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "hold" }, creature.Moves);
        AssertAt(creature.HoldPoints.Single(), 104f, 210f);

        // The ram stops being used: still working for twenty seconds, then the creature strikes.
        _scene.Ram = new RamReading(true, false, 0, false);
        _scene.Pass(10f);
        CollectionAssert.AreEqual(new[] { "hold" }, creature.Moves);
        _scene.Pass(15f);

        CollectionAssert.AreEqual(new[] { "hold", "strike" }, creature.Moves);
    }

    [TestMethod]
    public void ADeactivatedRam_DoesNotHoldACreatureBack()
    {
        _scene.Ram = new RamReading(true, true, 4, true);
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
    }

    [TestMethod]
    public void AGateOpenForTwoSeconds_IsPassable_AndAFlappingLeverIsNeverOpenLongEnough()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        _scene.OuterState = new GateLiveState(false, true, 15000f);
        _scene.Pass(0.5f);
        _scene.Pass(0.5f);
        _scene.Pass(0.5f);
        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves, "open for 1.0 s: not yet");

        _scene.Pass(0.5f);
        _scene.Pass(0.5f);

        CollectionAssert.AreEqual(new[] { "strike", "strike" }, creature.Moves, "open for 2.0 s: on to the inner gate");
        Assert.AreSame(_scene.InnerGate!.Handle, creature.StrikeGates[1]);
    }

    [TestMethod]
    public void AGateThatIsReclosed_SendsACreatureThatWentThroughBackToItsInnerGate_NotBackOut()
    {
        // Defenders open the gate for two seconds, the creature goes through, the gate shuts behind it.
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        _scene.OuterState = new GateLiveState(false, true, 15000f);
        for (var i = 0; i < 6; i++) _scene.Pass(0.5f);
        CollectionAssert.AreEqual(new[] { "strike", "strike" }, creature.Moves);

        _scene.OuterState = new GateLiveState(false, false, 15000f);
        creature.Position = new SiegePoint(100f, 192f, 10f);
        _scene.Pass();

        Assert.AreEqual(2, creature.Count("strike"), "it stays at the inner gate and does not turn round to the outer one");
    }

    [TestMethod]
    public void ALeverThatFlapsEveryPass_NeverMovesAStrikingCreature()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        for (var i = 0; i < 40; i++)
        {
            _scene.OuterState = new GateLiveState(false, i % 2 == 0, 15000f);
            _scene.Pass();
        }

        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves, "no flicker: one strike, nothing else");
    }

    [TestMethod]
    public void ACreatureThroughAShutOuterGate_FacedByAnInnerGate_StrikesTheInnerGate()
    {
        _scene.Activate();
        var creature = _scene.Add(at: new SiegePoint(100f, 193f, 10f));

        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike" }, creature.Moves);
        Assert.AreSame(_scene.InnerGate!.Handle, creature.StrikeGates.Single());
    }

    [TestMethod]
    public void ACreatureOnAnInsideNavmeshFace_CountsAsThroughTheGate()
    {
        _scene.Activate();
        var creature = _scene.Add(at: new SiegePoint(140f, 260f, 10f));
        creature.NavigationFaceId = 11;

        _scene.Pass();

        Assert.AreSame(_scene.InnerGate!.Handle, creature.StrikeGates.Single());
    }

    [TestMethod]
    public void AfterTheBreach_AnAttackerHoldsTheCourtyardAnchor_AndIsNeverChased()
    {
        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.InnerState = new GateLiveState(true, false, 0f);
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "hold" }, creature.Moves);
        AssertAt(creature.HoldPoints.Single(), 100f, 178f);
    }

    [TestMethod]
    public void ACourtyardHold_SkipsAnAnchorThatHasNoPath_ForTheNextCandidate()
    {
        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.InnerState = new GateLiveState(true, false, 0f);
        _scene.Activate();
        var creature = _scene.Add();
        creature.PathRule = p => Math.Abs(p.Y - 178f) > 0.01f;

        _scene.Pass();

        AssertAt(creature.HoldPoints.Single(), 100f, 198f);
    }

    // --- defenders --------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void ADefender_HoldsTheOuterGatesMiddleOnTheGround()
    {
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);

        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "hold" }, defender.Moves);
        AssertAt(defender.HoldPoints.Single(), 100f, 198f);
    }

    [TestMethod]
    public void Defenders_SpreadAcrossTheGatesAxis_AroundTheAnchor()
    {
        _scene.Activate();
        var first = _scene.Add(SiegeSide.Defender);
        var second = _scene.Add(SiegeSide.Defender);
        var third = _scene.Add(SiegeSide.Defender);
        var fourth = _scene.Add(SiegeSide.Defender);

        _scene.Pass();

        AssertAt(first.HoldPoints.Single(), 100f, 198f);
        AssertAt(second.HoldPoints.Single(), 102.8f, 198f);
        AssertAt(third.HoldPoints.Single(), 97.2f, 198f);
        AssertAt(fourth.HoldPoints.Single(), 100f, 195.2f);
    }

    [TestMethod]
    public void ADefendersSpreadSlotWithNoPath_FallsBackToTheAnchorItself()
    {
        _scene.Activate();
        _scene.Add(SiegeSide.Defender);
        var second = _scene.Add(SiegeSide.Defender);
        second.PathRule = p => Math.Abs(p.X - 102.8f) > 0.01f;

        _scene.Pass();

        AssertAt(second.HoldPoints.Single(), 100f, 198f);
    }

    [TestMethod]
    public void ADefender_FallsBackFromAnAnchorWithNoPathToTheInnerGatesMiddle()
    {
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);
        defender.PathRule = p => Math.Abs(p.Y - 198f) > 0.01f;

        _scene.Pass();

        AssertAt(defender.HoldPoints.Single(), 100f, 178f);
    }

    [TestMethod]
    public void ADefender_NeverHoldsAnAnchorOnAWallTop()
    {
        // The outer gate's middle position sits over a gatehouse (24 against a base of 10); the inner gate's is on the ground.
        _scene.OuterGate = Gate(name: "outer_gate_a", middleZ: 24f, destruction: new object(), handle: new object());
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);

        _scene.Pass();

        AssertAt(defender.HoldPoints.Single(), 100f, 178f);
        Assert.IsFalse(defender.PathQueries.Any(p => Math.Abs(p.Y - 198f) < 0.01f), "a wall-top anchor is not even path-tested");
    }

    [TestMethod]
    public void ADefenderWithNoValidAnchor_IsNotRouted_AndOneWarningNamesTheScene()
    {
        _scene.Activate();
        var first = _scene.Add(SiegeSide.Defender);
        var second = _scene.Add(SiegeSide.Defender);
        first.PathRule = _ => false;
        second.PathRule = _ => false;

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, first.Moves.Count);
        Assert.AreEqual(0, second.Moves.Count);
        var warnings = _scene.Warnings.Where(w => w.Contains("no valid anchor")).ToList();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.Contains(warnings[0], "taom_test_scene");
    }

    [TestMethod]
    public void ADefenderKeepsHoldingTheGate_AfterTheBreach()
    {
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);
        _scene.Pass();
        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.InnerState = new GateLiveState(true, false, 0f);

        _scene.Pass();
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "hold" }, defender.Moves);
    }

    // --- who is never scripted --------------------------------------------------------------------------------------------

    [TestMethod]
    public void ACreatureInAFormationThePlayerCommands_IsLeftAloneUnlessItChargesBeforeTheBreach()
    {
        _scene.Activate();
        var obeying = _scene.Add();
        obeying.Formation = PlayerFormation(SiegeFormationOrder.Other);
        var charging = _scene.Add();
        charging.Formation = PlayerFormation(SiegeFormationOrder.Charge);

        _scene.Pass();

        Assert.AreEqual(0, obeying.Moves.Count, "a Move or Follow order is obeyed");
        CollectionAssert.AreEqual(new[] { "strike" }, charging.Moves);
    }

    [TestMethod]
    public void AChargingFormation_IsReleasedOnceBothGatesAreOpen_AndKeepsItsExclusion()
    {
        _scene.Activate();
        var charging = _scene.Add();
        charging.Formation = PlayerFormation(SiegeFormationOrder.ChargeToTarget);
        _scene.Pass();

        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.InnerState = new GateLiveState(true, false, 0f);
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, charging.Moves);
        Assert.AreEqual(Cap, charging.Excluded.Count, "the exclusion is never removed");
    }

    [TestMethod]
    public void ADefenderInAFormationThePlayerCommands_IsLeftAlone()
    {
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);
        defender.Formation = PlayerFormation(SiegeFormationOrder.Other);

        _scene.Pass();

        Assert.AreEqual(0, defender.Moves.Count);
    }

    [TestMethod]
    public void ACreatureWithNoTeam_IsNeverRouted()
    {
        _scene.Activate();
        var stray = _scene.Add(SiegeSide.None);

        _scene.Pass();

        Assert.AreEqual(0, stray.Moves.Count);
    }

    // --- release ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void AFleeingCreature_IsReleasedExactlyOnce_AndRoutedAgainWhenItStopsFleeing()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.IsFleeing = true;
        _scene.Pass();
        _scene.Pass();
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, creature.Moves);

        creature.IsFleeing = false;
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release", "strike" }, creature.Moves);
    }

    [TestMethod]
    public void ACreatureNeverRouted_IsNeverReleased()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.IsFleeing = true;

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, creature.Count("release"));
    }

    [TestMethod]
    public void ARetreatOrder_ReleasesARoutedCreature()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.Formation = AIFormation(SiegeFormationOrder.Retreat);
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, creature.Moves);
    }

    [TestMethod]
    public void ACreatureThatLosesAIControl_IsReleasedOnce_AndKeepsItsExclusion()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.IsAIControlled = false;
        _scene.Pass();
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, creature.Moves);
        Assert.AreEqual(Cap, creature.Excluded.Count);
    }

    // --- engine-cleared flags ---------------------------------------------------------------------------------------------

    [TestMethod]
    public void AStrikingCreatureWhoseFlagsAreIntact_IsNeverReapplied()
    {
        _scene.Activate();
        var creature = _scene.Add();

        for (var i = 0; i < 6; i++) _scene.Pass();

        Assert.AreEqual(1, creature.Count("strike"));
    }

    [TestMethod]
    public void WhenTheEngineClearsTheScriptedPosition_TheStrikeIsAppliedAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.HasScriptedPosition = false;
        _scene.Pass();

        Assert.AreEqual(2, creature.Count("strike"));
        AssertAt(creature.StrikePoints[1], creature.StrikePoints[0].X, creature.StrikePoints[0].Y);
    }

    [TestMethod]
    public void WhenTheEngineClearsTheAttackTarget_TheStrikeIsAppliedAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.IsAttackingEntity = false;
        _scene.Pass();

        Assert.AreEqual(2, creature.Count("strike"));
    }

    [TestMethod]
    public void WhenTheEngineClearsAHoldersPosition_TheHoldIsAppliedAgain()
    {
        _scene.Activate();
        var defender = _scene.Add(SiegeSide.Defender);
        _scene.Pass();

        defender.HasScriptedPosition = false;
        _scene.Pass();

        Assert.AreEqual(2, defender.Count("hold"));
        AssertAt(defender.HoldPoints[1], defender.HoldPoints[0].X, defender.HoldPoints[0].Y);
    }

    [TestMethod]
    public void AReapplication_KeepsTheSlotThePathWasValidatedFor_WithoutAskingForAPathAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        var queries = creature.PathQueries.Count;

        creature.HasScriptedPosition = false;
        _scene.Pass();

        Assert.AreEqual(queries, creature.PathQueries.Count);
    }

    // --- the sweep --------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void ACreatureThatLeavesTheList_IsForgottenWithoutAnyCall()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        var calls = creature.Adapter.ReceivedCalls().Count();

        _scene.Creatures.Clear();
        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(calls, creature.Adapter.ReceivedCalls().Count(), "a sweep makes no native call and reads nothing");
    }

    [TestMethod]
    public void AForgottenCreatureThatReturns_IsANewTenant_ExcludedAndRoutedAgain()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();
        _scene.Creatures.Clear();
        _scene.Pass();

        _scene.Creatures.Add(creature);
        _scene.Pass();

        Assert.AreEqual(2 * Cap, creature.Excluded.Count);
        Assert.AreEqual(2, creature.Count("strike"));
    }

    [TestMethod]
    public void TwoCreaturesWithEqualHashCodes_AreTwoKeys_NeverOne()
    {
        // Route records are keyed by reference, never by Equals.
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add();
        first.Adapter.Identity.Returns(new AlwaysEqualKey());
        second.Adapter.Identity.Returns(new AlwaysEqualKey());

        _scene.Pass();

        Assert.AreEqual(1, first.Count("strike"));
        Assert.AreEqual(1, second.Count("strike"));
    }

    private sealed class AlwaysEqualKey
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => 7;
    }

    // --- the fail-safe ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void AFaultInAPass_ReleasesEveryRoutedCreature_EachInItsOwnTry_StopsRoutingAndLogsOneError()
    {
        _scene.Activate();
        var first = _scene.Add();
        var second = _scene.Add();
        _scene.Pass();

        var third = _scene.Add();
        third.StrikeThrows = true;
        first.ReleaseThrows = true;
        _scene.Pass();

        Assert.AreEqual(1, first.Count("release"), "attempted, and the throw did not stop the others");
        Assert.AreEqual(1, second.Count("release"));
        Assert.AreEqual(1, third.Count("release"), "a creature whose strike threw part-way is released too");
        Assert.AreEqual(1, _scene.Errors.Count);
        StringAssert.Contains(_scene.Errors[0], "reconcile");

        var calls = first.Calls.Count + second.Calls.Count + third.Calls.Count;
        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(calls, first.Calls.Count + second.Calls.Count + third.Calls.Count, "routing stopped");
        Assert.AreEqual(1, _scene.Errors.Count, "one ERROR, never repeated");
    }

    [TestMethod]
    public void AFaultInAPass_KeepsTheSnapshot_SoTheCostAndDamageHooksKeepWorking()
    {
        _scene.Activate();
        var creature = _scene.Add();
        creature.StrikeThrows = true;

        _scene.Pass();

        Assert.IsNotNull(CreatureSiegeSnapshot.Current);
        Assert.AreEqual(1, _scene.Errors.Count);
    }

    [TestMethod]
    public void AFaultBeforeTheCreaturesAreCollected_StillReleasesWhatEarlierPassesRouted()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        _scene.Mission.ReadGate(Arg.Any<object>()).Throws(new InvalidOperationException("the gate read failed"));
        _scene.Pass();

        CollectionAssert.AreEqual(new[] { "strike", "release" }, creature.Moves);
        Assert.AreEqual(1, _scene.Errors.Count);
    }

    [TestMethod]
    public void AFaultWhileArming_StopsTheService_WithOneError()
    {
        _scene.Mission.ReadLadderWallIds().Throws(new InvalidOperationException("no ladders"));
        _scene.Activate();
        var creature = _scene.Add();

        _scene.Pass();
        _scene.Pass();

        Assert.AreEqual(0, creature.Moves.Count);
        Assert.AreEqual(1, _scene.Errors.Count);
    }

    // --- the log ----------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void ARoleChange_LogsOneDebugLine_NamingTheRoleTheReasonTheSlotAndTheQueueState()
    {
        _scene.Activate();
        _scene.Add();

        _scene.Pass();

        var line = _scene.Debugs.Single(l => l.Contains("StrikeOuter"));
        StringAssert.Contains(line, "OuterGateClosed");
        StringAssert.Contains(line, "slot 0");
        StringAssert.Contains(line, "100.0, 202.2, 10.0");
        StringAssert.Contains(line, "inLadderQueue False");
    }

    [TestMethod]
    public void RoleLines_AreThrottled_AndTheNextOneReportsHowManyWereSwallowed()
    {
        _scene.Activate();
        for (var i = 0; i < 30; i++) _scene.Add();

        _scene.Pass();

        Assert.AreEqual(8, _scene.Debugs.Count(l => l.Contains("(OuterGateClosed)")), "a burst of eight, then nothing");

        _scene.OuterState = new GateLiveState(true, false, 0f);
        _scene.Pass(30f);

        Assert.IsTrue(_scene.Debugs.Any(l => l.Contains("(InnerGateClosed)") && l.Contains("22 suppressed")), "30 - 8 swallowed");
    }

    [TestMethod]
    public void AReapplication_LogsADebugLineNamingTheClearedFlag()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.HasScriptedPosition = false;
        _scene.Pass();

        var line = _scene.Debugs.Single(l => l.Contains("re-applied"));
        StringAssert.Contains(line, "position");
    }

    [TestMethod]
    public void AReleaseLine_NamesItsReason()
    {
        _scene.Activate();
        var creature = _scene.Add();
        _scene.Pass();

        creature.IsFleeing = true;
        _scene.Pass();

        StringAssert.Contains(_scene.Debugs.Last(), "Fleeing");
    }

    [TestMethod]
    public void ARefusedRoleLine_IsNeverWritten_OnlyTheBurstThatPassesIs()
    {
        // Past the burst a refused line is never formatted or written: the logger sees the eight and nothing else.
        _scene.Activate();
        for (var i = 0; i < 40; i++) _scene.Add();

        _scene.Pass();

        Assert.AreEqual(8, _scene.Debugs.Count);
    }
}
