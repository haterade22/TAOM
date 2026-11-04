using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Client.Features.MarshalsWord;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The [Check] lines Marshal's Word writes, read by tools/ingame_checks/0.toml. The drivers and the session run against the fake engine side
/// and a real <see cref="OutcomeLog"/>, so the lines are the ones the game writes; the Python fixtures that <c>MarshalsWordManifestTests</c>
/// reads must not drift from them (the Seat's twin is <c>SeatOfSeeingChecksRenderTests</c>).
/// </summary>
[TestClass]
public class MarshalsWordChecksRenderTests
{
    private const string Fixtures = "ingame_verdict";

    private IModLogger _logger = null!;
    private OutcomeLog _log = null!;
    private FakeMarshalOrders _orders = null!;
    private MarshalQueueDriver _queue = null!;
    private MarshalMarchDriver _march = null!;

    [TestInitialize]
    public void Setup() => Fresh();

    // Each scenario starts a new process-like log, because the real log writes a line only the first time it sees (id, result, key).
    private void Fresh()
    {
        _logger = Substitute.For<IModLogger>();
        _log = new OutcomeLog(_logger, new FakeStampClock(), () => 0);
        _orders = new FakeMarshalOrders();
        _queue = new MarshalQueueDriver(_orders, _log, "1");
        _march = new MarshalMarchDriver(_orders, _log, "1");
    }

    private string[] CheckLines() => Lines(nameof(IModLogger.LogInfo)).Where(l => l.StartsWith("[Check] ", StringComparison.Ordinal)).ToArray();

    private string[] Lines(string method) =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == method)
            .Select(c => c.GetArguments()[0]?.ToString() ?? string.Empty)
            .ToArray();

    private static string[] Fixture(string fixture)
    {
        var path = RepoPath("tools", "tests", "fixtures", Fixtures, fixture);
        Assert.IsTrue(File.Exists(path), path);
        return File.ReadAllLines(path);
    }

    private static void AssertInFixture(string fixture, params string[] lines)
    {
        var all = Fixture(fixture);
        foreach (var line in lines)
            Assert.IsTrue(all.Any(l => l.EndsWith("] [INFO] " + line, StringComparison.Ordinal)), fixture + " lacks the rendered line: " + line);
    }

    private void QueueStep(StepEnd end, int company = 1)
    {
        _orders.Selected.Clear();
        _orders.Selected.Add(company);
        Assert.AreEqual(CaptureResult.Queued, _queue.Capture(end, new FakePayload("step"), "Position:Move"));
    }

    private void QueuePass(double now)
    {
        _orders.Now = now;
        _queue.Pass();
    }

    private void MarchPass(double now)
    {
        _orders.Now = now;
        _march.Pass(true);
    }

    private static MarchLeg Leg(int company, float remaining, float shift = 0f) => new(company, remaining, 4f, shift);

    private static readonly MarchTarget[] Pair = { new(1, 0f, 0f), new(2, 0f, 0f) };

    [TestMethod]
    public void CheckIds_AreValid()
    {
        foreach (var id in new[]
                 {
                     MarshalChecks.CapturedId, MarshalChecks.DeclinedId, MarshalChecks.StartedId, MarshalChecks.FinishedId, MarshalChecks.DroppedId,
                     MarshalChecks.ClearedId, MarshalChecks.PatienceId, MarshalChecks.ArrayId, MarshalChecks.MarchId, MarshalChecks.SpreadId,
                     MarshalChecks.FaultId, MarshalChecks.PreviewCounterId,
                 })
            Assert.IsTrue(OutcomeLog.IsValidId(id), id);
    }

    [TestMethod]
    public void RenderedLines_ForAMove_AreTheOnesTheCaptureStartAndFinishedFixturesRead()
    {
        QueueStep(StepEnd.Arrival);
        QueuePass(10);
        _orders.Arrivals.Add(1);
        QueuePass(11);

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.captured result=pass mission=1 kind=Position:Move companies=1",
            "[Check] id=taom.marshals-word.started result=pass mission=1 end=Arrival companies=1",
            "[Check] id=taom.marshals-word.finished result=pass mission=1 reason=arrival end=Arrival company=1 behind=0",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        foreach (var fixture in new[]
                 {
                     "marshal_full_pass.txt", "marshal_one_arrival.txt", "marshal_two_moves.txt", "marshal_fault.txt", "marshal_captured_then_dropped.txt",
                     "marshal_captured_never_started.txt",
                 })
            AssertInFixture(fixture, lines[0]);
        foreach (var fixture in new[] { "marshal_full_pass.txt", "marshal_one_arrival.txt", "marshal_two_moves.txt", "marshal_fault.txt" })
            AssertInFixture(fixture, lines[1], lines[2]);
        AssertInFixture("marshal_patience_fail.txt", lines[0], lines[1]);
    }

    [TestMethod]
    public void RenderedLines_ForTwoMoves_AreTwoFinishedLines_BecauseTheKeyNamesTheStep_AndOnlyTheSecondIsBehindOne()
    {
        // The real log writes one line per (id, result, key). With a key of mission and reason alone, the second arrival would write nothing.
        QueueStep(StepEnd.Arrival);
        QueueStep(StepEnd.Arrival);
        _orders.Arrivals.Add(1);
        QueuePass(10);

        var finished = CheckLines().Where(l => l.Contains("id=taom.marshals-word.finished")).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.marshals-word.finished result=pass mission=1 reason=arrival end=Arrival company=1 behind=0",
            "[Check] id=taom.marshals-word.finished result=pass mission=1 reason=arrival end=Arrival company=1 behind=1",
        }, finished);
        AssertInFixture("marshal_two_moves.txt", finished[0], finished[1]);
        AssertInFixture("marshal_full_pass.txt", finished[1]);
    }

    [TestMethod]
    public void RenderedLines_ForTwoLoneMoves_AreBothBehindZero_ForTheFixtureThatReadsNotReached()
    {
        QueueStep(StepEnd.Arrival, company: 1);
        QueueStep(StepEnd.Arrival, company: 2);
        _orders.Arrivals.Add(1);
        _orders.Arrivals.Add(2);
        QueuePass(10);

        var finished = CheckLines().Where(l => l.Contains("id=taom.marshals-word.finished")).ToArray();
        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.marshals-word.finished result=pass mission=1 reason=arrival end=Arrival company=1 behind=0",
            "[Check] id=taom.marshals-word.finished result=pass mission=1 reason=arrival end=Arrival company=2 behind=0",
        }, finished);
        AssertInFixture("marshal_two_lone_moves.txt", finished);
    }

    [TestMethod]
    public void RenderedLines_ForAStepThatEndsByPatience_AreASkipLineWithTheReason()
    {
        QueueStep(StepEnd.Arrival);
        QueuePass(10);
        QueuePass(10 + MarshalTuning.MinPatience + 1);

        var patience = "[Check] id=taom.marshals-word.finished result=skip mission=1 reason=patience end=Arrival company=1 behind=0";
        CollectionAssert.Contains(CheckLines(), patience);
        AssertInFixture("marshal_arrival_by_patience_only.txt", patience);
        AssertInFixture("marshal_full_pass.txt", patience);
        AssertInFixture("marshal_patience_fail.txt", patience);
    }

    [TestMethod]
    public void RenderedLines_ForTheEighthFinishedStep_AreThePatiencePassAndTheFailTheFixturesRead()
    {
        // Seven arrivals and one patience end: eight finished, one by patience, within one in four.
        for (var i = 0; i < 8; i++)
        {
            QueueStep(StepEnd.Arrival);
            QueuePass(i * 100.0);
            if (i == 3)
            {
                QueuePass(i * 100.0 + 50.0);
            }
            else
            {
                _orders.Arrivals.Add(1);
                QueuePass(i * 100.0 + 10.0);
                _orders.Arrivals.Clear();
            }
        }

        var pass = "[Check] id=taom.marshals-word.patience result=pass mission=1 finished=8 by-patience=1";
        CollectionAssert.Contains(CheckLines(), pass);
        AssertInFixture("marshal_full_pass.txt", pass);

        // Five arrivals and three patience ends: above one in four.
        Fresh();
        for (var i = 0; i < 8; i++)
        {
            QueueStep(StepEnd.Arrival);
            QueuePass(i * 100.0);
            if (i < 3)
            {
                QueuePass(i * 100.0 + 50.0);
            }
            else
            {
                _orders.Arrivals.Add(1);
                QueuePass(i * 100.0 + 10.0);
                _orders.Arrivals.Clear();
            }
        }

        var fail = "[Check] id=taom.marshals-word.patience result=fail mission=1 finished=8 by-patience=3";
        CollectionAssert.Contains(CheckLines(), fail);
        AssertInFixture("marshal_patience_fail.txt", fail);
    }

    [TestMethod]
    public void RenderedLines_ForADirectOrder_AClearedQueue()
    {
        QueueStep(StepEnd.Arrival);
        QueueStep(StepEnd.Arrival);
        _queue.OnDirectOrder(new[] { 1 });

        var cleared = "[Check] id=taom.marshals-word.cleared result=pass mission=1 company=1 steps=2";
        CollectionAssert.Contains(CheckLines(), cleared);
        AssertInFixture("marshal_full_pass.txt", cleared);
    }

    [TestMethod]
    public void RenderedLines_ForEachDecline_OneSkipLinePerReason()
    {
        _queue.Capture(StepEnd.Arrival, new FakePayload("a"), "Position:Move");
        _orders.Selected.Add(1);
        _orders.IsDeployment = true;
        _queue.Capture(StepEnd.Arrival, new FakePayload("a"), "Position:Move");
        _orders.IsDeployment = false;
        _orders.Lost.Add(1);
        _queue.Capture(StepEnd.Arrival, new FakePayload("a"), "Position:Move");
        _orders.Lost.Clear();
        _queue.Capture(null, new FakePayload("followme"), "Agent:FollowMe");

        var skips = new[]
        {
            "[Check] id=taom.marshals-word.declined result=skip mission=1 reason=none-selected",
            "[Check] id=taom.marshals-word.declined result=skip mission=1 reason=deployment",
            "[Check] id=taom.marshals-word.declined result=skip mission=1 reason=ai-controlled",
            "[Check] id=taom.marshals-word.declined result=skip mission=1 reason=not-allowed",
        };
        CollectionAssert.AreEqual(skips, CheckLines());
        AssertInFixture("marshal_declines_only.txt", skips[1], skips[2]);
        AssertInFixture("marshal_full_pass.txt", skips[0]);
    }

    [TestMethod]
    public void TheDeclinedPattern_NamesEveryReasonTheDriverGives()
    {
        var pattern = Toml().Single(l => l.Contains(@"marshals-word\.declined result=skip mission"));

        foreach (var reason in new[] { "none-selected", "not-allowed", "deployment", "ai-controlled" })
            StringAssert.Contains(pattern, reason);
    }

    [TestMethod]
    public void RenderedLines_ForACompanyThatLostControl_AreADroppedSkipLine()
    {
        QueueStep(StepEnd.Arrival);
        _orders.Lost.Add(1);
        QueuePass(1);

        var dropped = "[Check] id=taom.marshals-word.dropped result=skip mission=1 reason=lost-control company=1";
        CollectionAssert.Contains(CheckLines(), dropped);
        AssertInFixture("marshal_captured_then_dropped.txt", dropped);
    }

    [TestMethod]
    public void RenderedLines_ForACaptureBehindAWalkThatADirectOrderClears_AreTheImplicitStartAndTheClear()
    {
        _orders.Walking.Add(1);
        QueueStep(StepEnd.Arrival);
        QueuePass(1);
        _queue.OnDirectOrder(new[] { 1 });

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.captured result=pass mission=1 kind=Position:Move companies=1",
            "[Check] id=taom.marshals-word.started result=pass mission=1 end=ImplicitArrival companies=1",
            "[Check] id=taom.marshals-word.cleared result=pass mission=1 company=1 steps=2",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("marshal_captured_behind_a_walk_cleared.txt", lines);
    }

    [TestMethod]
    public void RenderedLines_ForTheMarch_AreThePassTheNoTestSkipAndTheReorderedSkip()
    {
        _march.Request(Pair);
        _orders.Legs = new[] { Leg(1, 99), Leg(2, 51) };
        MarchPass(0);
        _orders.Legs = new[] { Leg(1, 2f), Leg(2, 5f) };
        MarchPass(24);

        var march = "[Check] id=taom.marshals-word.march result=pass mission=1 companies=2";
        var spread = "[Check] id=taom.marshals-word.march-spread result=pass mission=1 spread=0.5 longest=24.0 shortest=12.0";
        CollectionAssert.AreEqual(new[] { march, spread }, CheckLines());
        AssertInFixture("marshal_full_pass.txt", march, spread);
        AssertInFixture("marshal_spread_fail.txt", march);

        Fresh();
        _march.Request(Pair);
        _orders.Legs = new[] { Leg(1, 103), Leg(2, 103) };
        MarchPass(0);
        _orders.Legs = new[] { Leg(1, 2f), Leg(2, 2.5f) };
        MarchPass(25);

        var noTest = "[Check] id=taom.marshals-word.march-spread result=skip mission=1 spread=0.0 longest=25.0 shortest=25.0 reason=no-test";
        CollectionAssert.Contains(CheckLines(), noTest);
        AssertInFixture("marshal_march_no_test.txt", noTest);

        Fresh();
        _march.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        MarchPass(0);
        _orders.Legs = new[] { Leg(1, 90), Leg(2, 40, shift: 12f) };
        MarchPass(1);

        var reordered = "[Check] id=taom.marshals-word.march-spread result=skip mission=1 reason=reordered";
        CollectionAssert.Contains(CheckLines(), reordered);
        AssertInFixture("marshal_march_reordered.txt", reordered);
    }

    [TestMethod]
    public void RenderedLines_ForAMarchThatFailsTheSpread()
    {
        _march.Request(Pair);
        _orders.Legs = new[] { Leg(1, 103), Leg(2, 33) };
        MarchPass(0);
        _orders.Legs = new[] { Leg(1, 99), Leg(2, 2) };
        MarchPass(1);
        _orders.Legs = new[] { Leg(1, 5), Leg(2, 2) };
        MarchPass(40);

        var fail = "[Check] id=taom.marshals-word.march-spread result=fail mission=1 spread=39.5 longest=25.0 shortest=7.5";
        CollectionAssert.Contains(CheckLines(), fail);
        AssertInFixture("marshal_spread_fail.txt", fail);
    }

    [TestMethod]
    public void RenderedLines_ForTheSession_AreTheArrayLines_TheFaultAndTheSkipOfASessionThatNeverWentLive()
    {
        var screen = new FakeMarshalScreen();
        var settings = new FakeMarshalSettings();
        var session = new MarshalSession(_orders, screen, settings, () => true, _log, _logger, "1");
        session.OnMissionTick();
        session.ArraySolved(click: true, companies: 2);
        session.ArraySolved(click: false, companies: 2);
        session.ArrayFault(new NullReferenceException());
        _orders.FailOnNow = new InvalidOperationException("boom");
        session.OnMissionTick();

        var array = new[]
        {
            "[Check] id=taom.marshals-word.array result=pass mission=1 kind=click companies=2",
            "[Check] id=taom.marshals-word.array result=pass mission=1 kind=drag companies=2",
            "[Check] id=taom.marshals-word.array result=fail mission=1 reason=fault error=NullReferenceException",
        };
        var fault = "[Check] id=taom.marshals-word.fault result=fail mission=1 error=InvalidOperationException";
        CollectionAssert.AreEqual(array.Concat(new[] { fault }).ToArray(), CheckLines());
        AssertInFixture("marshal_full_pass.txt", array[0], array[1]);
        AssertInFixture("marshal_array_fault.txt", array[0], array[2]);
        AssertInFixture("marshal_fault.txt", fault);
        AssertInFixture("marshal_array_click_only.txt", array[0]);
        AssertInFixture("marshal_spread_fail.txt", array[1]);

        const string prefix = "[MarshalsWord] Marshal's Word failed and is off for this mission, orders run as in the base game: System.InvalidOperationException: boom";
        StringAssert.StartsWith(Lines(nameof(IModLogger.LogError)).Last(), prefix);
        Assert.IsTrue(Fixture("marshal_fault.txt").Any(l => l.EndsWith("] [ERROR] " + prefix, StringComparison.Ordinal)));

        Fresh();
        var off = new MarshalSession(_orders, screen, new FakeMarshalSettings { Enabled = false }, () => true, _log, _logger, "1");
        off.OnMissionTick();

        var skip = "[Check] id=taom.marshals-word.captured result=skip mission=1 reason=setting-off";
        CollectionAssert.AreEqual(new[] { skip }, CheckLines());
        AssertInFixture("marshal_session_off.txt", skip);
    }

    [TestMethod]
    public void RenderedLines_ForAFullQueue_AreACapturedSkipWithTheReason()
    {
        _orders.Selected.Add(1);
        for (var i = 0; i < MarshalTuning.MaxStepsPerCompany; i++)
            _queue.Capture(StepEnd.Arrival, new FakePayload("s" + i), "Position:Move");
        _queue.Capture(StepEnd.Arrival, new FakePayload("late"), "Position:Move");

        CollectionAssert.Contains(CheckLines(), "[Check] id=taom.marshals-word.captured result=skip mission=1 reason=full");
        CollectionAssert.Contains(CheckLines(), "[Check] id=taom.marshals-word.captured result=pass mission=1 kind=Position:Move companies=1");
    }

    [DataTestMethod]
    [DataRow("marshal_could_not_start.txt", "Client/Features/MarshalsWord/MarshalView.cs", "[MarshalsWord] Marshal's Word could not start for this mission", "ERROR", "Word could not start")]
    [DataRow("marshal_hotkey_failed.txt", "Main/Features/MarshalsWord/MarshalsWordModule.cs", "[MarshalsWord] hotkey registration failed", "WARNING", "hotkey registration failed")]
    public void TheFeaturesOwnFailureLines_AreInTheCode_TheFixtures_AndTheCaptureChecksFailList(string fixture, string source, string prefix, string level, string manifestFragment)
    {
        StringAssert.Contains(ReadSource(source, stripComments: true), prefix, source);

        Assert.IsTrue(Fixture(fixture).Any(l => l.Contains("] [" + level + "] " + prefix)), fixture);
        Assert.IsTrue(Toml().Any(l => l.Contains(@"\[MarshalsWord\] ") && l.Contains(manifestFragment)), "0.toml names '" + manifestFragment + "'");
    }

    [TestMethod]
    public void TheStartedPattern_NamesEveryStepEndButTheImplicitWait()
    {
        var pattern = Toml().Single(l => l.Contains(@"marshals-word\.started result=pass"));

        foreach (var end in Enum.GetNames(typeof(StepEnd)).Where(n => n != nameof(StepEnd.ImplicitArrival)))
            StringAssert.Contains(pattern, end);
        Assert.IsFalse(pattern.Contains(nameof(StepEnd.ImplicitArrival)),
            "an implicit wait starts even when the captured order is never issued, so it proves nothing about the order");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Describe_IsTheKindOfTheCapturedLine_CallAndOrderType()
    {
        Assert.AreEqual("Position:Move", MarshalOrderPayload.ForPosition(OrderType.Move, default).Describe());
        Assert.AreEqual("Order:Charge", MarshalOrderPayload.ForOrder(OrderType.Charge).Describe());
        Assert.AreEqual("TwoPositions:MoveToLineSegment", MarshalOrderPayload.ForTwoPositions(OrderType.MoveToLineSegment, default, default).Describe());
    }

    private static string[] Toml() => File.ReadAllLines(RepoPath("tools", "ingame_checks", "0.toml"));
}
