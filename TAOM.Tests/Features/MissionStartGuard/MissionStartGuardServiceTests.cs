using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionStartGuard;
using TAOM.Features.MissionStartGuard.Models;

namespace TAOM.Tests.Features.MissionStartGuard;

[TestClass]
public class MissionStartGuardServiceTests
{
    private IMissionStartGuardSettingsProvider _settings = null!;
    private IMissionStartGuardAdapter _adapter = null!;
    private IDedicatedServerProvider _server = null!;
    private IModLogger _logger = null!;
    private MissionStartGuardService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IMissionStartGuardSettingsProvider>();
        _settings.SurviveMissionStartFailures.Returns(true);
        _adapter = Substitute.For<IMissionStartGuardAdapter>();
        _server = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new MissionStartGuardService(_settings, _adapter, _server, _logger);
    }

    private const int Six = MissionStartGuardService.ExpectedSites;

    private static Exception Boom() => new InvalidOperationException("boom in a behaviour");

    private List<string> Errors() =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogError))
            .Select(c => (string)c.GetArguments()[0]!).ToList();

    private List<string> Infos() =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogInfo))
            .Select(c => (string)c.GetArguments()[0]!).ToList();

    private List<string> Warnings() =>
        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IModLogger.LogWarning))
            .Select(c => (string)c.GetArguments()[0]!).ToList();

    // ---- ShouldSurvive ----

    [TestMethod]
    public void ShouldSurvive_ToggleOn_ReturnsTrue()
    {
        Assert.IsTrue(_sut.ShouldSurvive(Boom()));
    }

    [TestMethod]
    public void ShouldSurvive_ToggleOff_ReturnsFalse()
    {
        _settings.SurviveMissionStartFailures.Returns(false);

        Assert.IsFalse(_sut.ShouldSurvive(Boom()));
    }

    [TestMethod]
    public void ShouldSurvive_OutOfMemory_ReturnsFalseWithTheToggleOn()
    {
        Assert.IsFalse(_sut.ShouldSurvive(new OutOfMemoryException()));
    }

    [TestMethod]
    public void ShouldSurvive_SettingsReadThrows_ReturnsTrue()
    {
        // A broken MCM read must not re-open the reload loop the guard exists to close.
        _settings.SurviveMissionStartFailures.Throws(new InvalidOperationException("MCM not ready"));

        Assert.IsTrue(_sut.ShouldSurvive(Boom()));
    }

    [TestMethod]
    public void ShouldSurvive_NullException_ReturnsFalse()
    {
        Assert.IsFalse(_sut.ShouldSurvive(null!));
    }

    // ---- Report: the log line ----

    [TestMethod]
    public void Report_ACaughtThrow_LogsOneErrorWithTagCallOwnerAssemblyAndTheFullException()
    {
        var ex = Boom();

        _sut.Report(StartCall.EarlyStart, "Some.Mod.BrokenBehavior", "Some.Mod", ex);

        var errors = Errors();
        Assert.AreEqual(1, errors.Count);
        StringAssert.StartsWith(errors[0], "[MissionStartGuard]");
        StringAssert.Contains(errors[0], "EarlyStart");
        StringAssert.Contains(errors[0], "Some.Mod.BrokenBehavior");
        StringAssert.Contains(errors[0], "Some.Mod");
        StringAssert.Contains(errors[0], ex.ToString());
    }

    [TestMethod]
    public void Report_MoreThanTenInOneMission_LogsTenFullLinesAndOneSuppressedNote()
    {
        for (var i = 0; i < 25; i++)
            _sut.Report(StartCall.AfterStart, "Some.Mod.B" + i, "Some.Mod", Boom());

        var errors = Errors();
        Assert.AreEqual(10, errors.Count(e => e.Contains("threw in")), "ten full ERROR lines");
        Assert.AreEqual(1, Warnings().Count(w => w.Contains("not logged")), "one note that the rest are counted, not logged");
    }

    [TestMethod]
    public void Report_TheLoggerThrows_DoesNotThrow()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log closed"));

        _sut.Report(StartCall.AfterStart, "T", "A", Boom());
    }

    // ---- Report: the on-screen message ----

    [TestMethod]
    public void Report_ACaughtThrow_ShowsOneNoticeNamingModuleCallAndExceptionType()
    {
        _sut.Report(StartCall.OnMissionBehaviorInitialize, "Some.Mod.Sub", "Some.Mod", Boom());

        _adapter.Received(1).ShowNotice("Some.Mod", "OnMissionBehaviorInitialize", nameof(InvalidOperationException));
    }

    [TestMethod]
    public void Report_FourThrowsInOneMission_ShowsOnlyThreeNotices()
    {
        for (var i = 0; i < 4; i++)
            _sut.Report(StartCall.AfterStart, "T" + i, "A", Boom());

        _adapter.Received(3).ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BeginMission_ANewMission_ResetsTheNoticeBudget()
    {
        for (var i = 0; i < 4; i++)
            _sut.Report(StartCall.AfterStart, "T", "A", Boom());
        _adapter.ClearReceivedCalls();

        _sut.BeginMission(new object());
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        _adapter.Received(1).ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BeginMission_TheSameMissionLoadedAgain_KeepsItsBudget()
    {
        // AfterStart that escapes is run again for the same Mission object: the budget must not refill each frame.
        var mission = new object();
        _sut.BeginMission(mission);
        for (var i = 0; i < 3; i++)
            _sut.Report(StartCall.AfterStart, "T", "A", Boom());
        _adapter.ClearReceivedCalls();

        _sut.BeginMission(mission);
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        _adapter.DidNotReceive().ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void BeginMission_ANullToken_AlwaysResets()
    {
        _sut.BeginMission(null);
        for (var i = 0; i < 3; i++)
            _sut.Report(StartCall.AfterStart, "T", "A", Boom());
        _adapter.ClearReceivedCalls();

        _sut.BeginMission(null);
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        _adapter.Received(1).ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Report_DedicatedServer_ShowsNoNoticeButStillLogs()
    {
        _server.IsDedicatedServer.Returns(true);

        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        _adapter.DidNotReceive().ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
        Assert.AreEqual(1, Errors().Count);
    }

    [TestMethod]
    public void Report_TheAdapterThrows_DoesNotThrowAndTheErrorIsStillLogged()
    {
        _adapter.When(a => a.ShowNotice(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("no message log yet"));

        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        Assert.AreEqual(1, Errors().Count);
    }

    // ---- EndMissionStart: the per-mission summary ----

    [TestMethod]
    public void EndMissionStart_NothingCaught_WritesNothing()
    {
        _sut.BeginMission(new object());

        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void EndMissionStart_SomethingCaught_WritesOneSummaryWithTheCount()
    {
        _sut.BeginMission(new object());
        _sut.Report(StartCall.EarlyStart, "T", "A", Boom());
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());

        _sut.EndMissionStart(true, Six);

        var summary = Warnings().Where(w => w.Contains("survived")).ToList();
        Assert.AreEqual(1, summary.Count);
        StringAssert.StartsWith(summary[0], "[MissionStartGuard]");
        StringAssert.Contains(summary[0], "2");
    }

    [TestMethod]
    public void EndMissionStart_CalledTwiceForTheSameMission_WritesTheSummaryOnce()
    {
        var mission = new object();
        _sut.BeginMission(mission);
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());
        _sut.EndMissionStart(true, Six);

        _sut.BeginMission(mission);
        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(1, Warnings().Count(w => w.Contains("survived")));
    }

    [TestMethod]
    public void EndMissionStart_ANewMissionThatCaughtNothing_WritesNoSummary()
    {
        _sut.BeginMission(new object());
        _sut.Report(StartCall.AfterStart, "T", "A", Boom());
        _sut.EndMissionStart(true, Six);
        _logger.ClearReceivedCalls();

        _sut.BeginMission(new object());
        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    // ---- EndMissionStart: the lost-guard canary ----

    [TestMethod]
    public void EndMissionStart_ThePrefixDidNotRun_WarnsOnceThatItDidNot()
    {
        _sut.EndMissionStart(false, Six);

        var warnings = Warnings();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.StartsWith(warnings[0], "[MissionStartGuard]");
        StringAssert.Contains(warnings[0], "prefix");
    }

    [TestMethod]
    public void EndMissionStart_ALiveSwapCountOfFive_WarnsOnceNamingTheCount()
    {
        _sut.BeginMission(new object());

        _sut.EndMissionStart(true, 5);

        var warnings = Warnings();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.StartsWith(warnings[0], "[MissionStartGuard]");
        StringAssert.Contains(warnings[0], "5 of 6");
    }

    [TestMethod]
    public void EndMissionStart_BeganAndSixSwapsLive_IsSilent()
    {
        _sut.BeginMission(new object());

        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void EndMissionStart_ASecondFailureInTheSameProcess_WritesNothingMore()
    {
        _sut.EndMissionStart(false, Six);
        _logger.ClearReceivedCalls();

        _sut.EndMissionStart(false, Six);
        _sut.BeginMission(new object());
        _sut.EndMissionStart(true, 2);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "one warning per process, whichever way the guard was lost");
    }

    [TestMethod]
    public void EndMissionStart_AWarningThenAHealthyMission_StaysSilentAboutTheGuard()
    {
        _sut.BeginMission(new object());
        _sut.EndMissionStart(true, 5);
        _logger.ClearReceivedCalls();

        _sut.BeginMission(new object());
        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count());
    }

    [TestMethod]
    public void EndMissionStart_TheFlagIsPerCall_ARerunOfTheSameCallNeverTurnsIntoAMissingPrefix()
    {
        // Harmony reruns the finalizers for one call when a later one throws; the call's __state is the same both times.
        _sut.BeginMission(new object());

        _sut.EndMissionStart(true, Six);
        _sut.EndMissionStart(true, Six);

        Assert.AreEqual(0, Warnings().Count(w => w.Contains("prefix")));
    }

    [TestMethod]
    public void EndMissionStart_InstallAlreadyWarned_WritesNoSecondGuardWarning()
    {
        _sut.LogInstall(0);
        _logger.ClearReceivedCalls();

        _sut.EndMissionStart(true, 0);

        Assert.AreEqual(0, _logger.ReceivedCalls().Count(), "the install warning already said the guard is off");
    }

    [TestMethod]
    public void EndMissionStart_TheLoggerThrows_DoesNotThrow()
    {
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log closed"));

        _sut.EndMissionStart(false, Six);
    }

    // ---- ReportEscaped: the observe-only finalizer ----

    [TestMethod]
    public void ReportEscaped_AnEscapedThrow_LogsAnErrorWithTheExceptionAndTheLoadAgainNote()
    {
        var ex = Boom();

        _sut.ReportEscaped(ex);

        var errors = Errors();
        Assert.AreEqual(1, errors.Count);
        StringAssert.StartsWith(errors[0], "[MissionStartGuard]");
        StringAssert.Contains(errors[0], "load the mission again");
        StringAssert.Contains(errors[0], ex.ToString());
    }

    [TestMethod]
    public void ReportEscaped_FiveThrows_LogsOnlyTheFirstThreePerProcess()
    {
        for (var i = 0; i < 5; i++)
            _sut.ReportEscaped(Boom());
        _sut.BeginMission(new object());
        _sut.ReportEscaped(Boom());

        Assert.AreEqual(3, Errors().Count);
    }

    [TestMethod]
    public void ReportEscaped_TheLoggerThrows_DoesNotThrow()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log closed"));

        _sut.ReportEscaped(Boom());
    }

    // ---- LogInstall: the configuration line ----

    [TestMethod]
    public void LogInstall_AllSitesWrapped_LogsTheOnLineAtInfo()
    {
        _sut.LogInstall(MissionStartGuardService.ExpectedSites);

        var infos = Infos();
        Assert.AreEqual(1, infos.Count);
        StringAssert.StartsWith(infos[0], "[MissionStartGuard] ON: 6 call sites wrapped in Mission.AfterStart");
        Assert.AreEqual(0, Warnings().Count);
    }

    [TestMethod]
    public void LogInstall_NoSitesWrapped_LogsOneWarningNamingTheCountAndNothingElse()
    {
        _sut.LogInstall(0);

        var warnings = Warnings();
        Assert.AreEqual(1, warnings.Count);
        StringAssert.StartsWith(warnings[0], "[MissionStartGuard]");
        StringAssert.Contains(warnings[0], "0 of 6");
        StringAssert.Contains(warnings[0], "vanilla");
        Assert.AreEqual(0, Infos().Count);
    }

    [TestMethod]
    public void LogInstall_ToggleOff_SaysSoOnTheOnLine()
    {
        _settings.SurviveMissionStartFailures.Returns(false);

        _sut.LogInstall(MissionStartGuardService.ExpectedSites);

        StringAssert.Contains(Infos().Single(), "toggle off");
    }

    [TestMethod]
    public void LogInstall_TheLoggerThrows_DoesNotThrow()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log closed"));

        _sut.LogInstall(6);
    }
}
