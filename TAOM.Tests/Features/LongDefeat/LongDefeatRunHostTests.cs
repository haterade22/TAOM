using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>The host between the mission logic's callbacks and the director: a failed start, a throwing start and a faulting tick all end the run and leave.</summary>
[TestClass]
public class LongDefeatRunHostTests
{
    private static readonly RunPlan Plan = new("helms", new[] { new GarrisonRow("t", FormationSlot.Infantry, 1) },
        new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300)),
        new ShopPlan(0, Array.Empty<ShopOffer>()), RunRules.Default, RunOptions.Default);

    private static (LongDefeatRunHost Host, FakeLongDefeatMenu Menu, LongDefeatSession Session, RecordingOutcomeLog Outcomes) NewHost(
        Func<TAOM.Adapters.ILongDefeatBattlefieldAdapter> field)
    {
        var menu = new FakeLongDefeatMenu();
        var session = new LongDefeatSession();
        var outcomes = new RecordingOutcomeLog();
        var host = new LongDefeatRunHost(Plan, session, Substitute.For<IModLogger>(), outcomes, menu, "m1", field);
        return (host, menu, session, outcomes);
    }

    [TestInitialize]
    public void Init() => MissionThreadGuard.ResetForTests();

    [TestCleanup]
    public void Clean() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void WantsHits_IsFalse_AndAHitIsIgnored_BeforeTheRunStarts()
    {
        var (host, menu, _, _) = NewHost(() => new FakeLongDefeatField());

        Assert.IsFalse(host.WantsHits);
        host.OnHit("D:man", "A:orc");

        Assert.AreEqual(0, menu.EndGames);
    }

    [TestMethod]
    public void WantsHits_IsFalse_ForARunWithNoBladesWave_AndAfterTheShutDown()
    {
        var (host, _, _, _) = NewHost(() => new FakeLongDefeatField());
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        Assert.IsFalse(host.WantsHits, "wave 1 carries no affix");
        host.ShutDown();
        Assert.IsFalse(host.WantsHits);
    }

    [TestMethod]
    public void AContactHit_AndABodyEvent_BeforeTheRunStarts_AreIgnored()
    {
        var (host, menu, _, _) = NewHost(() => new FakeLongDefeatField());

        host.OnContactHit("A:orc", "P", 40f);
        host.OnBodyEvent("P", "removed", "Routed");

        Assert.AreEqual(0, menu.EndGames);
    }

    [TestMethod]
    public void ABodyEvent_ReachesTheField_OnlyOnceTheRunHasStarted()
    {
        var field = new FakeLongDefeatField();
        var (host, _, _, _) = NewHost(() => field);
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        host.OnBodyEvent("P", "removed", "Routed");

        Assert.AreEqual(1, field.BodyNotes);
    }

    [TestMethod]
    public void WatchesBodies_IsTrueOnlyForAMountedClass()
    {
        var (foot, _, _, _) = NewHost(() => new FakeLongDefeatField());
        var mounted = new LongDefeatRunHost(Plan with { Loadout = new LdLoadout("cavalry", true, Array.Empty<LdKitSlot>()) }, new LongDefeatSession(),
            Substitute.For<IModLogger>(), new RecordingOutcomeLog(), new FakeLongDefeatMenu(), "m1", () => new FakeLongDefeatField());

        Assert.IsFalse(foot.WatchesBodies);
        Assert.IsTrue(mounted.WatchesBodies);
    }

    [TestMethod]
    public void Step_BeforeTheStartIsDue_DoesNothing_AndNeverAsksTheMissionClock()
    {
        var (host, menu, session, _) = NewHost(() => new FakeLongDefeatField());

        host.Step(0.1f, () => throw new InvalidOperationException("the clock was read"));

        Assert.AreEqual(0, menu.EndGames);
        Assert.IsFalse(session.IsActive);
    }

    [TestMethod]
    public void Step_BeforeTheEngineIsReady_WaitsForTheFirstHalfSecond()
    {
        var (host, _, session, _) = NewHost(() => new FakeLongDefeatField());
        host.MarkStartDue();

        host.Step(0.1f, () => 0.2f);
        Assert.IsFalse(session.IsActive);

        host.Step(0.1f, () => 0.6f);
        Assert.IsTrue(session.IsActive);
    }

    [TestMethod]
    public void Step_AFieldThatCannotBoot_LeavesThroughTheMenu()
    {
        var (host, menu, session, _) = NewHost(() => new FakeLongDefeatField { BootOk = false });
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);

        Assert.AreEqual(1, menu.EndGames);
        Assert.AreEqual("Boot", session.Fault, "the autorun driver reads this at once instead of waiting out its start timeout");
    }

    [TestMethod]
    public void Step_AStartThatThrows_TakesTheSameRouteAsAFailedBoot_AndRecordsTheFault()
    {
        var (host, menu, session, outcomes) = NewHost(() => throw new InvalidOperationException("the engine call threw"));
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);

        Assert.AreEqual(1, menu.EndGames, "the player leaves instead of staring at an open mission");
        Assert.AreEqual("OnMissionTick", session.Fault);
        Assert.IsTrue(outcomes.Lines.Any(l => l.StartsWith("fail " + LongDefeatChecks.EndId + " m1 ") && l.Contains("reason=fault")), string.Join("\n", outcomes.Lines));

        host.Step(0.1f, () => 2f);
        Assert.AreEqual(1, menu.EndGames, "a faulted host starts nothing again");
    }

    [TestMethod]
    public void Step_AStartThatThrowsHalfwayThrough_ShutsTheDirectorDown()
    {
        var field = new FakeLongDefeatField { ThrowOnRead = true };
        var (host, menu, session, _) = NewHost(() => field);
        host.MarkStartDue();

        host.Step(0.1f, () => 1f);

        Assert.AreEqual(1, menu.EndGames);
        Assert.AreEqual("OnMissionTick", session.Fault);
        Assert.IsFalse(session.IsActive, "the partial garrison's run is not left filled");
        Assert.AreEqual(1, field.Count("Clear"));
    }

    [TestMethod]
    public void Step_AFaultInAStartedRun_AbandonsItAndLeaves()
    {
        var field = new FakeLongDefeatField();
        var (host, menu, session, _) = NewHost(() => field);
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);
        Assert.IsTrue(session.IsActive, "the run started");
        Assert.AreEqual(0, menu.EndGames);

        field.ThrowOnRead = true;
        host.Step(0.1f, () => 2f);

        Assert.AreEqual(1, menu.EndGames);
        Assert.AreEqual("OnMissionTick", session.Fault);
        Assert.AreEqual(RunEndReason.Abandoned, session.Result!.Reason, "the run is abandoned and its report written");
        Assert.IsFalse(session.IsActive);
        Assert.AreEqual(1, field.Count("Clear"));
        var calls = field.Calls.Count;
        field.ThrowOnRead = false;
        host.Step(0.1f, () => 3f);
        Assert.AreEqual(calls, field.Calls.Count, "no tick reaches a director that was shut down");
    }

    [TestMethod]
    public void ShutDown_WithARunGoing_AbandonsItOnceAndClearsTheField()
    {
        var field = new FakeLongDefeatField();
        var (host, _, session, _) = NewHost(() => field);
        host.MarkStartDue();
        host.Step(0.1f, () => 1f);

        host.ShutDown();
        host.ShutDown();

        Assert.AreEqual(RunEndReason.Abandoned, session.Result!.Reason);
        Assert.AreEqual(1, field.Count("Clear"));
        Assert.IsNull(session.Fault, "an ordinary exit is not a fault");
    }

    [TestMethod]
    public void Leave_Twice_EndsTheGameOnce()
    {
        var (host, menu, _, _) = NewHost(() => new FakeLongDefeatField());

        host.Leave();
        host.Leave();

        Assert.AreEqual(1, menu.EndGames);
    }
}
