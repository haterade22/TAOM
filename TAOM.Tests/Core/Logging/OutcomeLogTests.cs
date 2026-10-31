using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Core.Logging;

/// <summary>
/// Issue #1071: the [Check] line, its once-per-key latch, the hot-path counters, the swallowed-exception
/// count and the [Session] summary. The reader regex below is the one the phase 0 reader (#1070) matches,
/// so a line this class writes and that regex disagree on is a verdict lost.
/// </summary>
[TestClass]
public class OutcomeLogTests
{
    internal const string ReaderCheckPattern = @"\[Check\] id=(\S+) result=(pass|fail|skip)(.*)$";

    private IModLogger _logger = null!;
    private FakeStampClock _clock = null!;
    private long _shield;
    private OutcomeLog _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _clock = new FakeStampClock();
        _shield = 0;
        _sut = new OutcomeLog(_logger, _clock, () => _shield);
    }

    [TestMethod]
    public void Pass_WritesOneInfoLine_TheReaderMatches()
    {
        _sut.Pass("taom.battle-action-bar.attach", "", "field=True");

        var line = InfoLines().Single();
        Assert.AreEqual("[Check] id=taom.battle-action-bar.attach result=pass field=True", line);
        var match = Regex.Match(line, ReaderCheckPattern);
        Assert.IsTrue(match.Success);
        Assert.AreEqual("taom.battle-action-bar.attach", match.Groups[1].Value);
        Assert.AreEqual("pass", match.Groups[2].Value);
        Assert.AreEqual(" field=True", match.Groups[3].Value);
    }

    [TestMethod]
    public void FailAndSkip_WriteTheirOwnResult_AndAnEmptyDetailLeavesNoTrailingSpace()
    {
        _sut.Fail("taom.probe.attach", "", "reason=never-attached");
        _sut.Skip("taom.probe.stance", "", "");
        _sut.Skip("taom.probe.other", "", null!);

        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.probe.attach result=fail reason=never-attached",
            "[Check] id=taom.probe.stance result=skip",
            "[Check] id=taom.probe.other result=skip",
        }, InfoLines());
        Assert.IsTrue(InfoLines().All(l => Regex.IsMatch(l, ReaderCheckPattern)));
    }

    [TestMethod]
    public void WhitespaceInsideAValue_BecomesAnUnderscore_AndThePairsStaySeparate()
    {
        _sut.Fail("taom.probe.attach", "", "reason=layer attach\tfailed\r\nbadly field=True  n=3");

        Assert.AreEqual("[Check] id=taom.probe.attach result=fail reason=layer_attach_failed_badly field=True n=3", InfoLines().Single());
    }

    [TestMethod]
    public void TheLatch_WritesOncePerIdResultAndKey_AndCountsTheRest()
    {
        for (var i = 0; i < 5; i++)
            _sut.Pass("taom.probe.attach", "mission-1", "n=" + i);
        _sut.Pass("taom.probe.attach", "mission-2", "n=9");
        _sut.Pass("taom.probe.stance", "mission-1", "n=9");
        _sut.Fail("taom.probe.attach", "mission-1", "late");
        _sut.Fail("taom.probe.attach", "mission-1", "later");

        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.probe.attach result=pass n=0",
            "[Check] id=taom.probe.attach result=pass n=9",
            "[Check] id=taom.probe.stance result=pass n=9",
            "[Check] id=taom.probe.attach result=fail late",
        }, InfoLines());

        _sut.WriteSummary("save");
        StringAssert.Contains(SummaryLine(), " checks_pass=7 checks_fail=2 ");
    }

    [TestMethod]
    public void ALaterResult_IsWrittenOnce_AfterAnEarlierOneForTheSameKey()
    {
        // A once-per-process check that passed, then failed on a later throw: the reader needs the fail line.
        _sut.Pass("taom.career-screen.open", "", "");
        _sut.Fail("taom.career-screen.open", "", "reason=throw");
        // A skip first must not hide the pass that follows it.
        _sut.Skip("taom.probe.attach", "mission-1", "reason=no-troops");
        _sut.Pass("taom.probe.attach", "mission-1", "");
        _sut.Pass("taom.probe.attach", "mission-1", "");

        CollectionAssert.AreEqual(new[]
        {
            "[Check] id=taom.career-screen.open result=pass",
            "[Check] id=taom.career-screen.open result=fail reason=throw",
            "[Check] id=taom.probe.attach result=skip reason=no-troops",
            "[Check] id=taom.probe.attach result=pass",
        }, InfoLines());
    }

    [TestMethod]
    public void EachIdHoldsAtMost256Keys_ThenOnlyCounts()
    {
        for (var i = 0; i < 300; i++)
            _sut.Pass("taom.probe.attach", "key-" + i, "");
        _sut.Pass("taom.probe.other", "key-0", "");

        Assert.AreEqual(257, InfoLines().Count);
        _sut.WriteSummary("save");
        StringAssert.Contains(SummaryLine(), " checks_pass=301 ");
    }

    [TestMethod]
    public void PassKeysPastTheCap_NeverHideTheFirstFail()
    {
        // A per-day check that passed 300 days, then failed: the fail line is the one the reader needs.
        for (var i = 0; i < 300; i++)
            _sut.Pass("taom.probe.attach", "key-" + i, "");
        _sut.Fail("taom.probe.attach", "key-300", "reason=x");

        Assert.AreEqual("[Check] id=taom.probe.attach result=fail reason=x", InfoLines().Last());
    }

    [TestMethod]
    public void AnInvalidId_IsCountedAsAnError_NeverThrownOrWritten()
    {
        foreach (var id in new[] { "Taom.probe", "taom", "taom..probe", "taom.probe x", "taom.pro_be", "", null! })
            _sut.Pass(id, "", "x=1");
        var counter = _sut.Counter("Not An Id");
        counter.Pass();

        Assert.AreEqual(0, InfoLines().Count);
        _sut.WriteSummary("save");
        StringAssert.Contains(SummaryLine(), " checks_pass=0 ");
        StringAssert.Contains(SummaryLine(), " errors=8 ");
        Assert.IsFalse(InfoLines().Any(l => l.StartsWith("[Session] counter")), "an invalid counter id is not reported");
    }

    [TestMethod]
    public void TheIdRegex_IsTheDesignOne()
    {
        foreach (var good in new[] { "taom.probe", "coop.join.campaign-entered", "taom.a1.b-2.c", "a.b" })
            Assert.IsTrue(OutcomeLog.IsValidId(good), good);
        foreach (var bad in new[] { "taom", "taom.", ".taom", "taom-x.probe", "taom.Probe", "taom.pro be" })
            Assert.IsFalse(OutcomeLog.IsValidId(bad), bad);
    }

    [TestMethod]
    public void Counter_ReturnsOneInstancePerId()
    {
        Assert.AreSame(_sut.Counter("taom.probe.blocked"), _sut.Counter("taom.probe.blocked"));
        Assert.AreNotSame(_sut.Counter("taom.probe.blocked"), _sut.Counter("taom.probe.allowed"));
    }

    [TestMethod]
    public void Counter_FromEightParallelThreads_LosesNoIncrement()
    {
        const int threads = 8;
        const int perThread = 100_000;
        var counter = _sut.Counter("taom.probe.blocked");
        var start = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < perThread; i++)
            {
                counter.Pass();
                if ((i & 1) == 0) counter.Fail();
            }
        })).ToList();
        workers.ForEach(t => t.Start());
        workers.ForEach(t => t.Join());

        Assert.AreEqual(threads * perThread, counter.Passes);
        Assert.AreEqual(threads * perThread / 2, counter.Fails);
        _sut.WriteSummary("save");
        CollectionAssert.Contains(InfoLines(), "[Session] counter id=taom.probe.blocked pass=800000 fail=400000");
    }

    [TestMethod]
    public void TheLatch_FromEightParallelThreads_WritesExactlyOneLine()
    {
        const int threads = 8;
        var start = new Barrier(threads);
        var workers = Enumerable.Range(0, threads).Select(_ => new Thread(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 1000; i++)
                _sut.Pass("taom.probe.attach", "", "");
        })).ToList();
        workers.ForEach(t => t.Start());
        workers.ForEach(t => t.Join());

        Assert.AreEqual(1, InfoLines().Count);
        _sut.WriteSummary("save");
        StringAssert.Contains(SummaryLine(), " checks_pass=8000 ");
    }

    [TestMethod]
    public void Swallowed_WritesOneWarningPerSite_ThenCounts()
    {
        for (var i = 0; i < 5; i++)
            _sut.Swallowed("Enlistment", "GameMenuManagerSetNextMenuPatch", new NullReferenceException("gone"));
        _sut.Swallowed("Enlistment", "other site", new InvalidOperationException("x"));

        var warnings = WarningLines();
        Assert.AreEqual(2, warnings.Count);
        StringAssert.StartsWith(warnings[0], "[Swallowed] feature=Enlistment site=GameMenuManagerSetNextMenuPatch first=NullReferenceException.");
        StringAssert.Contains(warnings[0], "gone");
        StringAssert.StartsWith(warnings[1], "[Swallowed] feature=Enlistment site=other_site first=InvalidOperationException.");
    }

    [TestMethod]
    public void Summary_WritesTheTotals_ThenEachCounter_ThenEachSwallowedSite_InOrder()
    {
        _clock.Frequency = 1000;
        _clock.Advance(1_834_250);
        _shield = 4;
        _sut.Pass("taom.probe.attach", "", "");
        _sut.Pass("taom.probe.attach", "again", "");
        _sut.Fail("taom.probe.stance", "", "");
        _sut.Skip("taom.probe.other", "", "");
        _sut.Counter("taom.probe.zeta").Pass();
        var alpha = _sut.Counter("taom.probe.alpha");
        alpha.Pass();
        alpha.Fail();
        _sut.Swallowed("Zed", "tick", new InvalidOperationException());
        _sut.Swallowed("Alpha", "click", new NullReferenceException());
        _sut.Swallowed("Alpha", "click", new InvalidOperationException());
        _logger.ClearReceivedCalls();

        _sut.WriteSummary("heartbeat");

        CollectionAssert.AreEqual(new[]
        {
            "[Session] summary reason=heartbeat uptime_s=1834 checks_pass=2 checks_fail=1 swallowed=3 shield_swallowed=4 checks_skip=1 errors=0 dropped=0",
            "[Session] counter id=taom.probe.alpha pass=1 fail=1",
            "[Session] counter id=taom.probe.zeta pass=1 fail=0",
            "[Session] swallowed feature=Alpha site=click n=2 first=NullReferenceException",
            "[Session] swallowed feature=Zed site=tick n=1 first=InvalidOperationException",
        }, InfoLines());
    }

    [TestMethod]
    public void TheExitSummary_IsWrittenOnce_OtherReasonsEachTime()
    {
        _sut.WriteSummary("exit");
        _sut.WriteSummary("exit");
        _sut.WriteSummary("save");
        _sut.WriteSummary("save");

        var summaries = InfoLines().Where(l => l.StartsWith("[Session] summary")).ToList();
        Assert.AreEqual(3, summaries.Count);
        Assert.AreEqual(1, summaries.Count(l => l.Contains("reason=exit")));
    }

    [TestMethod]
    public void AShieldCountThatThrows_IsReportedAsUnavailable()
    {
        var sut = new OutcomeLog(_logger, _clock, () => throw new TypeLoadException("no dependencies"));

        sut.WriteSummary("save");

        StringAssert.Contains(SummaryLine(), " shield_swallowed=n/a ");
    }

    [TestMethod]
    public void ALoggerThatThrows_ThrowsFromNoMember()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Throw(new InvalidOperationException("disk gone"));
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Throw(new InvalidOperationException("disk gone"));
        var throwingClock = new FakeStampClock { ThrowOnRead = 1 };
        var sut = new OutcomeLog(_logger, throwingClock, () => 0);

        sut.Pass("taom.probe.attach", "", "x=1");
        sut.Fail("taom.probe.attach", "k", "x=1");
        sut.Skip("taom.probe.attach", "j", "x=1");
        sut.Counter("taom.probe.blocked").Pass();
        sut.Swallowed("Probe", "site", new InvalidOperationException());
        sut.Swallowed(null!, null!, null!);
        sut.WriteSummary("save");
        sut.WriteSummary(null!);
    }

    private List<string> InfoLines() => Received("LogInfo");

    private List<string> WarningLines() => Received("LogWarning");

    private string SummaryLine() => InfoLines().Single(l => l.StartsWith("[Session] summary"));

    private List<string> Received(string method) =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == method)
            .Select(c => (string)c.GetArguments()[0]!)
            .ToList();
}
