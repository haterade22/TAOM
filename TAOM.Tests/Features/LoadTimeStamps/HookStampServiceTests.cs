using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.LoadTimeStamps;

[TestClass]
public class HookStampServiceTests
{
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private HookStampService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.LoadTimeStampsEnabled.Returns(true);
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        // The gate writes its "detail on" line to a logger of its own, so the oracles below see
        // only the hook lines.
        _sut = new HookStampService(new LoadStampDetailGate(_settings, new RecordingLogger()), _clock, _logger);
    }

    [TestMethod]
    public void Start_DetailOff_ReturnsNull()
    {
        _settings.LoadTimeStampsEnabled.Returns(false);

        Assert.IsNull(_sut.Start("H", "Campaign"));
        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void Mark_LogsTheTimeSinceThePreviousMark()
    {
        var timer = _sut.Start("H", "Campaign")!;
        _clock.Advance(5);
        timer.Mark("a");
        _clock.Advance(7);
        timer.Mark("b");

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadPhase] hook=H step=a ms=5.00",
            "INFO [LoadPhase] hook=H step=b ms=7.00",
        }, _logger.Lines);
    }

    // A step is the work between two marks. The line of the previous mark is written synchronously and
    // can be slow (a flushed disk write), so its cost is never part of the next step; the total stays
    // the hook's wall clock from its start, writes included.
    [TestMethod]
    public void Mark_TheWriteOfThePreviousLine_IsNotChargedToTheNextStep()
    {
        var timer = _sut.Start("H", "Campaign")!;
        _logger.OnLog = _ => _clock.Advance(100);

        _clock.Advance(5);
        timer.Mark("a");
        _clock.Advance(7);
        timer.Mark("b");
        timer.End();

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadPhase] hook=H step=a ms=5.00",
            "INFO [LoadPhase] hook=H step=b ms=7.00",
            "INFO [LoadPhase] hook=H game=Campaign scope=total ms=212.00 steps=2",
        }, _logger.Lines);
    }

    // The clock read that moves the mark past the write is the one new thing that can fail: the step
    // whose line is out stays counted, the pre-write tick stands as the next step's start (never an
    // older mark, which would charge that step the one before it), and nothing throws.
    [TestMethod]
    public void Mark_TheClockFailingAfterTheLinesWrite_CountsTheStep_KeepsThePreWriteTick_AndNeverThrows()
    {
        var timer = _sut.Start("H", "Campaign")!;
        // Read 1 was the start; read 2 is a's own; read 3 is the one after a's write; read 4 is b's own,
        // read 5 the one after b's write and read 6 the end's.
        _clock.ThrowOnRead = 3;

        _clock.Advance(5);
        timer.Mark("a");
        _clock.Advance(7);
        timer.Mark("b");
        timer.End();

        CollectionAssert.AreEqual(new[]
        {
            "INFO [LoadPhase] hook=H step=a ms=5.00",
            "INFO [LoadPhase] hook=H step=b ms=7.00",
            "INFO [LoadPhase] hook=H game=Campaign scope=total ms=12.00 steps=2",
        }, _logger.Lines);
    }

    [TestMethod]
    public void End_LogsTheTotalSinceStartAndTheStepCount()
    {
        var timer = _sut.Start("H", "Campaign")!;
        _clock.Advance(5);
        timer.Mark("a");
        _clock.Advance(7);
        timer.Mark("b");
        timer.End();

        Assert.AreEqual("INFO [LoadPhase] hook=H game=Campaign scope=total ms=12.00 steps=2", _logger.Lines[2]);
    }

    [TestMethod]
    public void End_Twice_LogsOnce()
    {
        var timer = _sut.Start("H", "Campaign")!;
        timer.End();
        timer.End();

        CollectionAssert.AreEqual(new[] { "INFO [LoadPhase] hook=H game=Campaign scope=total ms=0.00 steps=0" }, _logger.Lines);
    }

    // SubModule.cs calls Mark and End unguarded, ahead of model, behavior and patch registration.
    [TestMethod]
    public void MarkAndEnd_WhenTheLoggerThrows_NeverThrow()
    {
        var timer = _sut.Start("H", "Campaign")!;
        _logger.OnLog = _ => throw new System.InvalidOperationException("disk full");

        timer.Mark("a");
        timer.End();

        Assert.AreEqual(0, _logger.Lines.Count);
    }

    [TestMethod]
    public void End_NullGame_PrintsNone()
    {
        var timer = _sut.Start("GameInitOnce", null)!;
        _clock.Advance(1);
        timer.End();

        CollectionAssert.AreEqual(new[] { "INFO [LoadPhase] hook=GameInitOnce game=none scope=total ms=1.00 steps=0" }, _logger.Lines);
    }
}
