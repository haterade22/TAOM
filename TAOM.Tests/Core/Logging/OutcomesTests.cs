using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// Issue #1071: the static <see cref="Outcomes"/> facade that static Harmony patches and pre-IoC code call,
/// and the heartbeat that writes a summary every 10 real minutes. The facade is process-wide state, so each
/// test starts with no instance and puts that back afterwards, by reaching into its private fields.
/// </summary>
[TestClass]
public class OutcomesTests
{
    [TestInitialize]
    public void StartUninitialised() => ResetFacade();

    [TestCleanup]
    public void LeaveUninitialised() => ResetFacade();

    [TestMethod]
    public void BeforeInitialize_EveryMemberIsSafe_AndTheCallsAreCountedAndDropped()
    {
        Outcomes.Pass("taom.probe.attach", "", "x=1");
        Outcomes.Fail("taom.probe.attach", "", "x=1");
        Outcomes.Skip("taom.probe.attach", "", "x=1");
        Outcomes.Swallowed("Probe", "site", new InvalidOperationException());
        Outcomes.WriteSummary("save");
        Outcomes.Tick();

        var logger = Substitute.For<IModLogger>();
        Outcomes.Initialize(new OutcomeLog(logger, new FakeStampClock(), () => 0));
        Outcomes.WriteSummary("save");

        var summary = InfoLines(logger).Single();
        StringAssert.Contains(summary, " checks_pass=0 checks_fail=0 swallowed=0 ");
        StringAssert.EndsWith(summary, " dropped=5");
    }

    [TestMethod]
    public void ACounterTakenBeforeInitialize_KeepsCounting_AndIsReportedAfterIt()
    {
        var early = Outcomes.Counter("taom.probe.early");
        early.Pass();

        var logger = Substitute.For<IModLogger>();
        var log = new OutcomeLog(logger, new FakeStampClock(), () => 0);
        Outcomes.Initialize(log);
        early.Pass();
        Assert.AreSame(early, Outcomes.Counter("taom.probe.early"));
        Outcomes.WriteSummary("save");

        CollectionAssert.Contains(InfoLines(logger), "[Session] counter id=taom.probe.early pass=2 fail=0");
    }

    [TestMethod]
    public void AfterInitialize_EveryMemberReachesTheInstance()
    {
        var log = Substitute.For<IOutcomeLog>();
        var ex = new InvalidOperationException();
        Outcomes.Initialize(log);

        Outcomes.Pass("taom.probe.a", "k", "d");
        Outcomes.Fail("taom.probe.b", "k", "d");
        Outcomes.Skip("taom.probe.c", "k", "d");
        Outcomes.Counter("taom.probe.d");
        Outcomes.Swallowed("Probe", "site", ex);
        Outcomes.WriteSummary("crash");

        log.Received(1).Pass("taom.probe.a", "k", "d");
        log.Received(1).Fail("taom.probe.b", "k", "d");
        log.Received(1).Skip("taom.probe.c", "k", "d");
        log.Received(1).Counter("taom.probe.d");
        log.Received(1).Swallowed("Probe", "site", ex);
        log.Received(1).WriteSummary("crash");
    }

    [TestMethod]
    public void AnInstanceThatThrows_DoesNotEscapeTheFacade()
    {
        var log = Substitute.For<IOutcomeLog>();
        log.When(l => l.Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())).Do(_ => throw new InvalidOperationException());
        log.When(l => l.WriteSummary(Arg.Any<string>())).Do(_ => throw new InvalidOperationException());
        log.Counter(Arg.Any<string>()).Returns(_ => throw new InvalidOperationException());
        Outcomes.Initialize(log);

        Outcomes.Pass("taom.probe.a", "", "");
        Outcomes.WriteSummary("save");
        Assert.IsNotNull(Outcomes.Counter("taom.probe.d"), "a fault still hands back a counter, so a static field initialiser cannot throw");
    }

    [TestMethod]
    public void TheHeartbeat_WritesNothingBeforeTheInterval_ThenOneSummaryPerInterval()
    {
        var log = Substitute.For<IOutcomeLog>();
        var clock = new FakeStampClock { Frequency = 1000 };
        var heartbeat = new SessionHeartbeat(log, clock, TimeSpan.FromMinutes(10));

        clock.Advance(599_999);
        heartbeat.Tick();
        log.DidNotReceive().WriteSummary(Arg.Any<string>());

        clock.Advance(1);
        heartbeat.Tick();
        heartbeat.Tick();
        log.Received(1).WriteSummary("heartbeat");

        // A long stall (a debugger break, a minimised window) writes one summary, not one per missed interval,
        // and the next is due a whole interval after that one.
        clock.Advance(3_600_000);
        heartbeat.Tick();
        clock.Advance(599_999);
        heartbeat.Tick();
        log.Received(2).WriteSummary("heartbeat");
        clock.Advance(1);
        heartbeat.Tick();
        log.Received(3).WriteSummary("heartbeat");
    }

    [TestMethod]
    public void TheHeartbeat_SurvivesAClockAndALogThatThrow()
    {
        var log = Substitute.For<IOutcomeLog>();
        log.When(l => l.WriteSummary(Arg.Any<string>())).Do(_ => throw new InvalidOperationException());
        var clock = new FakeStampClock { Frequency = 1000 };
        var heartbeat = new SessionHeartbeat(log, clock, TimeSpan.FromMinutes(10));

        clock.Advance(600_000);
        heartbeat.Tick();
        clock.ThrowOnRead = 3;
        heartbeat.Tick();
    }

    private static System.Collections.Generic.List<string> InfoLines(IModLogger logger) =>
        logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToList();

    internal static void ResetFacade()
    {
        Field("_log").SetValue(null, null);
        Field("_heartbeat").SetValue(null, null);
        Field("_droppedBeforeInit").SetValue(null, 0L);
        ((ConcurrentDictionary<string, OutcomeCounter>)Field("_earlyCounters").GetValue(null)!).Clear();
    }

    private static FieldInfo Field(string name) =>
        typeof(Outcomes).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Outcomes.{name} is gone; update {nameof(OutcomesTests)}.");
}
