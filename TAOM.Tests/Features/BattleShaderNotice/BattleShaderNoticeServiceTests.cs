using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BattleShaderNotice;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShaderCompileNotice;

namespace TAOM.Tests.Features.BattleShaderNotice;

[TestClass]
public class BattleShaderNoticeServiceTests
{
    private IBattleShaderNoticeRunner _runner = null!;
    private IBattleShaderNoticeTextSource _textSource = null!;
    private IDedicatedServerProvider _server = null!;
    private IModLogger _logger = null!;
    private IShaderCompileQueueAdapter _queue = null!;
    private BattleShaderNoticeTexts _texts = null!;

    [TestInitialize]
    public void SetUp()
    {
        _runner = Substitute.For<IBattleShaderNoticeRunner>();
        _textSource = Substitute.For<IBattleShaderNoticeTextSource>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _queue = Substitute.For<IShaderCompileQueueAdapter>();
        _texts = new BattleShaderNoticeTexts(
            new ShaderCompileNoticeTexts("Preparing this scene: %COUNT%", "Preparing this scene: %COUNT%", "detail"),
            "loading line");
        _textSource.Resolve().Returns(_texts);
    }

    private BattleShaderNoticeService NewService() => new(_runner, _textSource, _server, _logger, _queue);

    [TestMethod]
    public void OnScreenTick_CharacterCreationCompilesMany_ShowsTheWindowUntilTheQueueEmpties()
    {
        var service = NewService();
        _queue.QueuedShaders().Returns(640);

        service.OnScreenTick(screenOpen: true);
        _runner.Received(1).Start(_texts.Window);
        Assert.IsNull(service.LoadingScreenLine, "a screen has no loading-screen line");

        _queue.QueuedShaders().Returns(0);
        service.OnScreenTick(screenOpen: true);
        _runner.Received(1).Stop();
    }

    [TestMethod]
    public void OnScreenTick_AFewShaders_ShowsNothing()
    {
        var service = NewService();
        _queue.QueuedShaders().Returns(12);

        service.OnScreenTick(screenOpen: true);

        _runner.DidNotReceiveWithAnyArgs().Start(default!);
    }

    [TestMethod]
    public void OnScreenTick_ScreenClosed_ReadsNoQueueAndClosesAShownWindow()
    {
        var service = NewService();
        service.OnScreenTick(screenOpen: false);
        _queue.DidNotReceive().QueuedShaders();

        _queue.QueuedShaders().Returns(300);
        service.OnScreenTick(screenOpen: true);
        service.OnScreenTick(screenOpen: false);

        _runner.Received(1).Stop();
    }

    [TestMethod]
    public void OnScreenTick_QueueUnreadable_WarnsOnceAndShowsNothing()
    {
        var service = NewService();
        _queue.QueuedShaders().Returns(-1);

        service.OnScreenTick(screenOpen: true);
        service.OnScreenTick(screenOpen: true);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("shader queue")));
        _runner.DidNotReceiveWithAnyArgs().Start(default!);
    }

    [TestMethod]
    public void OnScreenTick_WhileABattleNoticeIsUp_LeavesTheWindowAlone()
    {
        var service = NewService();
        service.OnMissionStateTick(waitingForRender: true, queuedShaders: 1192);
        _queue.QueuedShaders().Returns(0);

        service.OnScreenTick(screenOpen: true);

        _runner.DidNotReceive().Stop();
    }

    [TestMethod]
    public void OnMissionStateTick_ColdLoadStarts_StartsTheWindowWithTheResolvedTexts()
    {
        var service = NewService();

        service.OnMissionStateTick(waitingForRender: true, queuedShaders: 1192);

        _runner.Received(1).Start(_texts.Window);
        Assert.IsTrue(service.IsShowing);
    }

    [TestMethod]
    public void OnMissionStateTick_ColdLoadStarts_SetsTheLoadingScreenLine()
    {
        var service = NewService();

        service.OnMissionStateTick(true, 1192);

        Assert.AreEqual("loading line", service.LoadingScreenLine);
    }

    [TestMethod]
    public void OnMissionStateTick_WarmLoad_ShowsNothing()
    {
        var service = NewService();

        service.OnMissionStateTick(true, 3);

        _runner.DidNotReceiveWithAnyArgs().Start(default!);
        Assert.IsNull(service.LoadingScreenLine);
        Assert.IsFalse(service.IsShowing);
    }

    [TestMethod]
    public void OnMissionStateTick_BattleBecomesPlayable_StopsTheWindowAndClearsTheLine()
    {
        var service = NewService();
        service.OnMissionStateTick(true, 1192);

        service.OnMissionStateTick(false, -1);

        _runner.Received(1).Stop();
        Assert.IsNull(service.LoadingScreenLine);
        Assert.IsFalse(service.IsShowing);
    }

    [TestMethod]
    public void OnMissionStateTick_OrdinaryMissionFrame_NeitherStartsNorStops()
    {
        var service = NewService();

        service.OnMissionStateTick(false, -1);

        _runner.DidNotReceiveWithAnyArgs().Start(default!);
        _runner.DidNotReceive().Stop();
    }

    [TestMethod]
    public void OnMissionStateTick_DedicatedServer_NeverStarts()
    {
        _server.IsDedicatedServer.Returns(true);
        var service = NewService();

        service.OnMissionStateTick(true, 1192);

        _runner.DidNotReceiveWithAnyArgs().Start(default!);
        Assert.IsNull(service.LoadingScreenLine);
        Assert.IsFalse(service.IsShowing);
    }

    [TestMethod]
    public void IsDedicatedServer_ReadsTheProviderOnce_SoTheHookSkipsTheNativeCountOnAServer()
    {
        _server.IsDedicatedServer.Returns(true);
        var service = NewService();

        Assert.IsTrue(service.IsDedicatedServer);
        Assert.IsTrue(service.IsDedicatedServer);
        _ = _server.Received(1).IsDedicatedServer;
    }

    [TestMethod]
    public void OnMissionStateTick_TextsThrow_StartsNothingAndLogsTheTypeOnly()
    {
        _textSource.Resolve().Throws(new InvalidOperationException(@"C:\Users\someone\secret"));
        var service = NewService();

        service.OnMissionStateTick(true, 1192);

        _runner.DidNotReceiveWithAnyArgs().Start(default!);
        Assert.IsNull(service.LoadingScreenLine);
        _logger.Received(1).LogWarning(Arg.Is<string>(m =>
            m.Contains("InvalidOperationException") && !m.Contains("secret")));
    }

    [TestMethod]
    public void LineDuringRenderWait_GivesTheLineOnlyWhileTheEngineStillWaits()
    {
        // The writer passes the engine's render wait; the service gives the stored line only with it.
        var service = NewService();
        service.OnMissionStateTick(true, 1192);

        Assert.AreEqual("loading line", service.LineDuringRenderWait(missionWaiting: true));
        Assert.IsNull(service.LineDuringRenderWait(missionWaiting: false));
    }

    [TestMethod]
    public void LineDuringRenderWait_NoNotice_GivesNothingEvenWhileWaiting()
    {
        Assert.IsNull(NewService().LineDuringRenderWait(missionWaiting: true));
    }

    [TestMethod]
    public void OnMissionStateTick_RunnerThrowsOnStart_KeepsTheLoadingLineAndLogs()
    {
        _runner.When(r => r.Start(Arg.Any<ShaderCompileNoticeTexts>())).Do(_ => throw new OutOfMemoryException());
        var service = NewService();

        service.OnMissionStateTick(true, 1192);

        Assert.AreEqual("loading line", service.LoadingScreenLine);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("OutOfMemoryException")));
    }

    [TestMethod]
    public void OnMissionStateTick_StopAfterAFailedStart_StillClearsTheLine()
    {
        _textSource.Resolve().Throws(new InvalidOperationException());
        var service = NewService();
        service.OnMissionStateTick(true, 1192);

        service.OnMissionStateTick(false, -1);

        Assert.IsNull(service.LoadingScreenLine);
        Assert.IsFalse(service.IsShowing);
    }

    [TestMethod]
    public void OnMissionStateTick_ColdLoadStarts_LogsTheQueuedCount()
    {
        var service = NewService();

        service.OnMissionStateTick(true, 1192);

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.StartsWith("[BattleShaderNotice]") && m.Contains("1192")));
    }
}
