using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.LoadTimeStamps;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The [Check] lines of mission time and the lift of the view, rendered by a real OutcomeLog from the session, against the Python fixtures that
/// <c>SeatOfSeeingManifestTests</c> reads: a fixture that drifts from the emitter would keep the manifest green over a line the game never writes.
/// </summary>
[TestClass]
public class SeatTimeRiseChecksRenderTests
{
    private static readonly SeatPermissions Allowed = new(Camera: true, FightOn: true, Orders: true, Time: true, TakeSoldier: true);

    private IModLogger _logger = null!;
    private FakeSeatScreen _screen = null!;
    private FakeSeatSettings _settings = null!;
    private SeatPermissions _permissions = Allowed;
    private SeatSession _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        _logger = Substitute.For<IModLogger>();
        _screen = new FakeSeatScreen();
        _settings = new FakeSeatSettings { RiseWithPanel = true };
        _sut = new SeatSession(_screen, new FakeSeatHero(), _settings, () => _permissions, new OutcomeLog(_logger, new FakeStampClock(), () => 0), _logger, "1");
    }

    [TestCleanup]
    public void Cleanup() => SeatPresence.Set(false);

    private string[] CheckLines() =>
        _logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0])
            .Where(l => l.StartsWith("[Check] ", StringComparison.Ordinal))
            .ToArray();

    private static void AssertInFixture(string fixture, params string[] lines)
    {
        var path = RepoPath("tools", "tests", "fixtures", "ingame_verdict", fixture);
        Assert.IsTrue(File.Exists(path), path);
        var all = File.ReadAllLines(path);
        foreach (var line in lines)
            Assert.IsTrue(all.Any(l => l.EndsWith("] [INFO] " + line, StringComparison.Ordinal)), fixture + " lacks the rendered line: " + line);
    }

    [TestMethod]
    public void CheckIds_AreValid()
    {
        Assert.IsTrue(OutcomeLog.IsValidId(SeatChecks.TimeId));
        Assert.IsTrue(OutcomeLog.IsValidId(SeatChecks.RiseId));
    }

    [TestMethod]
    public void RenderedLines_OfALevelAPauseAndAResume_AreTheOnesTheFixtureReads()
    {
        _sut.PressTime(SeatTimeKey.Quicken);
        _sut.PressTime(SeatTimeKey.Stay);
        _sut.PressTime(SeatTimeKey.Stay);

        var lines = new[]
        {
            "[Check] id=taom.seat-of-seeing.time result=pass mission=1 level=2 event=level",
            "[Check] id=taom.seat-of-seeing.time result=pass mission=1 level=1 event=stay",
            "[Check] id=taom.seat-of-seeing.time result=pass mission=1 level=1 event=resume",
        };
        CollectionAssert.AreEqual(lines, CheckLines());
        AssertInFixture("seat_panel_visit_pass.txt", lines);
        AssertInFixture("seat_time_level_only.txt", lines[0]);
    }

    [TestMethod]
    public void RenderedLines_OfEveryRefusal_AreTheOnesTheSkipFixtureReads()
    {
        _permissions = Allowed with { Time = false };
        _sut.PressTime(SeatTimeKey.Quicken);
        _permissions = Allowed;
        _screen.OwnedElsewhere = true;
        _sut.PressTime(SeatTimeKey.Quicken);
        _settings.Enabled = false;
        _sut.PressTime(SeatTimeKey.Slow);

        var lines = new[]
        {
            "[Check] id=taom.seat-of-seeing.time result=skip mission=1 reason=denied",
            "[Check] id=taom.seat-of-seeing.time result=skip mission=1 reason=other-clock",
            "[Check] id=taom.seat-of-seeing.time result=skip mission=1 reason=setting-off",
        };
        CollectionAssert.AreEquivalent(lines, CheckLines());
        AssertInFixture("seat_time_skips.txt", lines);
    }

    [TestMethod]
    public void RenderedLine_OfACancelThatLeftTimeOn_IsTheOneTheFailFixtureReads()
    {
        _sut.PressTime(SeatTimeKey.Quicken);
        _sut.OnFrameTick(0.016f);
        Assert.IsTrue(_screen.FixedStep);
        _screen.StopThrows = new InvalidOperationException("stop");
        _screen.PausedThrows = new InvalidOperationException("pause");
        _sut.PressTime(SeatTimeKey.Stay);
        for (var i = 0; i < 6; i++)
            _sut.OnFrameTick(0.016f);

        var failLine = CheckLines().Single(l => l.Contains("result=fail") && l.Contains("time"));
        Assert.AreEqual("[Check] id=taom.seat-of-seeing.time result=fail mission=1 reason=cancel-failed tries=3", failLine);
        AssertInFixture("seat_time_cancel_failed.txt", failLine);
    }

    [TestMethod]
    public void RenderedLine_OfTheFirstLift_IsTheOneTheVisitFixtureReads()
    {
        _screen.RisePanelOpen = true;
        for (var i = 0; i < 40; i++)
            _sut.OnFrameTick(0.016f);

        var line = CheckLines().Single(l => l.Contains(".rise "));
        Assert.AreEqual("[Check] id=taom.seat-of-seeing.rise result=pass mission=1 height=7.0", line);
        AssertInFixture("seat_panel_visit_pass.txt", line);
    }
}
