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
using TAOM.Features.MarshalsWord.Hooks;
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
        var session = new MarshalSession(_orders, new FakeMarshalFire(), new FakeMarshalThrowers(), new FakeMarshalOutlines(), screen, settings, () => true, _log, _logger, "1");
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
        var off = new MarshalSession(_orders, new FakeMarshalFire(), new FakeMarshalThrowers(), new FakeMarshalOutlines(), screen, new FakeMarshalSettings { Enabled = false }, () => true, _log, _logger, "1");
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
    [DataRow("marshal_hotkey_failed.txt", "Main/Features/MarshalsWord/MarshalsWordModule.cs", "[MarshalsWord] hotkey registration failed", "ERROR", "hotkey registration failed")]
    [DataRow("marshal_release_failed.txt", "Main/Adapters/MarshalFireAdapter.cs", "[MarshalsWord] giving back a soldier", "ERROR", "giving back a soldier")]
    [DataRow("marshal_throwers_stand_fault.txt", "Main/Features/MarshalsWord/MarshalThrowerDriver.cs", "[MarshalsWord] the thrower stand failed", "ERROR", "the thrower stand failed")]
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

    // ---- stages M3 and M4: the archers' and the mark's lines ----

    private MarshalFireDriver FireDriver(FakeMarshalFire fire)
    {
        MarshalFireTable.Begin();
        return new MarshalFireDriver(_orders, fire, _log, _logger, _ => { }, "1");
    }

    private void FirePass(MarshalFireDriver driver, double now)
    {
        _orders.Now = now;
        driver.Pass();
    }

    [TestMethod]
    public void RenderedLines_ForTheKeys_AreTheFireAndWordLinesOfTheFixtures()
    {
        var fire = new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 6) } };
        var driver = FireDriver(fire);
        _orders.Selected.Add(1);
        driver.PressFire();
        FirePass(driver, 1.0);
        FirePass(driver, 2.0);
        driver.PressWord();
        FirePass(driver, 2.25);
        driver.PressFire();
        FirePass(driver, 3.0);
        driver.PressFire();
        FirePass(driver, 4.0);

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.fire result=pass mission=1 mode=OnWord company=1",
            "[Check] id=taom.marshals-word.word result=pass mission=1 company=1 holding=6",
            "[Check] id=taom.marshals-word.fire result=pass mission=1 mode=Massed company=1",
            "[Check] id=taom.marshals-word.fire result=pass mission=1 mode=Free company=1",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("marshal_full_pass.txt", lines);
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForTheSkips_AreTheOnesTheSkipFixtureHolds()
    {
        var driver = FireDriver(new FakeMarshalFire());
        _orders.Selected.Add(1);
        _orders.Lost.Add(1);
        driver.PressFire();
        FirePass(driver, 1.0);
        _orders.Lost.Clear();
        driver.PressWord();
        FirePass(driver, 2.0);

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.fire result=skip mission=1 reason=ai-controlled company=1",
            "[Check] id=taom.marshals-word.word result=skip mission=1 reason=mode-free company=1",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("marshal_m3_m4_skips.txt", lines);
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForTheHoldWindow_ArePassAndFailAsTheFixturesHoldThem()
    {
        var fire = new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 6) } };
        var driver = FireDriver(fire);
        _orders.Selected.Add(1);
        driver.PressFire();
        FirePass(driver, 1.0);
        for (var t = 1.25; t <= 7.0; t += 0.25)
            FirePass(driver, t);
        var pass = "[Check] id=taom.marshals-word.volley-hold result=pass mission=1 company=1 holding=6 shots=0";
        Assert.IsTrue(CheckLines().Contains(pass), string.Join("\n", CheckLines()));
        AssertInFixture("marshal_full_pass.txt", pass);

        Fresh();
        fire = new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 10) } };
        driver = FireDriver(fire);
        _orders.Selected.Add(1);
        driver.PressFire();
        FirePass(driver, 1.0);
        FirePass(driver, 2.0);
        MarshalFireTable.Count(1, ShotKind.WhileHolding);
        MarshalFireTable.Count(1, ShotKind.WhileHolding);
        for (var t = 2.25; t <= 8.0; t += 0.25)
            FirePass(driver, t);
        var fail = "[Check] id=taom.marshals-word.volley-hold result=fail mission=1 company=1 holding=10 shots=2";
        Assert.IsTrue(CheckLines().Contains(fail), string.Join("\n", CheckLines()));
        AssertInFixture("marshal_volley_hold_fail.txt", fail);
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForTheReleaseWindow_ArePassAndFailAsTheFixturesHoldThem()
    {
        foreach (var (loosed, expected, fixture) in new[]
                 {
                     (8, "[Check] id=taom.marshals-word.volley-release result=pass mission=1 company=1 holding=10 shots=8", "marshal_full_pass.txt"),
                     (1, "[Check] id=taom.marshals-word.volley-release result=fail mission=1 company=1 holding=10 shots=1", "marshal_volley_release_fail.txt"),
                 })
        {
            Fresh();
            var driver = FireDriver(new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 10) } });
            _orders.Selected.Add(1);
            driver.PressFire();
            FirePass(driver, 1.0);
            FirePass(driver, 2.0);
            driver.PressWord();
            FirePass(driver, 2.25);
            for (var i = 0; i < loosed; i++)
                MarshalFireTable.Count(1, ShotKind.AfterWord);
            for (var t = 2.5; t <= 4.0; t += 0.25)
                FirePass(driver, t);

            Assert.IsTrue(CheckLines().Contains(expected), string.Join("\n", CheckLines()));
            AssertInFixture(fixture, expected);
            MarshalFireTable.Clear();
        }
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void RenderedLines_ForAMark_AndItsEffect_AreTheOnesTheFixturesHold()
    {
        var mark = MarshalOrderPayload.ForMark((Formation)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Formation)));
        _orders.Selected.Add(1);
        _queue.CaptureMark(mark);
        _orders.Now = 1.0;
        _queue.Pass();
        var watch = new MarshalMarkWatch(_orders, _log, _logger, "1");
        _orders.Marks = new[] { new MarkSample(2, 6, 5, 8, MarkContext.RangedOnStop) };
        _orders.Now = 1.0;
        watch.Pass();
        _orders.Now = 4.0;
        watch.Pass();

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.mark result=pass mission=1 companies=1",
            "[Check] id=taom.marshals-word.mark-effect result=pass mission=1 company=2 target=6 context=RangedOnStop onTarget=5 of=8",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("marshal_full_pass.txt", lines);

        Fresh();
        var failing = new MarshalMarkWatch(_orders, _log, _logger, "1");
        _orders.Marks = new[] { new MarkSample(2, 6, 2, 8, MarkContext.RangedOnStop) };
        _orders.Now = 1.0;
        failing.Pass();
        _orders.Now = 4.0;
        failing.Pass();
        var fail = "[Check] id=taom.marshals-word.mark-effect result=fail mission=1 company=2 target=6 context=RangedOnStop onTarget=2 of=8";
        CollectionAssert.AreEqual(new[] { fail }, CheckLines());
        AssertInFixture("marshal_mark_effect_fail.txt", fail);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void RenderedLine_ForAMarkNoMemberTook_IsTheNotAppliedSkip()
    {
        var mark = MarshalOrderPayload.ForMark((Formation)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(Formation)));
        _orders.Selected.Add(1);
        _orders.IssueResult = false;
        _queue.CaptureMark(mark);

        _orders.Now = 1.0;
        _queue.Pass();

        var skip = "[Check] id=taom.marshals-word.mark result=skip mission=1 reason=not-applied";
        CollectionAssert.AreEqual(new[] { skip }, CheckLines());
        AssertInFixture("marshal_m3_m4_skips.txt", skip);
    }

    [TestMethod]
    public void TheNewCheckIds_AreValid_AndTheManifestNamesEachOfThem()
    {
        var toml = string.Join("\n", Toml());
        foreach (var id in new[]
                 {
                     MarshalChecks.FireId, MarshalChecks.WordId, MarshalChecks.VolleyHoldId, MarshalChecks.VolleyReleaseId, MarshalChecks.MarkId,
                     MarshalChecks.MarkEffectId, MarshalChecks.HoldCounterId, MarshalChecks.ReleaseCounterId,
                 })
        {
            Assert.IsTrue(OutcomeLog.IsValidId(id), id);
            if (!id.Contains("fire-"))
                StringAssert.Contains(toml, "id = \"" + id + "\"", id);
        }
    }

    private static string[] Toml() => File.ReadAllLines(RepoPath("tools", "ingame_checks", "0.toml"));

    [TestMethod]
    public void RenderedLines_ForAMassedWord_ArePassAndFailAsTheFixturesHoldThem()
    {
        foreach (var (loosed, expected, release, fixture) in new[]
                 {
                     (19, "[Check] id=taom.marshals-word.massed result=pass mission=1 company=1 holding=21 shots=19 eligible=28 stale=2",
                         "[Check] id=taom.marshals-word.volley-release result=pass mission=1 company=1 holding=21 shots=19", "marshal_massed_pass.txt"),
                     (3, "[Check] id=taom.marshals-word.massed result=fail mission=1 company=1 holding=21 shots=3 eligible=28 stale=2",
                         "[Check] id=taom.marshals-word.volley-release result=fail mission=1 company=1 holding=21 shots=3", "marshal_massed_fail.txt"),
                 })
        {
            Fresh();
            var driver = FireDriver(new FakeMarshalFire { Census = { [1] = new FireCensus(1, 6, 21, 3, 0, 2) } });
            _orders.Selected.Add(1);
            driver.PressFire();
            FirePass(driver, 1.0);
            driver.PressFire();
            FirePass(driver, 2.0);
            for (var i = 0; i < loosed; i++)
                MarshalFireTable.Count(1, ShotKind.AfterWord);
            for (var t = 2.25; t <= 5.0; t += 0.25)
                FirePass(driver, t);

            Assert.IsTrue(CheckLines().Contains(expected), string.Join("\n", CheckLines()));
            Assert.IsTrue(CheckLines().Contains(release), string.Join("\n", CheckLines()));
            AssertInFixture(fixture, expected, release);
            MarshalFireTable.Clear();
        }
    }

    [TestMethod]
    public void RenderedLines_ForHoldTheLine_AreTheTogglesAndTheJudgedProofOfTheFixtures()
    {
        foreach (var (controlBlows, heldBlows, expected, fixture) in new[]
                 {
                     (41, 0, "[Check] id=taom.marshals-word.hold-line result=pass mission=1 company=1 flagOff=30 blows=0 control=2 controlBlows=41 seconds=10.3", "marshal_hold_line_pass.txt"),
                     (41, 37, "[Check] id=taom.marshals-word.hold-line result=fail mission=1 company=1 flagOff=30 blows=37 control=2 controlBlows=41 seconds=10.3", "marshal_hold_line_fail.txt"),
                 })
        {
            Fresh();
            var driver = FireDriver(new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 0, 40, 0, 0, 30) } });
            _orders.Selected.Add(1);
            for (var i = 0; i < 2; i++)
                MarshalFireTable.CountBlow(2);
            driver.HoldLine.Press();
            FirePass(driver, 0.0);
            FirePass(driver, 3.0);
            for (var i = 0; i < controlBlows; i++)
                MarshalFireTable.CountBlow(2);
            for (var i = 0; i < heldBlows; i++)
                MarshalFireTable.CountBlow(1);
            FirePass(driver, 13.25);

            Assert.IsTrue(CheckLines().Contains(expected), string.Join("\n", CheckLines()));
            AssertInFixture(fixture, expected);
            MarshalFireTable.Clear();
        }
    }

    [TestMethod]
    public void RenderedLines_ForTheHoldKey_AreTheOnAndOffLinesOfTheFixtures()
    {
        var driver = FireDriver(new FakeMarshalFire());
        _orders.Selected.Add(1);
        driver.HoldLine.Press();
        FirePass(driver, 1.0);
        driver.HoldLine.Press();
        FirePass(driver, 2.0);

        var lines = new[]
        {
            "[Check] id=taom.marshals-word.hold result=pass mission=1 hold=on company=1",
            "[Check] id=taom.marshals-word.hold result=pass mission=1 hold=off company=1",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("marshal_hold_line_pass.txt", lines);
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForAControlThatFoughtTooLittle_IsTheSkipTheFixtureHolds()
    {
        var driver = FireDriver(new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 0, 40, 0, 0, 30) } });
        _orders.Selected.Add(1);
        for (var i = 0; i < 2; i++)
            MarshalFireTable.CountBlow(2);
        driver.HoldLine.Press();
        FirePass(driver, 0.0);
        FirePass(driver, 3.0);
        for (var i = 0; i < 2; i++)
            MarshalFireTable.CountBlow(2);
        FirePass(driver, 13.25);

        var skip = CheckLines().Single(l => l.Contains("hold-line result=skip"));
        StringAssert.EndsWith(skip, "reason=no-control");
        StringAssert.StartsWith(skip, "[Check] id=taom.marshals-word.hold-line result=skip mission=1 company=1 flagOff=30 blows=0 control=2 controlBlows=2");
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForTheSquareAndTheSpacing_AreTheOnesTheFixturesHold()
    {
        var watch = new MarshalProofWatch(_orders, _log, _logger, "1");
        MarshalSpacingPatch.WhyNot = "";
        var infantry = new SpacingLimits(0f, 0.4f, 0f, 0.38f);
        var cavalry = new SpacingLimits(1.7f, 2.0f, 0.18f, 0.5f);
        try
        {
            _orders.Squares = new[] { new SquareRead(1, 14.5f, 9, 5, 40, 40, false) };
            _orders.Spacing = new SpacingRead(1, 41, 1, infantry, infantry, cavalry);
            _orders.Now = 1.0;
            watch.Pass();

            var square = "[Check] id=taom.marshals-word.square result=pass mission=1 company=1 side=14.5 files=9 ranks=5 units=40 placed=40 laidOut=0";
            var spacing = "[Check] id=taom.marshals-word.spacing result=pass mission=1 company=1 units=41 riders=1 minDistance=0.00 maxDistance=0.40 minInterval=0.00 "
                + "maxInterval=0.38 infantry=0.00/0.40/0.00/0.38 cavalry=1.70/2.00/0.18/0.50 reads=Infantry";
            CollectionAssert.AreEqual(new[] { square, spacing }, CheckLines());
            AssertInFixture("marshal_square_pass.txt", square);
            AssertInFixture("marshal_spacing_pass.txt", spacing);

            Fresh();
            watch = new MarshalProofWatch(_orders, _log, _logger, "1");
            _orders.Squares = new[] { new SquareRead(1, 14.5f, 9, 5, 40, 38, false) };
            _orders.Spacing = new SpacingRead(1, 41, 1, cavalry, infantry, cavalry);
            _orders.Now = 1.0;
            watch.Pass();

            AssertInFixture("marshal_square_fail.txt", "[Check] id=taom.marshals-word.square result=fail mission=1 company=1 side=14.5 files=9 ranks=5 units=40 placed=38 laidOut=0");
            AssertInFixture("marshal_spacing_fail.txt", "[Check] id=taom.marshals-word.spacing result=fail mission=1 company=1 units=41 riders=1 minDistance=1.70 maxDistance=2.00 "
                + "minInterval=0.18 maxInterval=0.50 infantry=0.00/0.40/0.00/0.38 cavalry=1.70/2.00/0.18/0.50 reads=Cavalry");
            CollectionAssert.AreEqual(
                new[]
                {
                    "[Check] id=taom.marshals-word.square result=fail mission=1 company=1 side=14.5 files=9 ranks=5 units=40 placed=38 laidOut=0",
                    "[Check] id=taom.marshals-word.spacing result=fail mission=1 company=1 units=41 riders=1 minDistance=1.70 maxDistance=2.00 minInterval=0.18 maxInterval=0.50 "
                    + "infantry=0.00/0.40/0.00/0.38 cavalry=1.70/2.00/0.18/0.50 reads=Cavalry",
                },
                CheckLines());

            Fresh();
            watch = new MarshalProofWatch(_orders, _log, _logger, "1");
            MarshalSpacingPatch.WhyNot = "coop";
            _orders.Spacing = new SpacingRead(1, 41, 1, cavalry, infantry, cavalry);
            _orders.Now = 1.0;
            watch.Pass();

            AssertInFixture("marshal_spacing_not_applied.txt", "[Check] id=taom.marshals-word.spacing result=skip mission=1 company=1 units=41 riders=1 minDistance=1.70 maxDistance=2.00 "
                + "minInterval=0.18 maxInterval=0.50 infantry=0.00/0.40/0.00/0.38 cavalry=1.70/2.00/0.18/0.50 reason=coop");
        }
        finally
        {
            MarshalSpacingPatch.WhyNot = "not-prepared";
        }
    }

    [TestMethod]
    public void RenderedLines_ForAMassedWordWithTooFewHolding_IsTheSkipTheFixtureHolds()
    {
        var driver = FireDriver(new FakeMarshalFire { Census = { [1] = new FireCensus(1, 0, 3) } });
        _orders.Selected.Add(1);
        driver.PressFire();
        FirePass(driver, 1.0);
        driver.PressFire();
        FirePass(driver, 2.0);
        FirePass(driver, 3.0);

        var skip = CheckLines().Single(l => l.Contains("massed result=skip"));
        Assert.AreEqual("[Check] id=taom.marshals-word.massed result=skip mission=1 reason=too-few-holding company=1 holding=3", skip);
        AssertInFixture("marshal_massed_few.txt", skip);
        MarshalFireTable.Clear();
    }

    [TestMethod]
    public void RenderedLines_ForTheSlotAndStrikeChecks_AreTheLinesOfTheFixtures()
    {
        foreach (var (far, steered, closer, farther, struckFlagOn, slot, strike, slotFixture, strikeFixture) in new[]
                 {
                     (2, 8, 6, 2, 3, "[Check] id=taom.marshals-word.hold-slot result=pass mission=1 company=1 qualified=30 far=2 order=Stop steered=8 closer=6 farther=2",
                         "[Check] id=taom.marshals-word.hold-strike result=pass mission=1 company=1 struck=3 flagOn=3", "marshal_hold_slot_pass.txt", "marshal_hold_strike_pass.txt"),
                     (12, 8, 2, 6, 0, "[Check] id=taom.marshals-word.hold-slot result=fail mission=1 company=1 qualified=30 far=12 order=Stop steered=8 closer=2 farther=6",
                         "[Check] id=taom.marshals-word.hold-strike result=fail mission=1 company=1 struck=3 flagOn=0", "marshal_hold_slot_fail.txt", "marshal_hold_strike_fail.txt"),
                 })
        {
            Fresh();
            var fire = new FakeMarshalFire
            {
                HoldCensuses = { [1] = new HoldCensus(1, 30, 30, 30, 30, far, 5, 3, 3, struckFlagOn, 0.5f, 0.1f, "Stop", "", new SlotCounts(closer, farther, steered, 0)) },
            };
            var driver = FireDriver(fire);
            _orders.Selected.Add(1);
            driver.HoldLine.Press();
            FirePass(driver, 0.0);
            FirePass(driver, 1.0);

            Assert.IsTrue(CheckLines().Contains(slot), string.Join("\n", CheckLines()));
            Assert.IsTrue(CheckLines().Contains(strike), string.Join("\n", CheckLines()));
            AssertInFixture(slotFixture, slot);
            AssertInFixture(strikeFixture, strike);
            MarshalFireTable.Clear();
        }
    }

    [TestMethod]
    public void RenderedLines_ForTheSlotLine_NamesTheOrderBesideTheDistance()
    {
        var fire = new FakeMarshalFire { HoldCensuses = { [1] = new HoldCensus(1, 30, 30, 30, 30, 2, 5, 0, 0, 0, 1.25f, 0.4f, "Stop", "", new SlotCounts(0, 0, 0, 2)) } };
        var driver = FireDriver(fire);
        _orders.Selected.Add(1);
        driver.HoldLine.Press();
        FirePass(driver, 0.0);
        FirePass(driver, 1.0);

        var line = Lines(nameof(IModLogger.LogInfo)).Single(l => l.StartsWith("[Marshal] slot ", StringComparison.Ordinal));
        Assert.AreEqual("[Marshal] slot company=1 units=30 qualified=30 flagOff=30 anchored=30 far=2 max=1.3 mean=0.4 order=Stop wounded=5 struck=0 why=none closer=0 farther=0 steered=0 loose=2", line);
        MarshalFireTable.Clear();
    }
}
