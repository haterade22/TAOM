using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The staged siege service over a substituted adapter and the pilot scene: arming, the first-tick setup, the capture
/// reconcile, the reserve gate behind the engine's timer, the end and the fault policy.
/// </summary>
[TestClass]
public class StagedSiegeServiceTests
{
    private const string Scene = "sturgia_castle_siege_001";

    private IStagedSiegeMissionAdapter _m = null!;
    private IModLogger _log = null!;
    private StagedSiegeSceneCatalog _catalog = null!;
    private ScenePlan _plan = null!;
    private StagedSiegeService _svc = null!;
    private object _token = null!;
    private float _now;
    private SpawnCounts _attackerCounts;
    private SpawnCounts _defenderCounts;
    private int _attackerStanding;
    private int _defenderStanding;

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        _now = 0f;
        _token = new object();
        _log = Substitute.For<IModLogger>();
        _catalog = StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, Substitute.For<IModLogger>());
        Assert.IsTrue(_catalog.TryGetScene(Scene, out _plan));

        _attackerCounts = new SpawnCounts(100, 200);
        _defenderCounts = new SpawnCounts(80, 100);
        _attackerStanding = 100;
        _defenderStanding = 80;
        _m = Substitute.For<IStagedSiegeMissionAdapter>();
        _m.MissionToken.Returns(_token);
        _m.SceneName.Returns(Scene);
        _m.Time.Returns(_ => _now);
        _m.IsSiegeBattle.Returns(true);
        _m.IsCampaignWallsAssault.Returns(true);
        _m.SiegeStateName.Returns("OnTheWalls");
        _m.PlayerSide.Returns((StagedSide?)StagedSide.Attacker);
        _m.SpawnSettingsAreStaged.Returns(true);
        _m.ReadSpawnCounts(StagedSide.Attacker).Returns(_ => _attackerCounts);
        _m.ReadSpawnCounts(StagedSide.Defender).Returns(_ => _defenderCounts);
        _m.CountStanding(StagedSide.Attacker).Returns(_ => _attackerStanding);
        _m.CountStanding(StagedSide.Defender).Returns(_ => _defenderStanding);
        _m.SpawnFlags(Arg.Any<IReadOnlyList<StageZone>>()).Returns(4);
        _m.OpenGates().Returns(2);
        _m.ActiveDefenderInfantry().Returns(40);
        _m.EnsureDefendBehaviors().Returns(3);
        _m.DetachmentNote.Returns("from 2 formation(s) into Skirmisher");
        _m.CreateDetachment(Arg.Any<int>(), Arg.Any<IReadOnlyList<StagePoint>>(), Arg.Any<StagePoint>(), Arg.Any<StageFacing>())
            .Returns(new DetachmentReading(10, true, 558.3f, 593.3f));
        _m.ReadDetachment().Returns(new DetachmentReading(10, true, 558.3f, 593.3f));
        _svc = new StagedSiegeService(_m, _catalog, _log);
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
    }

    // --- helpers ---------------------------------------------------------------------------------------------------

    private void Arm()
    {
        Assert.IsTrue(_svc.TryArm(true));
        _svc.ResetCadence(StagedSide.Attacker);
        _svc.ResetCadence(StagedSide.Defender);
    }

    private void At(float time)
    {
        _now = time;
        _svc.Tick();
    }

    private void ArmAndSetUp()
    {
        Arm();
        _m.IsDeploymentFinished.Returns(true);
        At(1f);
    }

    private static bool Near(StagePoint a, StagePoint b) => Math.Abs(a.X - b.X) < 0.01f && Math.Abs(a.Y - b.Y) < 0.01f;

    private void ZoneHas(StageId stage, int attackers, int defenders)
    {
        var center = _plan.Zone(stage).Center;
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 8f, 3f).Returns(new ZoneCount(attackers, defenders));
    }

    private StageId Stage => StagedSiegeSlot.SnapshotFor(_token)!.Stage;

    // Ticks in half-second passes until the current point is captured (or the siege ends).
    private void CaptureCurrent(bool clearBeforeEachPass = false)
    {
        var stage = Stage;
        ZoneHas(stage, 10, 0);
        for (var i = 0; i < 100; i++)
        {
            if (clearBeforeEachPass)
                _m.ClearReceivedCalls();
            At(_now + 0.5f);
            if (_svc.TryGetEnd(out _) || Stage != stage)
                return;
        }

        Assert.Fail($"stage {stage} was never captured");
    }

    private void AssertLogged(string fragment) =>
        _log.Received().LogInfo(Arg.Is<string>(s => s.StartsWith("[StagedSiege] ") && s.Contains(fragment)));

    private void AssertNotLogged(string fragment) =>
        _log.DidNotReceive().LogInfo(Arg.Is<string>(s => s.Contains(fragment)));

    // --- arming ----------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("NotASiege")]
    [DataRow("SallyOut")]
    [DataRow("Client")]
    [DataRow("NotWallsAssault")]
    [DataRow("Disabled")]
    [DataRow("NoPlayerSide")]
    [DataRow("UnrecordedScene")]
    public void TryArm_GateFails_LogsReasonPublishesNothing(string verdict)
    {
        var enabled = true;
        switch (verdict)
        {
            case "NotASiege": _m.IsSiegeBattle.Returns(false); break;
            case "SallyOut": _m.HasSallyOutController.Returns(true); break;
            case "Client": _m.IsClientOrReplay.Returns(true); break;
            case "NotWallsAssault": _m.IsCampaignWallsAssault.Returns(false); break;
            case "Disabled": enabled = false; break;
            case "NoPlayerSide": _m.PlayerSide.Returns((StagedSide?)null); break;
            case "UnrecordedScene": _m.SceneName.Returns("some_other_scene"); break;
        }

        Assert.IsFalse(_svc.TryArm(enabled));

        Assert.IsFalse(_svc.IsArmed);
        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
        AssertLogged("inert: " + verdict);
        AssertLogged("siege state OnTheWalls");
    }

    [TestMethod]
    public void TryArm_Inert_NamesThePreviousStagedSiege_OnceAndClearsIt()
    {
        StagedSiegeSlot.SetLastEnded("old_scene", StagedEnd.DefenderPushedBack, DateTime.UtcNow.AddSeconds(-30));
        _m.IsCampaignWallsAssault.Returns(false);

        Assert.IsFalse(_svc.TryArm(true));

        AssertLogged("previous staged siege old_scene ended DefenderPushedBack");
        Assert.IsNull(StagedSiegeSlot.LastEnded);
    }

    [TestMethod]
    public void TryArm_AllPass_ClearsAStaleWallsEnd()
    {
        // A record left by an earlier siege must not be reported again by the inert log of a later battle.
        StagedSiegeSlot.SetLastEnded("old_scene", StagedEnd.DefenderPushedBack, DateTime.UtcNow.AddSeconds(-600));

        Assert.IsTrue(_svc.TryArm(true));

        Assert.IsNull(StagedSiegeSlot.LastEnded);
    }

    [TestMethod]
    public void TryArm_AllPass_PublishesStageA()
    {
        Assert.IsTrue(_svc.TryArm(true));

        Assert.IsTrue(_svc.IsArmed);
        Assert.IsFalse(_svc.IsFaulted);
        Assert.AreSame(_svc, StagedSiegeSlot.ServiceFor(_token));
        var snapshot = StagedSiegeSlot.SnapshotFor(_token)!;
        Assert.AreEqual(StageId.A, snapshot.Stage);
        Assert.AreEqual(StagedSide.Attacker, snapshot.PlayerSide);
        AssertLogged($"armed: scene {Scene}, player Attacker");
    }

    [TestMethod]
    public void TryArm_ReadThrows_Faults_AndPublishesNothing()
    {
        _m.IsSiegeBattle.Throws(new InvalidOperationException("no mission"));

        Assert.IsFalse(_svc.TryArm(true));

        Assert.IsTrue(_svc.IsFaulted);
        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("fault in TryArm") && s.Contains("vanilla continues")));
    }

    // --- the first tick after deployment -----------------------------------------------------------------------------

    [TestMethod]
    public void Tick_BeforeDeployment_NoSetup()
    {
        Arm();

        At(1f);
        At(2f);

        _m.DidNotReceiveWithAnyArgs().SpawnFlags(null!);
        _m.DidNotReceiveWithAnyArgs().CreateDetachment(0, null!, default, default);
    }

    [TestMethod]
    public void Tick_DeploymentOver_SetupRunsOnce()
    {
        ArmAndSetUp();
        At(2f);
        At(3f);

        _m.Received(1).SpawnFlags(Arg.Is<IReadOnlyList<StageZone>>(z => z.Count == 4));
        foreach (var stage in new[] { StageId.A, StageId.B, StageId.C, StageId.D })
            _m.Received(1).ColourFlag(stage, StagedSide.Defender);
        _m.Received(1).CreateDetachment(10, Arg.Is<IReadOnlyList<StagePoint>>(s => s.Count == 10),
            Arg.Is<StagePoint>(p => Near(p, _plan.Zone(StageId.A).Center)), Arg.Any<StageFacing>());
        AssertLogged("S2 deployment over: settings live True; reserve attacker 200 defender 100; active attacker 100 defender 80");
        AssertLogged("S3 detachment: 10 units from 2 formation(s) into Skirmisher, driven True");
        AssertLogged("S4 BehaviorDefend added to 3 formation(s), stage A");
    }

    [TestMethod]
    public void Tick_DetachmentSizeZero_SkipsAndLogs()
    {
        _m.ActiveDefenderInfantry().Returns(3);

        ArmAndSetUp();

        _m.DidNotReceiveWithAnyArgs().CreateDetachment(0, null!, default, default);
        AssertLogged("S3 detachment: none");
    }

    [TestMethod]
    public void Tick_NotArmed_Nothing()
    {
        _m.IsDeploymentFinished.Returns(true);

        At(1f);

        _m.DidNotReceiveWithAnyArgs().SpawnFlags(null!);
        _ = _m.DidNotReceive().IsDeploymentFinished;
    }

    [TestMethod]
    public void Tick_NaNTime_Nothing()
    {
        Arm();
        _m.IsDeploymentFinished.Returns(true);

        At(float.NaN);

        _ = _m.DidNotReceive().IsDeploymentFinished;
        _m.DidNotReceiveWithAnyArgs().SpawnFlags(null!);
    }

    [TestMethod]
    public void Tick_WithinStride_NoRecount()
    {
        ArmAndSetUp();
        At(1.5f);
        _m.ReceivedWithAnyArgs(1).CountInZone(default, 0f, 0f);

        At(1.6f);
        At(1.9f);

        _m.ReceivedWithAnyArgs(1).CountInZone(default, 0f, 0f);
    }

    // --- capture, stages, gates ----------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_AttackersDominate_CapturesAMovesToB()
    {
        ArmAndSetUp();

        CaptureCurrent();

        Assert.AreEqual(StageId.B, Stage);
        _m.Received(1).ColourFlag(StageId.A, StagedSide.Attacker);
        _m.DidNotReceive().OpenGates();
        AssertLogged("stage A captured at");
        AssertLogged("next B");
    }

    [TestMethod]
    public void Tick_CaptureB_OpensGatesOnce()
    {
        ArmAndSetUp();
        CaptureCurrent();
        CaptureCurrent();
        CaptureCurrent();

        Assert.AreEqual(StageId.D, Stage);
        _m.Received(1).OpenGates();
        AssertLogged("S4 gates opened: 2");
    }

    [TestMethod]
    public void Tick_StageChange_EnsuresBehaviorsThenResetsTactics()
    {
        ArmAndSetUp();

        CaptureCurrent(clearBeforeEachPass: true);

        Received.InOrder(() =>
        {
            _m.EnsureDefendBehaviors();
            _m.ResetTactics();
        });
    }

    [TestMethod]
    public void Tick_StageChange_ColoursFlag()
    {
        ArmAndSetUp();

        CaptureCurrent();

        _m.Received(1).ColourFlag(StageId.A, StagedSide.Attacker);
    }

    [TestMethod]
    public void Tick_CaptureA_ReleasesDetachment()
    {
        ArmAndSetUp();
        _m.DidNotReceive().ReleaseDetachment();

        CaptureCurrent();

        _m.Received(1).ReleaseDetachment();
    }

    [TestMethod]
    public void Tick_AttackersNearA_OrdersCharge()
    {
        ArmAndSetUp();
        var center = _plan.Zone(StageId.A).Center;
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 16f, 10f).Returns(new ZoneCount(2, 0));
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 24f, 10f).Returns(new ZoneCount(2, 0));

        At(1.5f);
        At(2f);
        At(2.5f);
        At(3f);
        At(3.5f);

        _m.Received(1).OrderDetachment(DetachmentOrder.Charge, Arg.Any<StagePoint>(), Arg.Any<StageFacing>());
    }

    [TestMethod]
    public void Tick_DetachmentNotDriven_NoOrders()
    {
        _m.CreateDetachment(Arg.Any<int>(), Arg.Any<IReadOnlyList<StagePoint>>(), Arg.Any<StagePoint>(), Arg.Any<StageFacing>())
            .Returns(new DetachmentReading(10, false, 558.3f, 593.3f));
        _m.ReadDetachment().Returns(new DetachmentReading(10, false, 558.3f, 593.3f));
        ArmAndSetUp();
        var center = _plan.Zone(StageId.A).Center;
        _m.CountInZone(Arg.Is<StagePoint>(p => Near(p, center)), 16f, 10f).Returns(new ZoneCount(5, 0));

        for (var t = 1.5f; t < 8f; t += 0.5f)
            At(t);

        _m.DidNotReceiveWithAnyArgs().OrderDetachment(default, default, default);
    }

    [TestMethod]
    public void Tick_CaptureD_Attacker_EndPushedBack()
    {
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();

        Assert.IsTrue(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.DefenderPushedBack, end);
        Assert.IsTrue(StagedSiegeSlot.SnapshotFor(_token)!.Finished);
        AssertLogged("S5 D captured: ending with DefenderPushedBack");
    }

    [TestMethod]
    public void Tick_CaptureD_Defender_EndDefeated()
    {
        _m.PlayerSide.Returns((StagedSide?)StagedSide.Defender);
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();

        Assert.IsTrue(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.PlayerDefeated, end);
    }

    private void BecomeCustomBattle(StagedSide side)
    {
        _m.IsCampaignWallsAssault.Returns(false);
        _m.IsCustomBattleSiege.Returns(true);
        _m.PlayerSide.Returns((StagedSide?)side);
    }

    [TestMethod]
    public void TryArm_CustomBattleSiege_ArmsAndLogsTheKind()
    {
        BecomeCustomBattle(StagedSide.Attacker);

        Assert.IsTrue(_svc.TryArm(true));

        AssertLogged("armed: scene sturgia_castle_siege_001, player Attacker, custom battle");
    }

    [TestMethod]
    public void TryArm_CampaignWallsAssault_StillArmsWithoutTheCustomBattleNote()
    {
        Assert.IsTrue(_svc.TryArm(true));

        AssertNotLogged("custom battle");
    }

    [TestMethod]
    public void TryArm_NeitherKind_InertNamesTheKind()
    {
        _m.IsCampaignWallsAssault.Returns(false);
        _m.IsCustomBattleSiege.Returns(false);

        Assert.IsFalse(_svc.TryArm(true));

        AssertLogged("inert: NotWallsAssault");
        AssertLogged("mission kind other");
    }

    [TestMethod]
    public void TryArm_Inert_NamesTheCustomBattleKind()
    {
        BecomeCustomBattle(StagedSide.Attacker);
        _m.SceneName.Returns("some_other_scene");

        Assert.IsFalse(_svc.TryArm(true));

        AssertLogged("mission kind custom battle siege");
    }

    [TestMethod]
    public void Tick_CaptureD_CustomBattleAttacker_EndAttackerWon()
    {
        BecomeCustomBattle(StagedSide.Attacker);
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();

        Assert.IsTrue(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.AttackerWon, end);
        Assert.IsFalse(StagedSiegeSlot.SnapshotFor(_token)!.HallFollows);
    }

    [TestMethod]
    public void AnnounceEnd_AttackerWon_AnnouncesTheVictoryExactlyOnce()
    {
        BecomeCustomBattle(StagedSide.Attacker);
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();
        Assert.IsTrue(_svc.TryGetEnd(out var end));

        _svc.AnnounceEnd(end);
        _svc.AnnounceEnd(end);

        _m.Received(1).AnnounceVictory();
    }

    [TestMethod]
    public void AnnounceEnd_EveryOtherEnd_AnnouncesNothing()
    {
        ArmAndSetUp();

        _svc.AnnounceEnd(StagedEnd.DefenderPushedBack);
        _svc.AnnounceEnd(StagedEnd.PlayerDefeated);
        _svc.AnnounceEnd(StagedEnd.None);

        _m.DidNotReceive().AnnounceVictory();
    }

    [TestMethod]
    public void Tick_CaptureD_CustomBattleDefender_EndDefeated()
    {
        BecomeCustomBattle(StagedSide.Defender);
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();

        Assert.IsTrue(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.PlayerDefeated, end);
    }

    [TestMethod]
    public void Tick_CampaignAttacker_SnapshotSaysHallFollows()
    {
        Arm();

        Assert.IsTrue(StagedSiegeSlot.SnapshotFor(_token)!.HallFollows);
    }

    [TestMethod]
    public void TryGetEnd_NotFinished_False()
    {
        ArmAndSetUp();

        Assert.IsFalse(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.None, end);
    }

    // --- faults ------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Tick_AdapterThrows_FaultsOnceClearsSlotReleases()
    {
        ArmAndSetUp();
        _m.CountInZone(Arg.Any<StagePoint>(), Arg.Any<float>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));

        At(1.5f);
        At(2f);
        At(2.5f);

        Assert.IsTrue(_svc.IsFaulted);
        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        Assert.IsNull(StagedSiegeSlot.SnapshotFor(_token));
        _m.Received(1).ReleaseDetachment();
        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("fault in Tick") && s.Contains("boom")));
        Assert.IsFalse(_svc.TryGetEnd(out _));
    }

    // --- the reserve gate behind the engine's timer -------------------------------------------------------------------

    [TestMethod]
    public void ShouldReleaseBatch_BeforeSetup_False()
    {
        Arm();
        _now = 50f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
    }

    [TestMethod]
    public void ShouldReleaseBatch_BudgetLeftAndDue_True()
    {
        ArmAndSetUp();
        _now = 11f;

        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        AssertLogged("S2 reservation Attacker stage A: spent 0/40, remaining 200, active 100");
    }

    [TestMethod]
    public void ShouldReleaseBatch_NotYetDue_False()
    {
        ArmAndSetUp();
        _now = 9f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
    }

    [TestMethod]
    public void ShouldReleaseBatch_Fired_SchedulesTheNextAfterTheTrickle()
    {
        ArmAndSetUp();
        _now = 11f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _now = 12f;
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        _now = 21.5f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
    }

    [TestMethod]
    public void ShouldReleaseBatch_BudgetSpent_False()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(100, 160);
        _now = 11f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _log.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("S2 budget held Attacker stage A: spent 40 of 40")));
    }

    [TestMethod]
    public void ShouldReleaseBatch_Softlock_TrueLogsOnce()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(0, 50);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;

        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _log.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("S2 softlock release Attacker: standing 0, remaining 50")));
    }

    [TestMethod]
    public void ShouldReleaseBatch_AllActiveFleeing_ReserveLeft_TrueOverBudget()
    {
        // 60 agents are active but every one is running away; the stage budget (40 of 200) is spent, yet the side must refill
        // before vanilla's rout check (the interval plus 3 s of nobody standing) ends the battle.
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(60, 160);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;

        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        _log.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("S2 softlock release Attacker: standing 0, remaining 160")));
    }

    [TestMethod]
    public void ShouldReleaseBatch_AllActiveFleeing_NoReserve_False()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(60, 0);
        _attackerStanding = 0;
        At(1.5f);
        _now = 11f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
    }

    [TestMethod]
    public void ShouldReleaseBatch_StandingIsSampledByThePass_NotByEveryCheck()
    {
        ArmAndSetUp();
        _m.ClearReceivedCalls();
        _now = 11f;

        for (var i = 0; i < 5; i++)
        {
            _svc.ShouldReleaseBatch(StagedSide.Attacker);
            _svc.ShouldReleaseBatch(StagedSide.Defender);
        }

        _m.DidNotReceive().CountStanding(Arg.Any<StagedSide>());
        At(11.5f);
        _m.Received(1).CountStanding(StagedSide.Attacker);
        _m.Received(1).CountStanding(StagedSide.Defender);
    }

    [TestMethod]
    public void ShouldReleaseBatch_SoftlockEnds_WhenTheNextPassSeesSomeoneStanding()
    {
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(60, 160);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _attackerStanding = 5;
        At(2.5f);
        _now = 3f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker), "budget spent and not due: held again");
    }

    [TestMethod]
    public void ShouldReleaseBatch_Softlock_EndsOnTheFirstBuiltAgent_BeforeTheNextPass()
    {
        // Every approved check restarts the engine's reinforcement spawn slots, so a softlock that kept approving until the
        // next pass would stack that batch's troops on one slot. The first agent built for the side ends it.
        ArmAndSetUp();
        _attackerCounts = new SpawnCounts(60, 160);
        _attackerStanding = 0;
        At(1.5f);
        _now = 2f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));

        _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(1f, 2f, 3f));
        _now = 2.1f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker), "a built agent is standing: no softlock before the next pass");
    }

    [TestMethod]
    public void ShouldReleaseBatch_Finished_False()
    {
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();
        _now += 100f;

        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Defender));
    }

    [TestMethod]
    public void ShouldReleaseBatch_Faulted_TrickleOnly()
    {
        ArmAndSetUp();
        _m.CountInZone(Arg.Any<StagePoint>(), Arg.Any<float>(), Arg.Any<float>()).Throws(new InvalidOperationException("boom"));
        At(1.5f);
        Assert.IsTrue(_svc.IsFaulted);
        _attackerCounts = new SpawnCounts(100, 0);

        _now = 11f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker), "the budget no longer applies: plain trickle");
        _now = 12f;
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        _now = 21.5f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
    }

    [TestMethod]
    public void ResetCadence_DelaysByTrickle()
    {
        Arm();
        _m.IsDeploymentFinished.Returns(true);
        _now = 5f;
        _svc.ResetCadence(StagedSide.Defender);
        At(6f);

        _now = 14f;
        Assert.IsFalse(_svc.ShouldReleaseBatch(StagedSide.Defender));
        _now = 15f;
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Defender));
    }

    [TestMethod]
    public void ShouldReleaseBatch_AStageCaptureMakesBothSidesDueAtOnce()
    {
        ArmAndSetUp();
        CaptureCurrent();

        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Attacker));
        Assert.IsTrue(_svc.ShouldReleaseBatch(StagedSide.Defender));
    }

    // --- forcing a stage (the console) ---------------------------------------------------------------------------------

    [TestMethod]
    public void ForceStage_NotArmed_Refuses()
    {
        var reply = _svc.ForceStage(StageId.C);

        StringAssert.Contains(reply, "not armed");
    }

    [TestMethod]
    public void ForceStage_BeforeDeployment_Refuses()
    {
        Arm();

        StringAssert.Contains(_svc.ForceStage(StageId.C), "deployment");
        Assert.AreEqual(StageId.A, Stage);
    }

    [TestMethod]
    public void ForceStage_C_OpensGates()
    {
        ArmAndSetUp();

        var reply = _svc.ForceStage(StageId.C);

        Assert.AreEqual(StageId.C, Stage);
        _m.Received(1).OpenGates();
        _m.Received().ColourFlag(StageId.A, StagedSide.Attacker);
        _m.Received().ColourFlag(StageId.B, StagedSide.Attacker);
        StringAssert.Contains(reply, "C");
    }

    [TestMethod]
    public void ForceStage_Backward_IsRefused()
    {
        ArmAndSetUp();
        _svc.ForceStage(StageId.C);

        var reply = _svc.ForceStage(StageId.B);

        Assert.AreEqual(StageId.C, Stage);
        StringAssert.Contains(reply, "no change");
    }

    [TestMethod]
    public void ForceStage_End_Ends()
    {
        ArmAndSetUp();

        _svc.ForceStage(null);

        Assert.IsTrue(_svc.TryGetEnd(out var end));
        Assert.AreEqual(StagedEnd.DefenderPushedBack, end);
    }

    // --- reinforcement frames ------------------------------------------------------------------------------------------

    [TestMethod]
    public void NoteAgentBuilt_BeforeDeployment_Ignored()
    {
        Arm();

        _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(1f, 2f, 3f));

        AssertNotLogged("S2 reinforcement");
    }

    [TestMethod]
    public void NoteAgentBuilt_LogsFirstThreePerSidePerStage()
    {
        ArmAndSetUp();
        CaptureCurrent();

        for (var i = 0; i < 5; i++)
        {
            _svc.NoteAgentBuilt(StagedSide.Attacker, new StagePoint(1f, 2f, 3f));
            _svc.NoteAgentBuilt(StagedSide.Defender, new StagePoint(4f, 5f, 6f));
        }

        _log.Received(3).LogInfo(Arg.Is<string>(s => s.Contains("S2 reinforcement Attacker stage B at (1")));
        _log.Received(3).LogInfo(Arg.Is<string>(s => s.Contains("S2 reinforcement Defender stage B at (4")));
        _log.Received(6).LogInfo(Arg.Is<string>(s => s.Contains("m from the staged frame")));
    }

    // --- teardown ----------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Shutdown_ClearsByTokenReleases()
    {
        ArmAndSetUp();

        _svc.Shutdown();

        Assert.IsFalse(StagedSiegeSlot.HasArmed);
        _m.Received(1).ReleaseDetachment();
        Assert.IsNull(StagedSiegeSlot.LastEnded);
    }

    [TestMethod]
    public void Shutdown_AfterEnd_RecordsLastEnded()
    {
        ArmAndSetUp();
        for (var i = 0; i < 4; i++)
            CaptureCurrent();

        _svc.Shutdown();

        Assert.IsNotNull(StagedSiegeSlot.LastEnded);
        Assert.AreEqual(Scene, StagedSiegeSlot.LastEnded!.Value.Scene);
        Assert.AreEqual(StagedEnd.DefenderPushedBack, StagedSiegeSlot.LastEnded.Value.End);
    }

    [TestMethod]
    public void Shutdown_Twice_IsHarmless()
    {
        ArmAndSetUp();

        _svc.Shutdown();
        _svc.Shutdown();

        _m.Received(1).ReleaseDetachment();
    }

    // --- spawn settings ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void SpawnPlan_IsTheCatalogsBalancedPercentages()
    {
        Assert.AreEqual(0.05f, _svc.SpawnPlan.BatchPercentage, 1e-6f);
        Assert.AreEqual(0.166f, _svc.SpawnPlan.DesiredPercentage, 1e-6f);
        Assert.AreEqual(10f, _svc.SpawnPlan.TrickleSeconds, 1e-6f);
    }

    [TestMethod]
    public void NoteSpawnSettingsSwapped_LogsTheS2Line()
    {
        Arm();

        _svc.NoteSpawnSettingsSwapped("GlobalTimer");

        AssertLogged("S2 spawn settings: GlobalTimer -> Balanced/CustomTimer (batch 0.05, desired 0.166); timer installed");
    }

    [TestMethod]
    public void DescribeStatus_Armed_NamesStageAndProgress()
    {
        ArmAndSetUp();

        var text = _svc.DescribeStatus();

        StringAssert.Contains(text, "stage A");
        StringAssert.Contains(text, Scene);
    }

    [TestMethod]
    public void DescribeStatus_Inert_SaysWhy()
    {
        _m.IsCampaignWallsAssault.Returns(false);
        _svc.TryArm(true);

        StringAssert.Contains(_svc.DescribeStatus(), "NotWallsAssault");
    }
}
