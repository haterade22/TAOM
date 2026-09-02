using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ClearExtensions;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

// [MemStation] screen-transition memory anchors (#386 follow-up).
//
// Why this exists: a measured session went 10,646 -> 19,032 MB privMB across 37 minutes with ZERO
// missions and nobody typing anything. The battle lifecycle is already anchored on 8 phase lines
// and showed stable per-mission baselines, so the growth is on the map/UI path, which had no
// anchors at all — only the 30s periodic [MemSample] trace, which cannot say WHICH screen.
//
// The fixture screen name is deliberately GauntletInventoryScreen: a type that ACTUALLY
// EXISTS in v1.4.8 and is actually covered by these events. The first cut of this file pinned
// "GauntletEncyclopediaScreen", which exists nowhere in the engine - the encyclopedia is a
// MapView overlay on MapScreen, never pushed through ScreenManager. The tests still passed
// because they only exercise string formatting, so a fake name in a fixture reads as a
// coverage claim nothing checks. Keep fixture names to real, covered screens.
//
// The pinned literals here are the C# half of a cross-language contract with
// tools/triage_battle_load.py (tests: tools/tests/test_triage_battle_load.py). Change one, change
// all four: this literal, the Python PINNED_MEM_STATION_* constant, and both parse tests.
[TestClass]
public class MemoryStationSamplerTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private MemoryStationSampler _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.MemorySamplerEnabled.Returns(true);
        _settings.IsEnabled.Returns(true);
        _sut = new MemoryStationSampler(_logger, _settings);
    }

    // Same sample values MemoryPressureSamplerTests pins, so one number set covers both tags.
    private static MemorySample HealthySample() => new MemorySample(
        privMb: 4211, wsMb: 3900, heapMb: 654,
        sysCommitUsedMb: 14003, sysCommitLimitMb: 31646,
        availPhysMb: 6200, totalPhysMb: 16296, memLoadPercent: 61);

    private static MemorySample LowHeadroomSample() => new MemorySample(
        privMb: 4211, wsMb: 3900, heapMb: 654,
        sysCommitUsedMb: 29847, sysCommitLimitMb: 31646,
        availPhysMb: 310, totalPhysMb: 16296, memLoadPercent: 97);

    // ---- Pinned format literals (cross-language contract) ----------------------------------

    [TestMethod]
    public void FormatStation_EnterWithKnownValues_MatchesPinnedLiteral()
        => Assert.AreEqual(
            "[MemStation] enter screen='GauntletInventoryScreen' privMB=4211 wsMB=3900 heapMB=654 "
            + "sysCommitUsedMB=14003 sysCommitLimitMB=31646 availPhysMB=6200 memLoad=61%",
            MemoryStationSampler.FormatStation("enter", "GauntletInventoryScreen", HealthySample()));

    [TestMethod]
    public void FormatStation_ExitWithKnownValues_MatchesPinnedLiteral()
        => Assert.AreEqual(
            "[MemStation] exit screen='GauntletInventoryScreen' privMB=4211 wsMB=3900 heapMB=654 "
            + "sysCommitUsedMB=29847 sysCommitLimitMB=31646 availPhysMB=310 memLoad=97%",
            MemoryStationSampler.FormatStation("exit", "GauntletInventoryScreen", LowHeadroomSample()));

    // One vocabulary across [MemSample] and [MemStation]: a single `grep privMB` over a session
    // log must hit the periodic trend and the station anchors alike.
    [TestMethod]
    public void FormatStation_TokenTail_IsByteIdenticalToMemoryPressureSamplerFormatSampleTokens()
    {
        var sample = HealthySample();

        var line = MemoryStationSampler.FormatStation("enter", "S", sample);

        StringAssert.EndsWith(line, MemoryPressureSampler.FormatSampleTokens(sample));
    }

    // ---- Screen-name sanitisation (log-forgery guard) ---------------------------------------

    [TestMethod]
    public void SanitizeScreenName_NullOrEmpty_ReturnsUnknownMarker()
    {
        Assert.AreEqual("<unknown>", MemoryStationSampler.SanitizeScreenName(null));
        Assert.AreEqual("<unknown>", MemoryStationSampler.SanitizeScreenName(""));
        Assert.AreEqual("<unknown>", MemoryStationSampler.SanitizeScreenName("   "));
    }

    // A quote would close the screen='...' token and a newline would let a screen name forge a
    // whole log line into the file triage_battle_load.py parses. Same guard as
    // MemoryProbeReportFormatter's station-label validator.
    [TestMethod]
    public void SanitizeScreenName_NameWithQuoteBracketOrNewline_StripsForgeryCharacters()
    {
        var cleaned = MemoryStationSampler.SanitizeScreenName("Ga'unt\nlet[Screen]`1");

        Assert.IsFalse(cleaned.Contains("'"), cleaned);
        Assert.IsFalse(cleaned.Contains("\n"), cleaned);
        Assert.IsFalse(cleaned.Contains("["), cleaned);
        Assert.IsFalse(cleaned.Contains("`"), cleaned);
        StringAssert.StartsWith(cleaned, "Ga_unt_let_Screen");
    }

    [TestMethod]
    public void SanitizeScreenName_LongName_TruncatesToCap()
        => Assert.AreEqual(
            MemoryStationSampler.MaxScreenNameLength,
            MemoryStationSampler.SanitizeScreenName(new string('A', 300)).Length);

    [TestMethod]
    public void SanitizeScreenName_OrdinaryTypeName_PassesThroughUnchanged()
        => Assert.AreEqual(
            "GauntletInventoryScreen",
            MemoryStationSampler.SanitizeScreenName("GauntletInventoryScreen"));

    // ---- Cap ------------------------------------------------------------------------------

    [TestMethod]
    public void ShouldEmit_UnderCap_ReturnsTrue()
        => Assert.IsTrue(MemoryStationSampler.ShouldEmit(emitted: 1999, cap: 2000));

    [TestMethod]
    public void ShouldEmit_AtCap_ReturnsFalse()
        => Assert.IsFalse(MemoryStationSampler.ShouldEmit(emitted: 2000, cap: 2000));

    // Silence must never read as a clean result — the TableauDiagnostics "census is FULL" wording.
    [TestMethod]
    public void FormatCapReached_StatesThatSilenceIsNotACleanResult()
    {
        var line = MemoryStationSampler.FormatCapReached(2000);

        StringAssert.Contains(line, "NOT measured");
        StringAssert.Contains(line, "not 'no growth'");
    }

    [TestMethod]
    public void NoteStation_PastCap_EmitsCapLineOnceThenGoesSilent()
    {
        var sut = new MemoryStationSampler(_logger, _settings, cap: 2);

        sut.NoteStation("enter", "A");
        sut.NoteStation("exit", "A");
        sut.NoteStation("enter", "B"); // trips the cap
        sut.NoteStation("exit", "B");
        sut.NoteStation("enter", "C");

        _logger.Received(2).LogInfo(Arg.Is<string>(m => m.StartsWith("[MemStation] ")));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("NOT measured")));
    }

    // ---- Gating ---------------------------------------------------------------------------

    [TestMethod]
    public void NoteStation_SamplerDisabled_LogsNothing()
    {
        _settings.MemorySamplerEnabled.Returns(false);

        _sut.NoteStation("enter", "GauntletInventoryScreen");

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // Mirrors MemoryPressureSampler's contract: the master toggle governs battle-load PHASE
    // logging, not session-wide memory forensics. Turning off phase logging must not kill this.
    [TestMethod]
    public void NoteStation_MasterToggleOff_StillLogs()
    {
        _settings.IsEnabled.Returns(false);
        _settings.MemorySamplerEnabled.Returns(true);

        _sut.NoteStation("enter", "GauntletInventoryScreen");

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.StartsWith("[MemStation] enter")));
    }

    // THE critical test. ScreenManager.OnPushScreen is a plain multicast delegate, so a throw
    // from our handler skips every later subscriber AND propagates into ScreenManager.PushScreen
    // itself — a hard crash on every screen open, for every mod in the process.
    [TestMethod]
    public void NoteStation_ThrowingLogger_DoesNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        _sut.NoteStation("enter", "GauntletInventoryScreen");

        // Not-throwing is the primary guarantee, but asserting only that would still pass if the
        // catch block were reduced to a bare `catch { }` — silently losing the diagnostic too.
        _logger.Received(1).LogWarning(Arg.Is<string>(m =>
            m.Contains("station failed") && m.Contains("InvalidOperationException")));
    }

    // DEBUG rides an async writer and a hard crash drops whatever is queued. A forensic anchor
    // that does not survive the crash it exists to explain is worthless.
    [TestMethod]
    public void NoteStation_EmitsAtInfoLevelNotDebug()
    {
        _sut.NoteStation("enter", "GauntletInventoryScreen");

        _logger.Received(1).LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
    }

    [TestMethod]
    public void NoteStation_SanitisesTheScreenNameBeforeLogging()
    {
        _sut.NoteStation("enter", "Bad'Name");

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("screen='Bad_Name'")));
    }

    // ---- Subscription lifecycle ------------------------------------------------------------

    // OnBeforeInitialModuleScreenSetAsRoot re-fires on EVERY return to the main menu, so a
    // non-idempotent Start would add a second handler per menu visit and double every line.
    [TestMethod]
    public void Start_CalledTwice_SubscribesOnlyOnce()
    {
        _sut.Start();
        _sut.Start();

        Assert.AreEqual(1, _sut.SubscribeCount);
        _sut.Dispose(); // static engine event: never leak a handler out of a test
    }

    [TestMethod]
    public void Dispose_AfterStart_UnsubscribesSoStartCanResubscribe()
    {
        _sut.Start();
        _sut.Dispose();
        _sut.Start();

        Assert.AreEqual(2, _sut.SubscribeCount);
    }

    [TestMethod]
    public void Dispose_WithoutStart_DoesNotThrow()
        => _sut.Dispose();

    // The cap is documented as per-SESSION. Start() re-fires on every return to the main menu,
    // which is the only session boundary this class sees, so the budget has to clear there or
    // the cap is really per-PROCESS: a second campaign would inherit an exhausted budget, log
    // nothing at all, and its one cap-reached warning would be sitting in a previous
    // campaign's log. That is a defect class this repo has shipped before.
    // These must call Start() BEFORE spending the budget, or the second Start() is really the
    // FIRST one and takes the subscribe path instead of the `if (_started) return;` early-return
    // the reset is supposed to sit in front of. The first cut of these tests made exactly that
    // mistake: they passed while proving nothing about the path they are named for.
    // Each disposes, because Start() subscribes to a STATIC engine event and a leaked handler
    // would outlive the test and fire during unrelated ones.
    [TestMethod]
    public void Start_CalledAgainAfterCapReached_ResetsTheSessionBudget()
    {
        using var sut = new MemoryStationSampler(_logger, _settings, cap: 1);
        sut.Start();                             // real first start: subscribes
        sut.NoteStation("enter", "MapScreen");   // spends the budget
        sut.NoteStation("exit", "MapScreen");    // trips the cap
        _logger.ClearReceivedCalls();

        sut.Start();                             // a return to the main menu: early-return + reset

        Assert.AreEqual(1, sut.SubscribeCount, "the second Start must NOT resubscribe");
        sut.NoteStation("enter", "MapScreen");
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.StartsWith("[MemStation] enter")));
    }

    [TestMethod]
    public void Start_ResetsTheCapWarningLatchSoASecondSessionCanWarnAgain()
    {
        using var sut = new MemoryStationSampler(_logger, _settings, cap: 1);
        sut.Start();
        sut.NoteStation("enter", "MapScreen");
        sut.NoteStation("exit", "MapScreen");    // warns once
        sut.Start();                             // early-return + reset
        _logger.ClearReceivedCalls();

        sut.NoteStation("enter", "MapScreen");   // spends the fresh budget
        sut.NoteStation("exit", "MapScreen");    // must warn again for THIS session

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("NOT measured")));
    }

    // The reset is a discontinuity in the artefact being analysed, so it must be visible in it.
    [TestMethod]
    public void Start_AfterAPreviousSession_EmitsADurableResetMarker()
    {
        using var sut = new MemoryStationSampler(_logger, _settings, cap: 4);
        sut.Start();
        sut.NoteStation("enter", "MapScreen");
        _logger.ClearReceivedCalls();

        sut.Start();

        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.StartsWith("[MemStation] session-reset")));
    }

    [TestMethod]
    public void Start_FirstEverCall_DoesNotEmitAResetMarker()
    {
        using var sut = new MemoryStationSampler(_logger, _settings, cap: 4);

        sut.Start();

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(m => m.Contains("session-reset")));
    }

    // A 64-char name is exactly at the cap and must survive intact, not lose its last char.
    [TestMethod]
    public void SanitizeScreenName_ExactlyAtCap_PassesThroughUnchanged()
    {
        var name = new string('A', MemoryStationSampler.MaxScreenNameLength);

        Assert.AreEqual(name, MemoryStationSampler.SanitizeScreenName(name));
    }
}
