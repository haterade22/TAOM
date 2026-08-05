using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Pure decision + format seams of the session-wide memory-pressure sampler (#386), plus
/// PollOnce behavior via mocks. The timer plumbing is game-only per the watchdog precedent.
/// The format literals are the cross-lane contract — the Python triage twin is
/// tools/tests/test_triage_battle_load.py.
/// </summary>
[TestClass]
public class MemoryPressureSamplerTests
{
    private IModLogger _logger;
    private IBattleLoadDiagnosticsSettingsProvider _settings;
    private MemoryPressureSampler _sut;

    private static readonly DateTime T0 = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _settings.MemorySamplerEnabled.Returns(true);
        _settings.MemorySampleIntervalSeconds.Returns(30d);
        _sut = new MemoryPressureSampler(_logger, _settings);
    }

    // Counts every logged line (any level) containing the token — a low-headroom test host
    // may legitimately emit the WARN line instead of the INFO sample line; both carry privMB=.
    private int CountLoggedLines(string token) =>
        _logger.ReceivedCalls().Count(c =>
            c.GetArguments().Length == 1 && c.GetArguments()[0] is string s && s.Contains(token));

    // ---- ShouldSample --------------------------------------------------------------------

    [TestMethod]
    public void ShouldSample_AtInterval_ReturnsTrue()
        => Assert.IsTrue(MemoryPressureSampler.ShouldSample(30d, 30d));

    [TestMethod]
    public void ShouldSample_BelowInterval_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.ShouldSample(29.9d, 30d));

    // ---- IsLowHeadroom (headroom = limit - used; low if < 2048 floor OR < 10% of limit) --

    [TestMethod]
    public void IsLowHeadroom_BelowFloor_ReturnsTrue()
        // headroom 1384 < the 2048 floor (10% of 16384 = 1638 < floor, so the floor governs)
        => Assert.IsTrue(MemoryPressureSampler.IsLowHeadroom(usedMb: 15000, limitMb: 16384));

    [TestMethod]
    public void IsLowHeadroom_BelowTenPercentAboveFloor_ReturnsTrue()
        // headroom 3460 >= the 2048 floor but < 10% of 40960 = 4096
        => Assert.IsTrue(MemoryPressureSampler.IsLowHeadroom(usedMb: 37500, limitMb: 40960));

    [TestMethod]
    public void IsLowHeadroom_Healthy_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.IsLowHeadroom(usedMb: 14003, limitMb: 31646));

    // Garbage inputs must NOT report low — no WARN computed from garbage.

    [TestMethod]
    public void IsLowHeadroom_ZeroLimit_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.IsLowHeadroom(usedMb: 1000, limitMb: 0));

    [TestMethod]
    public void IsLowHeadroom_NegativeUsed_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.IsLowHeadroom(usedMb: -5, limitMb: 31646));

    // ---- ShouldWarn (one WARN per low episode — latched until rearm) ---------------------

    [TestMethod]
    public void ShouldWarn_LowAndUnlatched_ReturnsTrue()
        => Assert.IsTrue(MemoryPressureSampler.ShouldWarn(usedMb: 15000, limitMb: 16384, latched: false));

    [TestMethod]
    public void ShouldWarn_LowButLatched_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.ShouldWarn(usedMb: 15000, limitMb: 16384, latched: true));

    // ---- ShouldRearm (recovery must clear the threshold by the 512 MB hysteresis) --------

    [TestMethod]
    public void ShouldRearm_RecoveredPastHysteresis_ReturnsTrue()
        // threshold for limit 40960 is max(2048, 4096) = 4096; rearm needs headroom >= 4608
        => Assert.IsTrue(MemoryPressureSampler.ShouldRearm(usedMb: 36352, limitMb: 40960, latched: true));

    [TestMethod]
    public void ShouldRearm_InsideHysteresisBand_ReturnsFalse()
        // headroom 4360: above the 4096 threshold but inside the 512 MB hysteresis band
        => Assert.IsFalse(MemoryPressureSampler.ShouldRearm(usedMb: 36600, limitMb: 40960, latched: true));

    [TestMethod]
    public void ShouldRearm_NotLatched_ReturnsFalse()
        => Assert.IsFalse(MemoryPressureSampler.ShouldRearm(usedMb: 36352, limitMb: 40960, latched: false));

    // ---- Truncation boundary (cross-lane contract) ---------------------------------------
    // The percent threshold uses INTEGER-FLOOR division: max(2048, limit * 10 / 100). The
    // Python mirror (tools/triage_battle_load.py _headroom_low) must floor identically —
    // deep-review 2026-08-05 caught the mirrors disagreeing in the ~1 MB band below the
    // true 10% line on any limit not divisible by 10. These pins hold both semantics.

    [TestMethod]
    public void IsLowHeadroom_ExactlyAtFlooredPercentThreshold_ReturnsFalse()
        // limit=31646 → threshold = max(2048, 3164 [floored from 3164.6]); headroom 3164 is NOT low
        => Assert.IsFalse(MemoryPressureSampler.IsLowHeadroom(usedMb: 28482, limitMb: 31646));

    [TestMethod]
    public void IsLowHeadroom_OneBelowFlooredPercentThreshold_ReturnsTrue()
        // same limit, one MB more used → headroom 3163 < 3164 → low
        => Assert.IsTrue(MemoryPressureSampler.IsLowHeadroom(usedMb: 28483, limitMb: 31646));

    [TestMethod]
    public void IsLowHeadroom_HeadroomExactlyAtFloorOnOddLimit_ReturnsFalse()
        // limit=20481 → threshold = max(2048, 2048 [floored from 2048.1]); headroom 2048 is NOT low
        => Assert.IsFalse(MemoryPressureSampler.IsLowHeadroom(usedMb: 18433, limitMb: 20481));

    // ---- Warn-episode latch sequence (pins the once-per-episode contract end-to-end) -----

    [TestMethod]
    public void WarnEpisode_LatchThenRecoverThenLowAgain_FiresOncePerEpisode()
    {
        // Episode 1: low, unlatched → warn fires, latch set.
        Assert.IsTrue(MemoryPressureSampler.ShouldWarn(usedMb: 30000, limitMb: 31646, latched: false));
        // Still low, latched → no repeat warn, and no rearm while low.
        Assert.IsFalse(MemoryPressureSampler.ShouldWarn(usedMb: 30000, limitMb: 31646, latched: true));
        Assert.IsFalse(MemoryPressureSampler.ShouldRearm(usedMb: 30000, limitMb: 31646, latched: true));
        // Recovered past threshold+hysteresis (headroom 3164+512=3676 needed; 25000 → 6646) → rearm.
        Assert.IsTrue(MemoryPressureSampler.ShouldRearm(usedMb: 25000, limitMb: 31646, latched: true));
        // Episode 2: low again after rearm → warn fires again.
        Assert.IsTrue(MemoryPressureSampler.ShouldWarn(usedMb: 30000, limitMb: 31646, latched: false));
    }

    // ---- Format contract literals --------------------------------------------------------
    // These are the EXACT pinned cross-lane literals. The Python parser twin is
    // tools/tests/test_triage_battle_load.py — a token rename here breaks that lane.

    private static MemorySample HealthySample() => new MemorySample(
        privMb: 4211, wsMb: 3900, heapMb: 654,
        sysCommitUsedMb: 14003, sysCommitLimitMb: 31646,
        availPhysMb: 6200, totalPhysMb: 16296, memLoadPercent: 61);

    private static MemorySample LowHeadroomSample() => new MemorySample(
        privMb: 4211, wsMb: 3900, heapMb: 654,
        sysCommitUsedMb: 29847, sysCommitLimitMb: 31646,
        availPhysMb: 310, totalPhysMb: 16296, memLoadPercent: 97);

    [TestMethod]
    public void FormatSample_KnownValues_MatchesContractLine()
        => Assert.AreEqual(
            "[MemSample] privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=14003 sysCommitLimitMB=31646 availPhysMB=6200 memLoad=61%",
            MemoryPressureSampler.FormatSample(HealthySample()));

    [TestMethod]
    public void FormatWarn_KnownValues_MatchesContractLine()
        => Assert.AreEqual(
            "[MemSample] WARN LOW COMMIT HEADROOM headroomMB=1799 privMB=4211 wsMB=3900 heapMB=654 sysCommitUsedMB=29847 sysCommitLimitMB=31646 availPhysMB=310 memLoad=97%",
            MemoryPressureSampler.FormatWarn(LowHeadroomSample()));

    [TestMethod]
    public void FormatWarn_StartsWithWarnPrefix()
        => StringAssert.StartsWith(
            MemoryPressureSampler.FormatWarn(LowHeadroomSample()),
            "[MemSample] WARN LOW COMMIT HEADROOM headroomMB=");

    [TestMethod]
    public void FormatSession_KnownValues_MatchesContractLine()
        => Assert.AreEqual(
            "[MemSample] session totalPhysMB=16296 sysCommitLimitMB=31646",
            MemoryPressureSampler.FormatSession(HealthySample()));

    // ---- PollOnce behavior (real reader on the Windows test host, mocked logger/settings) -

    [TestMethod]
    public void PollOnce_SamplerDisabled_LogsNothing()
    {
        _settings.MemorySamplerEnabled.Returns(false);

        _sut.PollOnce(T0);

        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
        _logger.DidNotReceive().LogDebug(Arg.Any<string>());
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    // The sampler gates on its OWN toggle, deliberately NOT the master IsEnabled — turning
    // off battle-load phase logging must not kill session-wide crash forensics.
    [TestMethod]
    public void PollOnce_MasterToggleOff_StillSamples()
    {
        _settings.IsEnabled.Returns(false);

        _sut.PollOnce(T0);

        Assert.AreEqual(1, CountLoggedLines("session totalPhysMB="));
    }

    [TestMethod]
    public void PollOnce_FirstEnabledPoll_EmitsSessionLineOnce()
    {
        _sut.PollOnce(T0);
        _sut.PollOnce(T0.AddSeconds(31)); // second emit tick — session line must not repeat

        Assert.AreEqual(1, CountLoggedLines("session totalPhysMB="));
    }

    [TestMethod]
    public void PollOnce_WithinInterval_DoesNotEmitSecondSample()
    {
        _sut.PollOnce(T0);
        _sut.PollOnce(T0.AddSeconds(5));

        Assert.AreEqual(1, CountLoggedLines("privMB="));
    }

    [TestMethod]
    public void PollOnce_IntervalElapsed_EmitsAgain()
    {
        _sut.PollOnce(T0);
        _sut.PollOnce(T0.AddSeconds(31));

        Assert.AreEqual(2, CountLoggedLines("privMB="));
    }

    // The MCM knob must be live per poll — no timer rescheduling on change.
    [TestMethod]
    public void PollOnce_IntervalKnobLowered_TakesEffectWithoutRestart()
    {
        _sut.PollOnce(T0);
        _settings.MemorySampleIntervalSeconds.Returns(10d);

        _sut.PollOnce(T0.AddSeconds(11));

        Assert.AreEqual(2, CountLoggedLines("privMB="));
    }

    [TestMethod]
    public void PollOnce_LoggerThrows_DoesNotPropagate()
    {
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogDebug(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));
        _logger.When(l => l.LogError(Arg.Any<string>())).Do(_ => throw new InvalidOperationException("boom"));

        // Not throwing IS the assertion — a diagnostic must never crash the game.
        _sut.PollOnce(T0);
    }
}
