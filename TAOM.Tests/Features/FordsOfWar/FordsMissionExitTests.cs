using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsMissionExitTests
{
    private readonly IFordsMenuAdapter _menu = Substitute.For<IFordsMenuAdapter>();
    private readonly IModLogger _logger = Substitute.For<IModLogger>();

    private FordsMissionExit Exit() => new(_menu, _logger);

    [TestMethod]
    public void Tick_AfterTheResultHasBeenUpLongEnough_EndsTheGameOnceWithNoNotice()
    {
        var exit = Exit();

        exit.Tick(20f, failedStart: false, matchEnded: true);
        _menu.DidNotReceive().EndGame();
        exit.Tick(6f, failedStart: false, matchEnded: true);
        exit.Tick(60f, failedStart: false, matchEnded: true);

        _menu.Received(1).EndGame();
        _menu.DidNotReceive().Notify(Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_AFailedStart_EndsTheGameAndShowsTheSameStartFailedNoticeTheSetupPromptShows()
    {
        var exit = Exit();

        exit.Tick(3.1f, failedStart: true, matchEnded: false);

        _menu.Received(1).EndGame();
        _menu.Received(1).Notify(FordsSetupPrompt.StartFailedMessage);
        StringAssert.StartsWith(FordsSetupPrompt.StartFailedMessage, "{=TAOM_FOW_StartFailed}");
    }

    [TestMethod]
    public void Tick_WhileTheMatchRuns_TouchesNothing()
    {
        Exit().Tick(500f, failedStart: false, matchEnded: false);

        _menu.DidNotReceive().EndGame();
        _menu.DidNotReceive().Notify(Arg.Any<string>());
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
        _menu.When(m => m.Notify(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("no inquiry"));
        var exit = Exit();

        exit.Tick(4f, failedStart: true, matchEnded: false);

        _menu.Received(1).EndGame();
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("no inquiry")));
    }
}
