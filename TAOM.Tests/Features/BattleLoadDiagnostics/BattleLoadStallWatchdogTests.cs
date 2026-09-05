using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

[TestClass]
public class BattleLoadStallWatchdogTests
{
    [TestMethod]
    public void ShouldFire_WindowOpenPastThresholdAndNotFired_ReturnsTrue()
        => Assert.IsTrue(BattleLoadStallWatchdog.ShouldFire(true, 50d, 45d, false));

    [TestMethod]
    public void ShouldFire_ExactlyThreshold_ReturnsTrue()
        => Assert.IsTrue(BattleLoadStallWatchdog.ShouldFire(true, 45d, 45d, false));

    [TestMethod]
    public void ShouldFire_BelowThreshold_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(true, 10d, 45d, false));

    [TestMethod]
    public void ShouldFire_AlreadyFired_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(true, 99d, 45d, true));

    [TestMethod]
    public void ShouldFire_WindowClosed_ReturnsFalse()
        => Assert.IsFalse(BattleLoadStallWatchdog.ShouldFire(false, 999d, 45d, false));

    // ---- Shader-compile deferral (bundle b18f3441, 2026-09-04) ----
    // A player load sat 305 s past FinishMissionLoadingDone and fired a stall bundle. The engine log
    // for that exact window holds 818 compile_shader lines and nothing else: the load was working,
    // and MissionState.OnTick was withholding the first Mission.Tick behind
    // MissionScreen.RenderIsReady() -> SceneView.ReadyToRender() while ~700 cold character shaders
    // compiled one at a time. Wall clock alone cannot tell that apart from a wedge, so the watchdog
    // asks two more questions: is the compile queue still MOVING, and has it been busy so long
    // without ever draining that it has to be reported anyway?

    private const double NoProgress = 60d;
    private const double MaxCompile = 900d;

    private static BattleLoadStallWatchdog.StallAction Decide(
        int shaders, double sinceChange, double compiling)
        => BattleLoadStallWatchdog.Decide(shaders, sinceChange, compiling, NoProgress, MaxCompile);

    [TestMethod]
    public void Decide_CompilingAndCountMovingRecently_Defers()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.Defer, Decide(412, 3d, 120d));

    [TestMethod]
    public void Decide_NoShadersInFlight_FiresWedge()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireWedge, Decide(0, 1d, 0d));

    // The whole point of the no-progress window: a compile queue that stops draining IS a wedge,
    // and it must still produce a bundle rather than deferring forever.
    [TestMethod]
    public void Decide_CountFrozenPastNoProgressWindow_FiresWedge()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireWedge, Decide(412, 61d, 120d));

    [TestMethod]
    public void Decide_CountFrozenExactlyAtWindow_FiresWedge()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireWedge, Decide(412, 60d, 120d));

    // -1 is the probe's "never sampled" sentinel (the hook never ran, or the native read threw).
    // Absent evidence must not buy a deferral, or a binding failure would silently disable the
    // watchdog — the same never-read-absence-as-zero rule the polls=0 token follows.
    [TestMethod]
    public void Decide_CountNeverSampled_FiresWedge()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireWedge, Decide(-1, 0d, 0d));

    // NaN reaches this gate if a clock ever misbehaves. `secondsSinceCountChanged < noProgress`
    // is written as a positive requirement, so NaN fails it and fires rather than deferring.
    [TestMethod]
    public void Decide_NonFiniteSinceChange_FiresWedgeRatherThanDeferring()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireWedge, Decide(412, double.NaN, 120d));

    // ---- The churn backstop ----
    // "Changing" is not "draining". A count that thrashes among positive values refreshes the
    // no-progress clock forever, so a hold-off gated only on that clock would suppress the bundle
    // for the whole session — the failure the watchdog exists to prevent, inverted into silence.
    // TAOM already carries this lesson as ShaderPrecompileDecider's named ChurnTimeout abort
    // ("count > 0 CONTINUOUSLY, churns without ever settling", from the 1.4.7 precompile hang).

    [TestMethod]
    public void Decide_MovingQueuePastTheContinuousCompileCap_FiresChurnCapped()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireChurnCapped, Decide(412, 3d, 901d));

    [TestMethod]
    public void Decide_MovingQueueExactlyAtTheCap_FiresChurnCapped()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireChurnCapped, Decide(412, 3d, 900d));

    [TestMethod]
    public void Decide_MovingQueueJustInsideTheCap_Defers()
        => Assert.AreEqual(BattleLoadStallWatchdog.StallAction.Defer, Decide(412, 3d, 899d));

    /// <summary>
    /// The cap is measured on CONTINUOUS COMPILATION, not on how long the loading window has been
    /// open. A big siege scene can legitimately spend minutes in native scene setup before the
    /// first shader compiles, and that time must not be deducted from the compile allowance. This
    /// is the review finding that the first cut of the cap got wrong.
    /// </summary>
    [TestMethod]
    public void Decide_LongPreRenderLoadThenBriefCompile_StillDefers()
    {
        // 550s of scene setup, then only 60s of actual compiling: nowhere near the cap.
        Assert.AreEqual(BattleLoadStallWatchdog.StallAction.Defer, Decide(412, 3d, 60d));
    }

    /// <summary>
    /// The scenario the cap exists for, simulated over many polls: a count that oscillates among
    /// positive values, changing every 40s so the no-progress clock never expires, and never
    /// draining so the continuous-compile clock keeps climbing. Without the cap this defers on
    /// every poll forever and the player never gets a bundle for a load that never finishes.
    /// </summary>
    [TestMethod]
    public void Decide_OscillatingCountOverManyPolls_EventuallyStopsDeferring()
    {
        var deferrals = 0;
        var capped = false;

        // Poll every 5s (the real PollInterval) for an hour. The count changed 40s ago at every
        // poll and the queue has never been empty, so `compiling` tracks elapsed time.
        for (double compiling = 5d; compiling <= 3600d; compiling += 5d)
        {
            var action = Decide(412, 40d, compiling);
            if (action == BattleLoadStallWatchdog.StallAction.Defer) { deferrals++; continue; }
            Assert.AreEqual(BattleLoadStallWatchdog.StallAction.FireChurnCapped, action,
                "a moving-but-never-draining queue must fire as churn-capped, not as a wedge");
            capped = true;
            break;
        }

        Assert.IsTrue(capped, "an oscillating compile queue must eventually stop deferring, or a "
                              + "genuinely unfinishable load produces no bundle at all");
        Assert.IsTrue(deferrals > 0, "it must still defer while inside the cap, or the fix for "
                                     + "b18f3441's false positive is undone");
    }

    // The token DERIVES from the verdict rather than re-deriving its algebra, so the two seams
    // cannot contradict each other. Pass 1 shipped a second copy of the condition; a review pass
    // flagged it as the "two seams, same guards" trap and this is the consolidated shape.
    [TestMethod]
    public void FormatChurnToken_ChurnCapped_MarksTheLine()
        => Assert.AreEqual("churn-capped ",
            BattleLoadStallWatchdog.FormatChurnToken(BattleLoadStallWatchdog.StallAction.FireChurnCapped));

    [TestMethod]
    public void FormatChurnToken_Wedge_EmitsNothing()
        => Assert.AreEqual(string.Empty,
            BattleLoadStallWatchdog.FormatChurnToken(BattleLoadStallWatchdog.StallAction.FireWedge));

    [TestMethod]
    public void FormatChurnToken_Defer_EmitsNothing()
        => Assert.AreEqual(string.Empty,
            BattleLoadStallWatchdog.FormatChurnToken(BattleLoadStallWatchdog.StallAction.Defer));

    [TestMethod]
    public void FormatShaderToken_NeverSampled_OmitsTheToken()
        => Assert.AreEqual(string.Empty, BattleLoadStallWatchdog.FormatShaderToken(-1));

    [TestMethod]
    public void FormatShaderToken_NothingCompiling_StillReportsZero()
        => Assert.AreEqual("shaders=0 ", BattleLoadStallWatchdog.FormatShaderToken(0));

    /// <summary>
    /// The C# side of the cross-language contract. The tokens sit between the em-dash and the
    /// literal `last`, and triage_battle_load.py's _WATCHDOG_RE has to tolerate them. It did not
    /// until 2026-09-04, so every bundle carrying real telemetry had its watchdog line silently
    /// dropped. tools/tests/test_triage_battle_load.py pins the Python half against this shape.
    /// </summary>
    [TestMethod]
    public void WatchdogLineTokens_ComposeInTheOrderTheParserExpects()
    {
        var line = BattleLoadStallWatchdog.FormatShaderToken(412)
                   + BattleLoadStallWatchdog.FormatChurnToken(BattleLoadStallWatchdog.StallAction.FireChurnCapped)
                   + "last phase=WaitingForRender";

        Assert.AreEqual("shaders=412 churn-capped last phase=WaitingForRender", line);
    }
}
