using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.AnimMemory;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// One mission of the clip memory probe: the start line, the one-second sample cadence, the 5 s line
/// with its window aggregates, drops inside and across windows, the 90% boundary, the summary, and the
/// three ways a mission stops early.
/// </summary>
[TestClass]
public class AnimMemorySessionTests
{
    private const string StoppedSummary =
        "[AnimMem] summary: t=+3s samples=1 startKB=10240 endKB=10240 peakKB=10240 peakPct=83 samplesAtOrAbove90Pct=0 drops=0 loadingSamples=0 stopped=1";

    private IAnimClipMemoryProbe _probe = null!;
    private IModLogger _logger = null!;
    private AnimMemorySession _session = null!;

    [TestInitialize]
    public void SetUp()
    {
        _probe = Substitute.For<IAnimClipMemoryProbe>();
        _logger = Substitute.For<IModLogger>();
        _probe.BudgetBytes.Returns(12582912);
        _probe.IsAnyClipLoading().Returns(false);
        _session = new AnimMemorySession(_probe, _logger);
    }

    [TestMethod]
    public void Start_LogsMissionStartLine()
    {
        ArrangeStandardReads();

        _session.Start(0.0);

        _logger.Received(1).LogInfo(
            "[AnimMem] mission start: sample every 1 s, line every 5 s, startKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0");
    }

    [TestMethod]
    public void Tick_BeforeOneSecond_DoesNotSample()
    {
        ArrangeStandardReads();

        _session.Start(0.0);
        _session.Tick(0.5);

        _probe.Received(1).ReadLoadedBytes();
    }

    [TestMethod]
    public void Tick_AtFiveSeconds_LogsPeriodicLine()
    {
        ArrangeStandardReads();

        RunStandard();

        _logger.Received(1).LogInfo(
            "[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=0/6");
    }

    [TestMethod]
    public void Tick_DropAcrossWindowBoundary_CountsInTheNextWindow()
    {
        _probe.ReadLoadedBytes().Returns(10485760, 12582912, 11534336, 11534336, 9437184, 12582912,
            8388608, 8388608, 8388608, 8388608, 8388608);

        _session.Start(0.0);
        for (var t = 1; t <= 10; t++)
            _session.Tick(t);

        _logger.Received(1).LogInfo(
            "[AnimMem] t=+10s loadedKB=8192 budgetKB=12288 pctOfBudget=66 loadingNow=0 drops=1 minKB=8192 maxKB=8192 loadingSamples=0/5");
    }

    [TestMethod]
    public void Tick_LoadingSamples_AreCounted()
    {
        ArrangeStandardReads();
        _probe.IsAnyClipLoading().Returns(false, true, false, true, false, false);

        RunStandard();

        _logger.Received(1).LogInfo(
            "[AnimMem] t=+5s loadedKB=12288 budgetKB=12288 pctOfBudget=100 loadingNow=0 drops=2 minKB=9216 maxKB=12288 loadingSamples=2/6");
    }

    [TestMethod]
    public void End_LogsSummary()
    {
        ArrangeStandardReads();

        RunStandard();
        _session.End(7.0);

        _logger.Received(1).LogInfo(
            "[AnimMem] summary: t=+7s samples=6 startKB=10240 endKB=12288 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=2 loadingSamples=0 stopped=0");
    }

    [TestMethod]
    public void End_WithPartialWindow_LogsTailLineBeforeSummary()
    {
        _probe.ReadLoadedBytes().Returns(10485760, 12582912, 11534336, 11534336, 9437184, 12582912,
            8388608, 10485760);

        RunStandard();
        _session.Tick(6.0);
        _session.Tick(7.0);
        _session.End(7.0);

        Received.InOrder(() =>
        {
            _logger.LogInfo(
                "[AnimMem] t=+7s loadedKB=10240 budgetKB=12288 pctOfBudget=83 loadingNow=0 drops=1 minKB=8192 maxKB=10240 loadingSamples=0/2");
            _logger.LogInfo(
                "[AnimMem] summary: t=+7s samples=8 startKB=10240 endKB=10240 peakKB=12288 peakPct=100 samplesAtOrAbove90Pct=4 drops=3 loadingSamples=0 stopped=0");
        });
    }

    [TestMethod]
    public void End_RightAfterAPeriodicLine_LogsNoTailLine()
    {
        ArrangeStandardReads();

        RunStandard();
        _session.End(5.5);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] t=")));
    }

    [TestMethod]
    public void Start_NegativeFirstRead_LogsNothingAndStops()
    {
        _probe.ReadLoadedBytes().Returns(-1);

        _session.Start(0.0);
        _session.Tick(1.0);
        _session.End(2.0);

        _probe.Received(1).ReadLoadedBytes();
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Sample_At11324621Bytes_CountsAsAtOrAbove90Pct()
    {
        _probe.ReadLoadedBytes().Returns(11324621);

        _session.Start(0.0);
        _session.End(1.0);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] summary: ")
            && s.Contains(" samplesAtOrAbove90Pct=1 ") && s.Contains(" peakPct=90 ")));
    }

    [TestMethod]
    public void Sample_At11324620Bytes_DoesNot()
    {
        _probe.ReadLoadedBytes().Returns(11324620);

        _session.Start(0.0);
        _session.End(1.0);

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("[AnimMem] summary: ")
            && s.Contains(" samplesAtOrAbove90Pct=0 ") && s.Contains(" peakPct=89 ")));
    }

    [TestMethod]
    public void Tick_NegativeRead_StopsWithoutAnError()
    {
        _probe.ReadLoadedBytes().Returns(10485760, -1);

        RunStopping();

        _probe.Received(2).ReadLoadedBytes();
        _probe.Received(1).IsAnyClipLoading();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.Received(1).LogInfo(StoppedSummary);
    }

    [TestMethod]
    public void Tick_ReaderThrows_LogsOneErrorAndStops()
    {
        _probe.ReadLoadedBytes().Returns(x => 10485760, x => throw new InvalidOperationException("boom"));

        RunStopping();

        _logger.Received(1).LogError(Arg.Any<string>());
        _logger.Received(1).LogError("[AnimMem] stopped for this mission after InvalidOperationException: boom");
        _probe.Received(2).ReadLoadedBytes();
        _logger.Received(1).LogInfo(StoppedSummary);
    }

    [TestMethod]
    public void End_WithNoSamples_LogsNothing()
    {
        _probe.ReadLoadedBytes().Returns(x => throw new InvalidOperationException("boom"));

        _session.Start(0.0);
        _session.Tick(1.0);
        _session.End(2.0);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.Received(1).LogError(Arg.Any<string>());
        _probe.Received(1).ReadLoadedBytes();
    }

    private void ArrangeStandardReads() =>
        _probe.ReadLoadedBytes().Returns(10485760, 12582912, 11534336, 11534336, 9437184, 12582912);

    private void RunStandard()
    {
        _session.Start(0.0);
        for (var t = 1; t <= 5; t++)
            _session.Tick(t);
    }

    private void RunStopping()
    {
        _session.Start(0.0);
        _session.Tick(1.0);
        _session.Tick(2.0);
        _session.Tick(3.0);
        _session.End(3.0);
    }
}
