using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Hooks;
using TAOM.Features.MissionStartGuard.Models;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.MissionStartGuard;

/// <summary>
/// #1028: DualWield adds its behaviour from a Mission.SpawnAgent postfix, and an arena practice fight started from the town
/// menu spawns from a CampaignEvents.AfterMissionStarted listener that CampaignMissionComponent.AfterStart raises, so the add
/// changed MissionBehaviors while the engine walked it. An add made while a wrapped EarlyStart or
/// AfterStart call runs is queued, and applied when Mission.AfterStart ends. An add outside those two calls (a mod's
/// OnMissionBehaviorInitialize, which runs before the walk) is not touched.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MissionBehaviorDeferralTests
{
    private sealed class Added : MissionLogic { }

    private readonly object _mission = new();

    [TestCleanup]
    public void Cleanup()
    {
        MissionBehaviorDeferral.Flush((_, _) => { });
        MissionStartGuardCalls.Initialize(null);
    }

    [TestMethod]
    public void TryDefer_NoWindowOpen_ReturnsFalse()
    {
        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void TryDefer_WindowOpenForThisMission_QueuesAndReturnsTrue()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsTrue(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
        Assert.AreEqual(1, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void TryDefer_WindowOpenForAnotherMission_ReturnsFalse()
    {
        MissionBehaviorDeferral.Open(new object(), StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
    }

    [TestMethod]
    public void TryDefer_WindowClosedAgain_ReturnsFalse()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.EarlyStart, new Added());
        MissionBehaviorDeferral.Close();

        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
    }

    [TestMethod]
    public void Open_NullMission_NeverDefers()
    {
        MissionBehaviorDeferral.Open(null, StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
    }

    [TestMethod]
    public void TryDefer_NullBehavior_ReturnsFalse()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, null!));
    }

    [TestMethod]
    public void Window_NamesTheCallAndOwnerThatWasRunning()
    {
        var owner = new Added();
        MissionBehaviorDeferral.Open(_mission, StartCall.EarlyStart, owner);

        Assert.AreEqual(StartCall.EarlyStart, MissionBehaviorDeferral.WindowCall);
        Assert.AreSame(owner, MissionBehaviorDeferral.WindowOwner);
    }

    [TestMethod]
    public void Flush_AddsEveryQueuedBehaviourInOrderToTheMissionThatQueuedThem()
    {
        var first = new Added();
        var second = new Added();
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());
        MissionBehaviorDeferral.TryDefer(_mission, first);
        MissionBehaviorDeferral.TryDefer(_mission, second);
        var seen = new List<(object, MissionBehavior)>();

        var count = MissionBehaviorDeferral.Flush((m, b) => seen.Add((m, b)));

        Assert.AreEqual(2, count);
        CollectionAssert.AreEqual(new object[] { first, second }, new object[] { seen[0].Item2, seen[1].Item2 });
        Assert.IsTrue(ReferenceEquals(_mission, seen[0].Item1) && ReferenceEquals(_mission, seen[1].Item1));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void Flush_ClosesTheWindowBeforeAdding_SoTheAddIsNotDeferredAgain()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());
        MissionBehaviorDeferral.TryDefer(_mission, new Added());
        var deferredAgain = true;

        MissionBehaviorDeferral.Flush((m, b) => deferredAgain = MissionBehaviorDeferral.TryDefer(m, b));

        Assert.IsFalse(deferredAgain);
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void Flush_OneAddThrows_StillAddsTheRestAndNeverThrows()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());
        var first = new Added();
        var second = new Added();
        MissionBehaviorDeferral.TryDefer(_mission, first);
        MissionBehaviorDeferral.TryDefer(_mission, second);
        var added = new List<MissionBehavior>();

        var count = MissionBehaviorDeferral.Flush((_, b) =>
        {
            if (ReferenceEquals(b, first)) throw new InvalidOperationException("boom");
            added.Add(b);
        });

        Assert.AreEqual(1, count);
        CollectionAssert.AreEqual(new MissionBehavior[] { second }, added);
    }

    [TestMethod]
    public void Flush_NothingQueued_ReturnsZeroAndAddsNothing()
    {
        var calls = 0;

        var count = MissionBehaviorDeferral.Flush((_, _) => calls++);

        Assert.AreEqual(0, count);
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public void Reset_AfterAnUnflushedMission_DropsTheStaleQueue()
    {
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());
        MissionBehaviorDeferral.TryDefer(_mission, new Added());

        MissionBehaviorDeferral.Reset();

        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
        Assert.IsFalse(MissionBehaviorDeferral.TryDefer(_mission, new Added()));
    }

    // ---- the Harmony prefix and the helpers ----

    [TestMethod]
    public void TryDeferAdd_ToggleOff_DoesNotDefer()
    {
        var service = Substitute.For<IMissionStartGuardService>();
        service.ShouldDeferBehaviorAdds().Returns(false);
        MissionStartGuardCalls.Initialize(service);
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionStartGuardCalls.TryDeferAdd(_mission, new Added()));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void TryDeferAdd_ToggleOnAndWindowOpen_DefersAndReportsTheCallOwnerAndType()
    {
        var service = Substitute.For<IMissionStartGuardService>();
        service.ShouldDeferBehaviorAdds().Returns(true);
        MissionStartGuardCalls.Initialize(service);
        var owner = new Added();
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, owner);

        Assert.IsTrue(MissionStartGuardCalls.TryDeferAdd(_mission, new Added()));

        Assert.AreEqual(1, MissionBehaviorDeferral.Pending);
        service.Received(1).ReportDeferredAdd(StartCall.AfterStart, owner.GetType().FullName!, typeof(Added).FullName!);
    }

    [TestMethod]
    public void TryDeferAdd_NoService_DoesNotDefer()
    {
        MissionStartGuardCalls.Initialize(null);
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionStartGuardCalls.TryDeferAdd(_mission, new Added()));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void TryDeferAdd_ServiceThrows_DoesNotDeferAndDoesNotThrow()
    {
        var service = Substitute.For<IMissionStartGuardService>();
        service.ShouldDeferBehaviorAdds().Returns(_ => throw new InvalidOperationException("boom"));
        MissionStartGuardCalls.Initialize(service);
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsFalse(MissionStartGuardCalls.TryDeferAdd(_mission, new Added()));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void TryDeferAdd_TheReportThrows_StillDefersAndReturnsTrue()
    {
        // The behaviour is queued before the report runs: a failing log line must not let the engine add it a second time.
        var service = Substitute.For<IMissionStartGuardService>();
        service.ShouldDeferBehaviorAdds().Returns(true);
        service.When(x => x.ReportDeferredAdd(Arg.Any<StartCall>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("log failed"));
        MissionStartGuardCalls.Initialize(service);
        MissionBehaviorDeferral.Open(_mission, StartCall.AfterStart, new Added());

        Assert.IsTrue(MissionStartGuardCalls.TryDeferAdd(_mission, new Added()));
        Assert.AreEqual(1, MissionBehaviorDeferral.Pending);
    }

    // ---- Patch103's second class: the prefix on Mission.AddMissionBehavior ----

    private static Mission NewMission()
    {
        try { return (Mission)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Mission)); }
        catch (Exception ex) { Assert.Inconclusive("Mission cannot be allocated in this host: " + ex.GetType().Name); return null!; }
    }

    private static IMissionStartGuardService DeferringService()
    {
        var service = Substitute.For<IMissionStartGuardService>();
        service.ShouldDeferBehaviorAdds().Returns(true);
        MissionStartGuardCalls.Initialize(service);
        return service;
    }

    [TestMethod]
    public void AddMissionBehaviorPrefix_WindowOpenForThisMission_SkipsTheEngineAdd()
    {
        DeferringService();
        var mission = NewMission();
        MissionBehaviorDeferral.Open(mission, StartCall.AfterStart, new Added());

        var runOriginal = Mission_AddMissionBehavior_MissionStartGuard_Patch.Prefix(mission, new Added());

        Assert.IsFalse(runOriginal);
        Assert.AreEqual(1, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void AddMissionBehaviorPrefix_NoWindow_RunsTheEngineAdd()
    {
        DeferringService();

        Assert.IsTrue(Mission_AddMissionBehavior_MissionStartGuard_Patch.Prefix(NewMission(), new Added()));
        Assert.AreEqual(0, MissionBehaviorDeferral.Pending);
    }

    [TestMethod]
    public void AddMissionBehaviorPrefix_WindowOpenForAnotherMission_RunsTheEngineAdd()
    {
        DeferringService();
        MissionBehaviorDeferral.Open(NewMission(), StartCall.AfterStart, new Added());

        Assert.IsTrue(Mission_AddMissionBehavior_MissionStartGuard_Patch.Prefix(NewMission(), new Added()));
    }
}
