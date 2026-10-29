using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Features.StagedSiege.Hooks;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The console never writes engine AI state itself: it enqueues, and the mission behavior drains the queue on the main thread at
/// the start of the mission tick. Drained over a substituted adapter and a real service.
/// </summary>
[TestClass]
public class StagedSiegeCommandQueueTests
{
    private const string Scene = "sturgia_castle_siege_001";

    private IStagedSiegeMissionAdapter _m = null!;
    private IModLogger _log = null!;
    private StagedSiegeCommandQueue _queue = null!;
    private object _token = null!;
    private float _now;
    private IReadOnlyList<StageZone> _zones = null!;

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        _now = 0f;
        _token = new object();
        _log = Substitute.For<IModLogger>();
        _queue = new StagedSiegeCommandQueue();
        _zones = new[] { new StageZone(StageId.A, new StagePoint(1f, 2f, 3f), 8f) };

        _m = Substitute.For<IStagedSiegeMissionAdapter>();
        _m.MissionToken.Returns(_token);
        _m.SceneName.Returns(Scene);
        _m.Time.Returns(_ => _now);
        _m.IsSiegeBattle.Returns(true);
        _m.IsCampaignWallsAssault.Returns(true);
        _m.PlayerSide.Returns((StagedSide?)StagedSide.Attacker);
        _m.ReadSpawnCounts(Arg.Any<StagedSide>()).Returns(new SpawnCounts(50, 50));
        _m.CountStanding(Arg.Any<StagedSide>()).Returns(50);
        _m.SpawnFlags(Arg.Any<IReadOnlyList<StageZone>>()).Returns(4);
        _m.OpenGates().Returns(2);
        _m.ActiveDefenderInfantry().Returns(0);
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
    }

    private StagedSiegeService ArmedAndSetUp()
    {
        var catalog = StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, Substitute.For<IModLogger>());
        var service = new StagedSiegeService(_m, catalog, _log);
        Assert.IsTrue(service.TryArm(true));
        _m.IsDeploymentFinished.Returns(true);
        _now = 1f;
        service.Tick();
        return service;
    }

    private StageId Stage => StagedSiegeSlot.SnapshotFor(_token)!.Stage;

    [TestMethod]
    public void Drain_Empty_AppliesNothing()
    {
        Assert.IsTrue(_queue.IsEmpty);

        Assert.AreEqual(0, _queue.Drain(null, _m, _log));

        _m.DidNotReceive().OpenGates();
    }

    [TestMethod]
    public void Drain_RunsCommandsInTheOrderTheyWereQueued()
    {
        _queue.Enqueue(StagedSiegeCommand.OpenGates());
        _queue.Enqueue(StagedSiegeCommand.ShowFlags(_zones));
        _queue.Enqueue(StagedSiegeCommand.OpenGates());

        Assert.IsFalse(_queue.IsEmpty);
        Assert.AreEqual(3, _queue.Drain(null, _m, _log));

        Received.InOrder(() =>
        {
            _m.OpenGates();
            _m.SpawnFlags(_zones);
            _m.OpenGates();
        });
        Assert.IsTrue(_queue.IsEmpty);
    }

    [TestMethod]
    public void Drain_ServiceNotArmed_StillOpensGatesAndShowsFlags()
    {
        // Recording an unrecorded scene needs both, and no service is armed there.
        _queue.Enqueue(StagedSiegeCommand.OpenGates());
        _queue.Enqueue(StagedSiegeCommand.ShowFlags(_zones));

        _queue.Drain(null, _m, _log);

        _m.Received(1).OpenGates();
        _m.Received(1).SpawnFlags(_zones);
    }

    [TestMethod]
    public void Drain_ForceStageWithoutAService_LogsNotArmed()
    {
        _queue.Enqueue(StagedSiegeCommand.ForceStage(StageId.C));

        _queue.Drain(null, _m, _log);

        _log.Received().LogInfo(Arg.Is<string>(s => s.Contains("not armed")));
    }

    [TestMethod]
    public void Drain_ForceStage_AppliesOnlyWhenDrained_NotWhenQueued()
    {
        var service = ArmedAndSetUp();

        _queue.Enqueue(StagedSiegeCommand.ForceStage(StageId.C));

        Assert.AreEqual(StageId.A, Stage, "queued is not applied");
        _m.DidNotReceive().OpenGates();

        _queue.Drain(service, _m, _log);

        Assert.AreEqual(StageId.C, Stage);
        _m.Received(1).OpenGates();
        _log.Received().LogInfo(Arg.Is<string>(s => s.Contains("Forced the siege from stage A to stage C")));
    }

    [TestMethod]
    public void Drain_ShowFlags_ArmedService_DoesNotSpawnASecondSet()
    {
        var service = ArmedAndSetUp();
        _m.ClearReceivedCalls();

        _queue.Enqueue(StagedSiegeCommand.ShowFlags(_zones));
        _queue.Drain(service, _m, _log);

        _m.DidNotReceive().SpawnFlags(Arg.Any<IReadOnlyList<StageZone>>());
    }

    [TestMethod]
    public void Drain_ACommandThatThrows_IsLogged_AndTheRestStillRun()
    {
        _m.OpenGates().Throws(new InvalidOperationException("boom"));
        _queue.Enqueue(StagedSiegeCommand.OpenGates());
        _queue.Enqueue(StagedSiegeCommand.ShowFlags(_zones));

        _queue.Drain(null, _m, _log);

        _log.Received().LogWarning(Arg.Is<string>(s => s.Contains("boom")));
        _m.Received(1).SpawnFlags(_zones);
    }

    [TestMethod]
    public void Enqueue_FromManyThreads_KeepsEveryCommand()
    {
        Parallel.For(0, 200, _ => _queue.Enqueue(StagedSiegeCommand.OpenGates()));

        Assert.AreEqual(200, _queue.Drain(null, _m, _log));
        _m.Received(200).OpenGates();
    }

    [TestMethod]
    public void Drain_ACommandQueuedWhileDraining_WaitsForTheNextDrain()
    {
        _m.OpenGates().Returns(_ =>
        {
            _queue.Enqueue(StagedSiegeCommand.ShowFlags(_zones));
            return 1;
        });
        _queue.Enqueue(StagedSiegeCommand.OpenGates());

        Assert.AreEqual(1, _queue.Drain(null, _m, _log));
        _m.DidNotReceive().SpawnFlags(Arg.Any<IReadOnlyList<StageZone>>());
        Assert.AreEqual(1, _queue.Drain(null, _m, _log));
        _m.Received(1).SpawnFlags(_zones);
        Assert.IsTrue(_queue.IsEmpty);
    }
}
