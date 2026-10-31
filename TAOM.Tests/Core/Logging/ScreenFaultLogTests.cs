using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;

namespace TAOM.Tests.Core.Logging;

[TestClass]
public class ScreenFaultLogTests
{
    private IModLogger _logger = null!;
    private ScreenFaultLog _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new ScreenFaultLog(_logger, "Probe");
    }

    [TestMethod]
    public void Run_SwallowsTheThrow_AndLogsOncePerSite()
    {
        for (var i = 0; i < 5; i++)
            _sut.Run("refresh", () => throw new InvalidOperationException("boom"));

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("refresh") && m.Contains("boom")));
    }

    [TestMethod]
    public void TheLogLine_SaysOnlyTheLineIsLatched_NotThatThePartIsSkipped()
    {
        _sut.Run("refresh", () => throw new InvalidOperationException("boom"));

        _logger.Received(1).LogError(Arg.Is<string>(m => !m.Contains("skipped") && m.Contains("later calls still run") && m.Contains("logged once")));
    }

    [TestMethod]
    public void TheTag_FollowsTheConstructor_AndTheLineSaysLoggedOnce()
    {
        new ScreenFaultLog(_logger, "Namegiving").Run("click", () => throw new InvalidOperationException("boom"));

        _logger.Received(1).LogError(Arg.Is<string>(m => m.StartsWith("[Namegiving] click threw.") && m.Contains("This line is logged once and later calls still run.") && !m.Contains("per screen")));
    }

    [TestMethod]
    public void Condition_ReturnsFalseOnAThrow_AndLogsOncePerSite()
    {
        for (var i = 0; i < 3; i++)
            Assert.IsFalse(_sut.Condition("offer", () => throw new InvalidOperationException("boom")));
        Assert.IsFalse(_sut.Condition("tier", () => throw new InvalidOperationException("bang")));

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("offer") && m.Contains("boom")));
        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Condition_PassesTheAnswerThrough()
    {
        Assert.IsTrue(_sut.Condition("a", () => true));
        Assert.IsFalse(_sut.Condition("b", () => false));
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ADifferentSite_StillLogs()
    {
        _sut.Run("refresh", () => throw new InvalidOperationException("a"));
        _sut.Run("click", () => throw new InvalidOperationException("b"));

        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Lines_And_Text_ReturnEmptyOnAThrow()
    {
        Assert.AreEqual(0, _sut.Lines<string>("tip", () => throw new InvalidOperationException()).Count);
        Assert.AreEqual("", _sut.Text("patrol", () => throw new InvalidOperationException()));
        Assert.AreEqual("ok", _sut.Text("patrol2", () => "ok"));
        _logger.Received(2).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void AProblemWithoutAnException_LogsOncePerSiteToo()
    {
        _sut.LogOnce("step", "the reserve moved");
        _sut.LogOnce("step", "the reserve moved");

        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("the reserve moved")));
    }

    // #1071: the ERROR line is latched per screen, so the repeats were never counted; Outcomes.Swallowed counts each throw.
    [TestMethod]
    public void EveryThrow_IsCountedInTheSessionSummary_AndTheErrorLineStaysLatched()
    {
        var outcomeLogger = Substitute.For<IModLogger>();
        OutcomesTests.ResetFacade();
        try
        {
            Outcomes.Initialize(new OutcomeLog(outcomeLogger, new TAOM.Tests.Features.LoadTimeStamps.FakeStampClock(), () => 0));

            for (var i = 0; i < 5; i++)
                _sut.Run("refresh", () => throw new InvalidOperationException("boom"));
            Assert.IsFalse(_sut.Condition("offer", () => throw new NullReferenceException()));
            _sut.LogOnce("step", "the reserve moved");
            Outcomes.WriteSummary("save");

            _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("refresh") && m.Contains("boom")));
            outcomeLogger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains(" swallowed=6 ")));
            outcomeLogger.Received(1).LogInfo("[Session] swallowed feature=Probe site=refresh n=5 first=InvalidOperationException");
            outcomeLogger.Received(1).LogInfo("[Session] swallowed feature=Probe site=offer n=1 first=NullReferenceException");
        }
        finally
        {
            OutcomesTests.ResetFacade();
        }
    }
}
