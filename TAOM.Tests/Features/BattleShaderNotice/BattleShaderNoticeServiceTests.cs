using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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
    private BattleShaderNoticeTexts _texts = null!;

    [TestInitialize]
    public void SetUp()
    {
        _runner = Substitute.For<IBattleShaderNoticeRunner>();
        _textSource = Substitute.For<IBattleShaderNoticeTextSource>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _texts = new BattleShaderNoticeTexts(
            new ShaderCompileNoticeTexts("Preparing this battle: %COUNT%", "Preparing this battle: %COUNT%", "detail"),
            "loading line");
        _textSource.Resolve().Returns(_texts);
    }

    private BattleShaderNoticeService NewService() => new(_runner, _textSource, _server, _logger);

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
