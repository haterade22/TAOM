using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AutoResolveDiagnostics;
using TAOM.Features.AutoResolveDiagnostics.Domain;

namespace TAOM.Tests.Features.AutoResolveDiagnostics;

/// <summary>
/// The write half: record ids, the once-per-session census, and emit policy. Split out of the
/// behavior tests when the behavior was reduced to event wiring (ADR-002).
/// </summary>
[TestClass]
public class AutoResolveLogWriterTests
{
    /// <summary>Overrides the one member that needs a live campaign, so ids are testable.</summary>
    private sealed class TestableWriter : AutoResolveLogWriter
    {
        private readonly int _day;
        public TestableWriter(ITroopCensusAdapter c, IAutoResolveDiagnosticsSettingsProvider s,
                              IModLogger l, int day = 1084) : base(c, s, l) => _day = day;
        protected override int CurrentDay() => _day;
    }

    private ITroopCensusAdapter _census = null!;
    private IAutoResolveDiagnosticsSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private TestableWriter _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _census = Substitute.For<ITroopCensusAdapter>();
        _settings = Substitute.For<IAutoResolveDiagnosticsSettingsProvider>();
        _logger = Substitute.For<IModLogger>();
        _settings.IsEnabled.Returns(true);
        _settings.IsCensusEnabled.Returns(true);
        _sut = new TestableWriter(_census, _settings, _logger);
    }

    // ---- census gating ------------------------------------------------------------------------

    [TestMethod]
    public void ShouldWriteCensus_RequiresBothSwitches()
    {
        // The census is subordinate: the master switch off means no census, whatever the
        // census toggle says. Table-driven so a future inversion of either gate fails loudly.
        Assert.IsTrue(AutoResolveLogWriter.ShouldWriteCensus(true, true));
        Assert.IsFalse(AutoResolveLogWriter.ShouldWriteCensus(true, false));
        Assert.IsFalse(AutoResolveLogWriter.ShouldWriteCensus(false, true),
            "master off must suppress the census even with the census toggle on");
        Assert.IsFalse(AutoResolveLogWriter.ShouldWriteCensus(false, false));
    }

    [TestMethod]
    public void WriteCensus_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);

        _sut.WriteCensus();

        _census.DidNotReceive().Capture();
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void WriteCensus_WhenOnlyTheCensusToggleIsOff_CapturesNothing()
    {
        _settings.IsCensusEnabled.Returns(false);

        _sut.WriteCensus();

        _census.DidNotReceive().Capture();
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void WriteCensus_RunsOnlyOncePerSession()
    {
        _census.Capture().Returns(new[] { new TroopCensusRecord { Id = "gondor_recruit" } });

        _sut.WriteCensus();
        _sut.WriteCensus();

        _census.Received(1).Capture();
    }

    [TestMethod]
    public void WriteCensus_RunsAgain_AfterASecondSessionLaunch()
    {
        // The bug: _censusWritten survived the session boundary, so the census ran once per
        // PROCESS. A player returning to the main menu and starting a second campaign got a log
        // with no engine ground truth at all — silently, and the census is what validates every
        // tier and power figure the offline analysis rests on.
        _census.Capture().Returns(new[] { new TroopCensusRecord { Id = "gondor_recruit" } });

        _sut.WriteCensus();
        _sut.BeginSession();
        _sut.WriteCensus();

        _census.Received(2).Capture();
    }

    [TestMethod]
    public void WriteCensus_WhenTheCensusThrows_DoesNotPropagate()
    {
        _census.Capture().Throws(new System.Exception("engine not ready"));

        _sut.WriteCensus();   // must not throw — this runs during session launch
    }

    [TestMethod]
    public void WriteCensus_WhenTheCensusThrows_StaysRetryable()
    {
        // The latch is set AFTER a successful pass. Setting it first meant one bad session-launch
        // permanently foreclosed the census for the rest of the process.
        //
        // One configuration with a counter, not two: re-arranging mid-test would call
        // _census.Capture() to set up the second behaviour, and that call goes through the
        // still-throwing first configuration.
        int calls = 0;
        _census.Capture().Returns(_ =>
        {
            calls++;
            if (calls == 1)
                throw new System.Exception("engine not ready");
            return new[] { new TroopCensusRecord { Id = "gondor_recruit" } };
        });

        _sut.WriteCensus();     // throws internally, must leave the latch clear
        _sut.WriteCensus();     // therefore retries

        Assert.AreEqual(2, calls, "a failed census must stay retryable");
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("troop census complete: 1")));
    }

    [TestMethod]
    public void WriteCensus_PerRecordLinesAreDebug_SummaryIsDurable()
    {
        // Inverted from Emit on purpose. LogInfo flushes to the OS on the calling thread for every
        // line; the census is ~8,300 lines at session launch, so INFO there would pay thousands of
        // synchronous flushes. A census line is not crash evidence — it is written before any
        // gameplay — but the summary stays durable so there is proof the census ran.
        _census.Capture().Returns(new[]
        {
            new TroopCensusRecord { Id = "gondor_recruit" },
            new TroopCensusRecord { Id = "mordor_orc" },
        });

        _sut.WriteCensus();

        _logger.Received(2).LogDebug(Arg.Is<string>(
            s => s.StartsWith(AutoResolveLogFormatter.CensusTag)));
        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("troop census complete: 2")));
    }

    // ---- emit ---------------------------------------------------------------------------------

    [TestMethod]
    public void Emit_WithNullRecord_LogsNothing()
    {
        _sut.Emit(null);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Emit_WithARecord_WritesExactlyOneTaggedLine()
    {
        _sut.Emit(new BattleLogRecord { Id = "1084.0" });

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith(AutoResolveLogFormatter.Tag)));
    }

    [TestMethod]
    public void Emit_UsesLogInfo_NotLogDebug()
    {
        // LogDebug is drained on a background thread and is LOST on a hard native crash. For a
        // diagnostic whose last line is the evidence, that difference is the whole point.
        _sut.Emit(new BattleLogRecord { Id = "1084.0" });

        _logger.DidNotReceiveWithAnyArgs().LogDebug(default!);
        _logger.ReceivedWithAnyArgs(1).LogInfo(default!);
    }

    // ---- ids ----------------------------------------------------------------------------------

    [TestMethod]
    public void RecordId_IsUniqueAcrossBattlesWithinADay()
    {
        // Two battles sharing an id collapse into one row on any join the analyzer performs.
        // Worst case is several battles on the same campaign day, so hold the day constant.
        var ids = Enumerable.Range(0, 5).Select(_ => _sut.NextRecordId()).ToList();

        CollectionAssert.AllItemsAreUnique(ids);
        Assert.AreEqual("1084.0", ids[0]);
        Assert.AreEqual("1084.4", ids[4]);
    }

    [TestMethod]
    public void RecordId_SequenceRestartsOnANewSession()
    {
        _sut.NextRecordId();
        _sut.NextRecordId();

        _sut.BeginSession();

        Assert.AreEqual("1084.0", _sut.NextRecordId());
    }
}
