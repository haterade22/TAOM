using System;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MountDespawn;
using TAOM.Features.MountDespawn.Hooks;

namespace TAOM.Tests.Features.MountDespawn;

/// <summary>
/// A player's log caught <c>OnAgentRemoved</c> off the main thread (#634), so a killed mount can be
/// recorded while the main thread sweeps <c>_pending</c> and the service's death times. The record is
/// parked and replayed from the next mission tick; a delete that races it must still leave no handle
/// behind, because a deleted agent's index is handed to the next agent built (#592).
/// </summary>
[TestClass]
public class MountDespawnOffThreadTests
{
    private IDeadMountDespawnService _service = null!;
    private MountDespawnMissionBehavior _sut = null!;

    // Agent.Index is a plain auto-property, so a bare instance is enough for the record path, which
    // stores the handle and never dereferences it.
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _service = Substitute.For<IDeadMountDespawnService>();
        _service.IsEnabled.Returns(true);
        _sut = new MountDespawnMissionBehavior(_service, Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void RecordKill_OnTheMainThread_RecordsAtOnce()
    {
        _sut.RecordKill(BareAgent(), 7, 12.5f);

        Assert.AreEqual(1, _sut.PendingCount);
        _service.Received(1).OnMountKilled(7, 12.5f);
    }

    [TestMethod]
    public void RecordKill_OffTheMainThread_WaitsForTheNextMissionTick_WithItsOwnDeathTime()
    {
        var mount = BareAgent();
        OnWorker(() => _sut.RecordKill(mount, 7, 12.5f));

        Assert.AreEqual(0, _sut.PendingCount, "the worker must not touch _pending");
        _service.DidNotReceiveWithAnyArgs().OnMountKilled(default, default);

        _sut.OnMissionTick(0.01f);

        Assert.AreEqual(1, _sut.PendingCount);
        _service.Received(1).OnMountKilled(7, 12.5f);
    }

    [TestMethod]
    public void ForgetAgent_OnTheMainThread_AfterAParkedKill_LeavesNoHandleBehind()
    {
        OnWorker(() => _sut.RecordKill(BareAgent(), 7, 12.5f));

        _sut.ForgetAgent(7);

        Assert.AreEqual(0, _sut.PendingCount, "the parked kill must land before the forget, never after it");
        Received.InOrder(() =>
        {
            _service.OnMountKilled(7, 12.5f);
            _service.Forget(7);
        });
    }

    // The deferral must not lean on another behavior having marked the main thread first: an unmarked
    // guard treats every thread as main, which would run an off-thread record inline, silently.
    [TestMethod]
    public void RecordKill_OffThread_ParksOnceTheBehaviorHasTickedEvenIfNothingElseMarkedTheThread()
    {
        MissionThreadGuard.ResetForTests();
        _sut.OnMissionTick(0.01f);

        OnWorker(() => _sut.RecordKill(BareAgent(), 7, 12.5f));

        Assert.AreEqual(0, _sut.PendingCount, "the record must wait for the next mission tick");
        _sut.OnMissionTick(0.01f);
        Assert.AreEqual(1, _sut.PendingCount);
    }

    // A kill parked in a mission's last frames must not replay into the next mission's first tick:
    // its handle belongs to a torn-down mission (#592).
    [TestMethod]
    public void OnEndMission_DropsParkedKills()
    {
        OnWorker(() => _sut.RecordKill(BareAgent(), 7, 12.5f));

        _sut.OnEndMissionInternal();
        _sut.OnMissionTick(0.01f);

        Assert.AreEqual(0, _sut.PendingCount);
        _service.DidNotReceiveWithAnyArgs().OnMountKilled(default, default);
    }

    [TestMethod]
    public void ForgetAgent_OffTheMainThread_ReplaysAfterTheKillItFollows()
    {
        OnWorker(() =>
        {
            _sut.RecordKill(BareAgent(), 7, 12.5f);
            _sut.ForgetAgent(7);
        });

        Assert.AreEqual(0, _sut.PendingCount);
        _service.DidNotReceiveWithAnyArgs().Forget(default);

        _sut.OnMissionTick(0.01f);

        Assert.AreEqual(0, _sut.PendingCount);
        Received.InOrder(() =>
        {
            _service.OnMountKilled(7, 12.5f);
            _service.Forget(7);
        });
    }
}
