using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// The main thread stopped finishing frames, anywhere in the game. A 1 s timer reads the heartbeat
/// and writes [Freeze] evidence at 10/20/40/80 s of silence; a new beat is the only end of an episode.
/// </summary>
[TestClass]
public class MainThreadFreezeWatchdogTests
{
    private static readonly long T0 = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private static long At(double seconds) => T0 + TimeSpan.FromSeconds(seconds).Ticks;

    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private IBattleLoadDiagnosticsService _service = null!;
    private IRglCompileTail _tail = null!;
    private MainThreadHeartbeat _heartbeat = null!;
    private MissionTickStallProbe _missionFrame = null!;
    private MainThreadFreezeWatchdog _sut = null!;
    private List<string> _warnings = null!;
    private List<string> _infos = null!;
    private int _gen0;
    private bool _walk;
    private bool _inRenderWait;
    private IRenderWaitNotice _notice = null!;

    [TestInitialize]
    public void Setup()
    {
        BattleLoadLoadingWindow.Close();
        _logger = Substitute.For<IModLogger>();
        _warnings = new List<string>();
        _infos = new List<string>();
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(c => _warnings.Add(c.Arg<string>()));
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _infos.Add(c.Arg<string>()));
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.MainThreadFreezeSamplerEnabled.Returns(true);
        _service = Substitute.For<IBattleLoadDiagnosticsService>();
        _service.CurrentStatusLine.Returns("phase=WaitingForRender seq=40");
        _tail = Substitute.For<IRglCompileTail>();
        _tail.Available.Returns(true);
        _heartbeat = new MainThreadHeartbeat();
        _heartbeat.BindCurrentThread();
        _missionFrame = new MissionTickStallProbe("mission frame");
        _gen0 = 0;
        _walk = false;
        _inRenderWait = false;
        _notice = Substitute.For<IRenderWaitNotice>();
        _sut = new MainThreadFreezeWatchdog(_logger, _settings, _service, _notice, _heartbeat, _missionFrame, _tail, () => _gen0, () => _walk, () => _inRenderWait)
        {
            DescribeStack = thread => "    at Fake.Frame",
        };
    }

    // A first poll that sees the beat, so the silence starts from a known episode.
    private void BeatAndSeeIt()
    {
        _heartbeat.BeatAt(T0);
        _sut.Poll(At(0.5));
    }

    [TestMethod]
    public void NoBeatYet_LogsNothing()
    {
        _sut.Poll(At(500));

        Assert.AreEqual(0, _warnings.Count);
        Assert.AreEqual(0, _infos.Count);
    }

    [TestMethod]
    public void FreshBeats_LogNothing()
    {
        for (int i = 0; i < 30; i++)
        {
            _heartbeat.BeatAt(At(i));
            _sut.Poll(At(i + 0.5));
        }

        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void SilentNineSeconds_LogsNothing()
    {
        BeatAndSeeIt();

        _sut.Poll(At(9));

        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void SilentMainThread_SamplesWithStacksAtTenTwentyFortyEighty_ThenStops()
    {
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 11d, 20d, 40d, 80d, 160d, 320d }) _sut.Poll(At(s));

        Assert.AreEqual(4, _warnings.Count);
        for (int i = 0; i < 4; i++)
        {
            StringAssert.Contains(_warnings[i], $"sample#{i + 1}");
            StringAssert.Contains(_warnings[i], "    at Fake.Frame");
        }
        StringAssert.Contains(_warnings[0], "[Freeze] main thread silent 10s");
        StringAssert.Contains(_warnings[3], "[Freeze] main thread silent 80s");
    }

    [TestMethod]
    public void NewBeatAfterALoggedEpisode_LogsRecoveredOnce()
    {
        BeatAndSeeIt();
        _sut.Poll(At(10));
        _tail.CompilesSinceFresh.Returns(1569);

        _heartbeat.BeatAt(At(68));
        _sut.Poll(At(68.5));
        _heartbeat.BeatAt(At(69));
        _sut.Poll(At(69.5));

        Assert.AreEqual(1, _infos.Count);
        Assert.AreEqual("[Freeze] main thread recovered after about 68s rglCompiles=1569", _infos[0]);
    }

    [TestMethod]
    public void NewBeatAfterASilentButUnloggedEpisode_LogsNoRecovered()
    {
        BeatAndSeeIt();
        _sut.Poll(At(5));

        _heartbeat.BeatAt(At(6));
        _sut.Poll(At(6.5));

        Assert.AreEqual(0, _infos.Count);
    }

    [TestMethod]
    public void ExitSamplerArmed_LogsNothing()
    {
        _service.ExitWindowOpenedUtcTicks.Returns(T0);
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 20d, 40d, 80d }) _sut.Poll(At(s));

        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void PrecompileWalk_LogsNothing()
    {
        _walk = true;
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 20d, 40d, 80d }) _sut.Poll(At(s));

        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void MissionFrameInFlight_LogsWithoutStack()
    {
        BeatAndSeeIt();
        _missionFrame.EnterAt(T0);

        _sut.Poll(At(10));

        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "context=mission-frame");
        StringAssert.Contains(_warnings[0], FreezeLog.NotSampledMissionFrame);
        Assert.IsFalse(_warnings[0].Contains("Fake.Frame"));
    }

    [TestMethod]
    public void HeapMoving_LogsOneStacklessLineAtForty()
    {
        BeatAndSeeIt();

        _gen0 = 10; _sut.Poll(At(10));
        _gen0 = 20; _sut.Poll(At(20));
        Assert.AreEqual(0, _warnings.Count);

        _gen0 = 40; _sut.Poll(At(40));
        _gen0 = 70; _sut.Poll(At(80));

        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "sample#3");
        StringAssert.Contains(_warnings[0], "the managed heap is moving (20 gen0 collections in 20s)");
        Assert.IsFalse(_warnings[0].Contains("Fake.Frame"));
    }

    [TestMethod]
    public void SettingOff_LogsNothing()
    {
        _settings.MainThreadFreezeSamplerEnabled.Returns(false);
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 20d, 40d, 80d }) _sut.Poll(At(s));

        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void CaptureThrows_LineSaysCaptureFailed()
    {
        _sut.DescribeStack = _ => throw new InvalidOperationException("boom");
        BeatAndSeeIt();

        _sut.Poll(At(10));

        StringAssert.Contains(_warnings.Single(), "    <capture failed: InvalidOperationException: boom>");
    }

    [TestMethod]
    public void EmptyStack_LineSaysNoManagedFrames()
    {
        var empty = new StackTrace(100000, false);
        Assert.AreEqual(0, empty.FrameCount, "the test needs a stack with no frames");

        Assert.AreEqual(FreezeLog.NoManagedFrames, MainThreadFreezeWatchdog.FormatStack(empty));
    }

    [TestMethod]
    public void RenderWaitSilence_PublishesTheDecisionEveryPoll()
    {
        _inRenderWait = true;
        _tail.CompilesSinceFresh.Returns(40);
        _tail.SecondsSinceLastCompile(Arg.Any<long>()).Returns(1d);
        BeatAndSeeIt();

        _sut.Poll(At(6));
        _sut.Poll(At(7));

        _notice.Received(2).Show(Arg.Is<RenderWaitNoticeDecision>(d => d.Kind == RenderWaitNoticeKind.Compiling && d.Compiles == 40));
    }

    [TestMethod]
    public void NewBeat_PublishesHidden()
    {
        BeatAndSeeIt();

        _notice.Received(1).Show(Arg.Is<RenderWaitNoticeDecision>(d => d.Kind == RenderWaitNoticeKind.Hidden));
    }

    [TestMethod]
    public void FreezeDumpEnabled_WritesOnceAtEightySeconds()
    {
        _settings.FreezeDumpEnabled.Returns(true);
        int writes = 0;
        _sut.WriteDump = (string directory, out string pathOrError) =>
        {
            writes++;
            pathOrError = @"C:\nowhere\taom_freeze.dmp";
            return true;
        };
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 20d, 40d, 80d, 160d }) _sut.Poll(At(s));
        _heartbeat.BeatAt(At(200));
        _sut.Poll(At(200.5));
        _heartbeat.BeatAt(At(201));
        _sut.Poll(At(201.5));
        foreach (double s in new[] { 211d, 221d, 241d, 281d }) _sut.Poll(At(s));

        Assert.AreEqual(1, writes, "one dump per process");
        Assert.IsTrue(_infos.Any(l => l.StartsWith(@"[Freeze] memory dump written: C:\nowhere\taom_freeze.dmp (")), string.Join("|", _infos));
    }

    [TestMethod]
    public void FreezeDumpOff_WritesNothing()
    {
        int writes = 0;
        _sut.WriteDump = (string directory, out string pathOrError) =>
        {
            writes++;
            pathOrError = "";
            return true;
        };
        BeatAndSeeIt();

        foreach (double s in new[] { 10d, 20d, 40d, 80d }) _sut.Poll(At(s));

        Assert.AreEqual(0, writes);
    }

    [TestMethod]
    public void RglCompilesSinceLastFrame_AppearInTheLine()
    {
        _tail.CompilesSinceFresh.Returns(31);
        _tail.SecondsSinceLastCompile(Arg.Any<long>()).Returns(2d);
        BeatAndSeeIt();

        _sut.Poll(At(10));

        StringAssert.Contains(_warnings.Single(), "rglCompiles=31 lastCompileAgoS=2");
        _tail.Received().ReadNew(At(10));
    }
}
