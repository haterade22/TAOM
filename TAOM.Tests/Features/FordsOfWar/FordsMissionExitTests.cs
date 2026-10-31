using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsMissionExitTests
{
    private readonly IFordsMenuAdapter _menu = Substitute.For<IFordsMenuAdapter>();
    private readonly IModLogger _logger = Substitute.For<IModLogger>();
    private readonly FordsRunReport _report;

    public FordsMissionExitTests()
    {
        _report = new FordsRunReport(_logger);
        _report.BeginRun("console", new FordsMatchSetup("s", "gondor", "mordor", "normal", 3));
    }

    private FordsMissionExit Exit() => new(_menu, _logger, _report);

    [TestMethod]
    public void Tick_AfterTheResultHasBeenUpLongEnough_EndsTheGameOnceWithNoNotice()
    {
        var exit = Exit();

        exit.Tick(20f, failedStart: false, matchEnded: true);
        _menu.DidNotReceive().EndGame();
        exit.Tick(6f, failedStart: false, matchEnded: true);
        exit.Tick(60f, failedStart: false, matchEnded: true);

        _menu.Received(1).EndGame();
        _menu.DidNotReceive().Notify(Arg.Any<string>(), Arg.Any<Action?>());
    }

    [TestMethod]
    public void Tick_AFailedStart_EndsTheGameAndShowsTheSameStartFailedNoticeTheSetupPromptShows()
    {
        var exit = Exit();

        exit.Tick(3.1f, failedStart: true, matchEnded: false);

        _menu.Received(1).EndGame();
        _menu.Received(1).Notify(FordsSetupPrompt.StartFailedMessage, Arg.Any<Action?>());
        StringAssert.StartsWith(FordsSetupPrompt.StartFailedMessage, "{=TAOM_FOW_StartFailed}");
    }

    [TestMethod]
    public void Tick_WhileTheMatchRuns_TouchesNothing()
    {
        Exit().Tick(500f, failedStart: false, matchEnded: false);

        _menu.DidNotReceive().EndGame();
        _menu.DidNotReceive().Notify(Arg.Any<string>(), Arg.Any<Action?>());
    }

    [TestMethod]
    public void Tick_WhenTheEngineThrows_LogsAWarningAndDoesNotThrow()
    {
        _menu.When(m => m.EndGame()).Do(_ => throw new InvalidOperationException("no game"));
        var exit = Exit();

        exit.Tick(30f, failedStart: false, matchEnded: true);

        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no game")));
    }

    [TestMethod]
    public void Tick_WhenTheNoticeThrows_StillHasEndedTheGame()
    {
        _menu.When(m => m.Notify(Arg.Any<string>(), Arg.Any<Action?>())).Do(_ => throw new InvalidOperationException("no inquiry"));
        var exit = Exit();

        exit.Tick(4f, failedStart: true, matchEnded: false);

        _menu.Received(1).EndGame();
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no inquiry")));
    }

    [TestMethod]
    public void FailedStart_FirstTick_RecordsRouteAOnce()
    {
        var exit = Exit();

        exit.Tick(0.1f, failedStart: true, matchEnded: false);
        exit.Tick(0.1f, failedStart: true, matchEnded: false);

        Assert.AreEqual("A", _report.Run!.Failure!.Route);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("failure route=A")));
    }

    [TestMethod]
    public void AfterResult_RecordsExitDueAndAutoEndGame()
    {
        var exit = Exit();

        exit.Tick(30f, failedStart: false, matchEnded: true);

        Assert.AreEqual("AfterResult", _report.Run!.End.ExitReason);
        Assert.AreEqual("auto", _report.Run.End.EndGameVia);
        Assert.IsNull(_report.Run.Failure);
    }

    [TestMethod]
    public void EndGameThrows_RecordsNoEndGame()
    {
        _menu.When(m => m.EndGame()).Do(_ => throw new InvalidOperationException("no game"));
        var exit = Exit();

        exit.Tick(30f, failedStart: false, matchEnded: true);

        Assert.AreEqual("AfterResult", _report.Run!.End.ExitReason);
        Assert.IsNull(_report.Run.End.EndGameVia);
    }

    [TestMethod]
    public void FailedStart_ShowsTheNoticeWithAnAckCallback()
    {
        Action? ack = null;
        _menu.When(m => m.Notify(Arg.Any<string>(), Arg.Any<Action?>())).Do(call => ack = call.ArgAt<Action?>(1));
        var exit = Exit();

        exit.Tick(3.1f, failedStart: true, matchEnded: false);

        Assert.IsNotNull(ack, "the notice carries an acknowledge callback");
        Assert.IsTrue(_report.Run!.End.NoticeRequested);
        Assert.IsNull(_report.Run.End.NoticeAckWall);
        ack!();
        Assert.IsNotNull(_report.Run.End.NoticeAckWall);
    }
}
