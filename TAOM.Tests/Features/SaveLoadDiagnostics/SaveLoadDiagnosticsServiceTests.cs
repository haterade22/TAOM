using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Core.Logging;
using TAOM.Features.SaveLoadDiagnostics;
using TAOM.Features.SaveLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

[TestClass]
public class SaveLoadDiagnosticsServiceTests
{
    private IModLogger _logger;
    private SaveLoadDiagnosticsService _sut;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new SaveLoadDiagnosticsService(_logger);
    }

    // ---- lifecycle / emit format ----

    [TestMethod]
    public void BeginLoadAttempt_EmitsLoadRequested_WithNameAndSeq1()
    {
        _sut.BeginLoadAttempt("save007");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("[SaveLoad]") && s.Contains("seq=1") &&
            s.Contains("phase=LoadRequested") && s.Contains("name='save007'")));
    }

    [TestMethod]
    public void BeginSaveAttempt_EmitsSaveBegin_WithName()
    {
        _sut.BeginSaveAttempt("saveauto2");
        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("phase=SaveBegin") && s.Contains("name='saveauto2'")));
    }

    [TestMethod]
    public void LogPhase_SecondCall_IncrementsSeq()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.LogPhase(SaveLoadPhase.LoadDataOk, "x=1");
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("seq=2") && s.Contains("phase=LoadDataOk")));
    }

    [TestMethod]
    public void BeginLoadAttempt_AfterPriorEmits_ResetsSeqTo1()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.LogPhase(SaveLoadPhase.LoadDataOk, string.Empty);
        _logger.ClearReceivedCalls();

        _sut.BeginLoadAttempt("save002");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("seq=1") && s.Contains("name='save002'")));
    }

    [TestMethod]
    public void LogPhase_ContainsElapsedTimestamp()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.LogPhase(SaveLoadPhase.LoadDataOk, string.Empty);
        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("t=+") && s.Contains("ms")));
    }

    // ---- fault logging / exception flattening ----

    [TestMethod]
    public void LogFault_AggregateException_NamesAllInners()
    {
        var agg = new AggregateException(
            new NullReferenceException("null buffer"),
            new InvalidCastException("bad cast"));

        _sut.LogFault(SaveLoadPhase.GraphFault, "kind=object", agg);

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("phase=GraphFault") && s.Contains("kind=object") &&
            s.Contains("NullReferenceException") && s.Contains("InvalidCastException")));
    }

    [TestMethod]
    public void LogFault_TargetInvocationException_UnwrapsToInner()
    {
        var tie = new TargetInvocationException(new InvalidOperationException("real cause"));

        _sut.LogFault(SaveLoadPhase.BehaviorSyncFault, "behavior='X'", tie);

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("InvalidOperationException") && s.Contains("real cause")));
    }

    [TestMethod]
    public void LogFault_ExceptionWithStack_EmitsStackLine()
    {
        Exception thrown;
        try { throw new InvalidOperationException("boom"); }
        catch (Exception ex) { thrown = ex; }

        _sut.LogFault(SaveLoadPhase.SaveWriteFault, "name='saveauto2'", thrown);

        // Compact line + stack line.
        _logger.Received(2).LogError(Arg.Any<string>());
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("stack=")));
    }

    [TestMethod]
    public void LogFault_NullException_LogsDetailOnly()
    {
        _sut.LogFault(SaveLoadPhase.LoadFailed, "dialog shown", null);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("phase=LoadFailed") && s.Contains("dialog shown")));
    }

    [TestMethod]
    public void LogFault_AggregateWrappingTargetInvocation_NamesInnermostCause()
    {
        // The composed production shape: TWParallel wraps worker faults in AggregateException;
        // reflection init callbacks wrap theirs in TargetInvocationException.
        var composed = new AggregateException(
            new TargetInvocationException(new InvalidCastException("the real cause")));

        _sut.LogFault(SaveLoadPhase.GraphFault, "kind=object", composed);

        _logger.Received().LogError(Arg.Is<string>(s =>
            s.Contains("InvalidCastException") && s.Contains("the real cause")));
    }

    [TestMethod]
    public void FaultCount_TracksFaults_AndResetsOnNextAttempt()
    {
        _sut.BeginLoadAttempt("save001");
        Assert.AreEqual(0, _sut.FaultCount);
        _sut.LogFault(SaveLoadPhase.GraphFault, "x", null);
        _sut.LogFault(SaveLoadPhase.GraphFault, "y", null);
        Assert.AreEqual(2, _sut.FaultCount);

        _sut.BeginLoadAttempt("save002");
        Assert.AreEqual(0, _sut.FaultCount);
    }

    [TestMethod]
    public void MarkUnknownSaveId_BeyondCap_StopsLogging()
    {
        _sut.BeginLoadAttempt("save001");
        for (int i = 0; i < 60; i++)
            _sut.MarkUnknownSaveId("object", $"id-{i}");

        // Cap is 50 distinct ids per attempt.
        _logger.Received(50).LogWarning(Arg.Is<string>(s => s.Contains("phase=UnknownSaveId")));
    }

    [TestMethod]
    public void LogFault_BeyondThrottleCap_SuppressesFurtherFaults()
    {
        _sut.BeginLoadAttempt("save001");
        for (int i = 0; i < 25; i++)
            _sut.LogFault(SaveLoadPhase.GraphFault, $"i={i}", null);

        // Cap is 20; null exception => exactly one LogError line per fault.
        _logger.Received(20).LogError(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("cap")));
    }

    [TestMethod]
    public void LogFault_ThrottleResets_OnNextAttempt()
    {
        _sut.BeginLoadAttempt("save001");
        for (int i = 0; i < 25; i++)
            _sut.LogFault(SaveLoadPhase.GraphFault, $"i={i}", null);
        _logger.ClearReceivedCalls();

        _sut.BeginLoadAttempt("save002");
        _sut.LogFault(SaveLoadPhase.GraphFault, "fresh", null);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("fresh")));
    }

    // ---- unknown SaveId dedup ----

    [TestMethod]
    public void MarkUnknownSaveId_SameIdTwice_LogsOnce()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.MarkUnknownSaveId("object", "726900801:101");
        _sut.MarkUnknownSaveId("object", "726900801:101");

        _logger.Received(1).LogWarning(Arg.Is<string>(s =>
            s.Contains("phase=UnknownSaveId") && s.Contains("kind=object") && s.Contains("726900801:101")));
    }

    [TestMethod]
    public void MarkUnknownSaveId_DistinctIds_LogsEach()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.MarkUnknownSaveId("object", "id-A");
        _sut.MarkUnknownSaveId("container", "id-A");
        _sut.MarkUnknownSaveId("object", "id-B");

        _logger.Received(3).LogWarning(Arg.Is<string>(s => s.Contains("phase=UnknownSaveId")));
    }

    [TestMethod]
    public void MarkUnknownSaveId_AfterNextAttempt_LogsAgain()
    {
        _sut.BeginLoadAttempt("save001");
        _sut.MarkUnknownSaveId("object", "id-A");
        _logger.ClearReceivedCalls();

        _sut.BeginLoadAttempt("save001");
        _sut.MarkUnknownSaveId("object", "id-A");

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("id-A")));
    }

    // ---- resilience ----

    [TestMethod]
    public void LogPhase_LoggerThrows_DoesNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Throw(new InvalidOperationException("sink died"));
        _sut.LogPhase(SaveLoadPhase.LoadDataOk, "x");
        // Reaching here without an exception is the assertion.
    }

    [TestMethod]
    public void LogFault_LoggerThrows_DoesNotPropagate()
    {
        _logger.When(l => l.LogError(Arg.Any<string>())).Throw(new InvalidOperationException("sink died"));
        _sut.LogFault(SaveLoadPhase.GraphFault, "x", new Exception("e"));
    }

    [TestMethod]
    public void LogPhase_ParallelCalls_AllSequencesDistinct()
    {
        var lines = new ConcurrentBag<string>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(ci => lines.Add(ci.Arg<string>()));

        _sut.BeginLoadAttempt("save001");
        Parallel.For(0, 100, i => _sut.LogPhase(SaveLoadPhase.GraphFault, $"i={i}"));

        var seqs = lines
            .Select(s => Regex.Match(s, @"seq=(\d+)"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value))
            .ToList();
        Assert.AreEqual(101, seqs.Count);           // Begin + 100 phases
        Assert.AreEqual(101, seqs.Distinct().Count());
    }
}
