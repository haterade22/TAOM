using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ShaderPrecompilation;

namespace TAOM.Tests.Features.ShaderPrecompilation;

[TestClass]
public class ShaderPrecompileDeciderTests
{
    // grace=100ms, settle=50ms, noProgress=huge (won't fire here), perItemTimeout=10000ms.
    private static ShaderPrecompileDecider New() => new(100, 50, 1_000_000, 10_000);

    [TestMethod]
    public void Decide_FirstFrameZeroBeforeWork_Waits_NotAdvance()
    {
        // The RCA 2026-05-04 bug: count==0 on the first frames (engine hasn't queued yet) must NOT
        // be read as "complete". Even rendering, within the grace it's a Wait.
        var d = New();
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(remaining: 0, itemElapsedMs: 10, nowMs: 1000, isLoading: false));
    }

    [TestMethod]
    public void Decide_StillLoading_NeverAdvancesOnZero_EvenPastGrace()
    {
        // HIGH fix (deep-review 2026-06-17): the grace must count RENDER time, not LOAD time. While
        // the loading window is up the scene hasn't rendered, so a zero count must NOT advance the
        // item even if the item clock has long exceeded the grace — else heavy scenes get skipped.
        // itemElapsed stays under the 10s absolute backstop so this isolates the loading/grace logic.
        var d = New(); // grace = 100ms, perItemTimeout = 10_000ms
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, itemElapsedMs: 5_000, nowMs: 1000, isLoading: true)); // 5000 > grace but loading -> Wait
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, itemElapsedMs: 9_000, nowMs: 5000, isLoading: true)); // still loading -> Wait
        // Loading ends -> render starts -> the grace now begins counting from here.
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 9_100, nowMs: 6000, isLoading: false));        // renderStart = 6000
        Assert.AreEqual(PrecompileAction.AdvanceItem, d.Decide(0, 9_200, nowMs: 6100, isLoading: false)); // 6100-6000 = 100 -> advance
    }

    [TestMethod]
    public void Decide_NoWorkAfterRenderGrace_Advances()
    {
        // A scene whose shaders are already cached renders but queues nothing — after the grace of
        // RENDER time, advance rather than hang forever.
        var d = New();
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 50, nowMs: 1000, isLoading: false));        // renderStart = 1000
        Assert.AreEqual(PrecompileAction.AdvanceItem, d.Decide(0, 100, nowMs: 1100, isLoading: false)); // 1100-1000 = 100 >= grace
    }

    [TestMethod]
    public void Decide_WorkThenIdleHeldForSettle_Advances()
    {
        var d = New();
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 10, 1000, false));   // work observed
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 20, 1010, false));   // idle starts (idleSince=1010)
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 30, 1040, false));   // 30 < 50 settle -> wait
        Assert.AreEqual(PrecompileAction.AdvanceItem, d.Decide(0, 40, 1060, false)); // 1060-1010=50 -> advance
    }

    [TestMethod]
    public void Decide_WorkObservedDuringLoading_StillCountsAsWork()
    {
        // Shaders DO compile during the loading-screen preload (per the RCA log). A positive count
        // while loading must set observed-work so the later zero is treated as completion (settle),
        // not as "still loading".
        var d = New();
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(8, 10, 1000, isLoading: true)); // work while loading
        Assert.IsTrue(d.HasObservedWork);
    }

    [TestMethod]
    public void Decide_IdleDipThenWorkAgain_DoesNotAdvancePrematurely()
    {
        var d = New();
        d.ResetForItem();
        d.Decide(5, 10, 1000, false);   // observed
        d.Decide(0, 20, 1010, false);   // idle (idleSince=1010)
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(3, 30, 1020, false)); // work again -> resets idle
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 40, 1030, false)); // idle again (idleSince=1030)
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 50, 1060, false)); // 1060-1030=30 < 50 -> wait
    }

    [TestMethod]
    public void Decide_PerItemTimeout_Aborts_EvenWithWorkInFlight()
    {
        var d = New();
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(remaining: 5, itemElapsedMs: 10_000, nowMs: 99_999, isLoading: false));
    }

    [TestMethod]
    public void Decide_CountFrozenWhileCompiling_AbortsAsStuck()
    {
        // noProgress=200ms: count stuck >0 and unchanged past 200ms -> compiler hung on one shader.
        var d = new ShaderPrecompileDecider(100, 50, 200, 10_000);
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(7, 10, 1000, false));        // first obs -> change clock=1000
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(7, 100, 1150, false));       // 150 < 200 -> wait
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(7, 250, 1210, false));  // 210 >= 200 -> stuck
    }

    [TestMethod]
    public void Decide_CountChanging_DoesNotAbortAsStuck()
    {
        var d = new ShaderPrecompileDecider(100, 50, 200, 10_000);
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(7, 10, 1000, false));
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 100, 1150, false));   // changed -> clock resets to 1150
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 250, 1300, false));   // 1300-1150=150 < 200 -> not stuck
    }

    [TestMethod]
    public void Decide_ObservedWorkFlag_TracksFirstPositiveCount()
    {
        var d = New();
        d.ResetForItem();
        Assert.IsFalse(d.HasObservedWork);
        d.Decide(0, 10, 1000, false);
        Assert.IsFalse(d.HasObservedWork);
        d.Decide(7, 20, 1010, false);
        Assert.IsTrue(d.HasObservedWork);
        d.ResetForItem();
        Assert.IsFalse(d.HasObservedWork, "ResetForItem must clear the per-item observed-work latch");
    }

    // ---- 1.4.7 robustness: churn backstop, per-kind caps, self-classifying abort reason ---- //

    [TestMethod]
    public void Decide_ContinuouslyCompilingPastChurnCap_Aborts_WithChurnReason()
    {
        // The 1.4.7 hang class: a count that CHANGES every frame (never returns to 0) never trips the
        // frozen-count guard (which needs an UNCHANGED count). The churn backstop bounds it — continuously
        // nonzero for maxActiveCompile -> abort. grace=100, settle=50, noProgress=huge (won't fire),
        // perItem=huge (won't fire), maxActiveCompile=300ms.
        var d = new ShaderPrecompileDecider(100, 50, 1_000_000, 1_000_000, 300);
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(9, 10, 1000, false));   // active-compile clock starts = 1000
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(7, 100, 1150, false));  // changed, 150 < 300 (frozen guard can't fire — count moving)
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 200, 1250, false));  // 250 < 300
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(4, 300, 1310, false)); // 1310-1000 = 310 >= 300 -> churn abort
        Assert.AreEqual(PrecompileAbortReason.ChurnTimeout, d.LastAbortReason);
    }

    [TestMethod]
    public void Decide_CompileDipsToZeroBeforeChurnCap_ResetsChurnClock()
    {
        // A dip to 0 (even brief, between batches) resets the continuously-nonzero clock, so a healthy
        // batchy compile is never mistaken for a runaway churn.
        var d = new ShaderPrecompileDecider(100, 50, 1_000_000, 1_000_000, 300);
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(9, 10, 1000, false));   // active clock = 1000
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(0, 100, 1200, false));  // dip to 0 -> churn clock reset; idle starts
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 150, 1250, false));  // work again -> active clock = 1250
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(4, 250, 1450, false));  // 1450-1250 = 200 < 300 -> no churn abort
    }

    [TestMethod]
    public void ResetForItem_WithCaps_OverridesCtorDefaults_AbsoluteReason()
    {
        // The runner passes per-item-kind caps via the overload. A short perItem override must abort
        // sooner than the (huge) ctor default, and the reason must be the absolute backstop.
        var d = new ShaderPrecompileDecider(100, 50, 1_000_000, 10_000_000, long.MaxValue);
        d.ResetForItem(perItemTimeoutMs: 500, noProgressTimeoutMs: 1_000_000, maxActiveCompileMs: long.MaxValue);
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(5, itemElapsedMs: 500, nowMs: 1000, isLoading: false));
        Assert.AreEqual(PrecompileAbortReason.AbsoluteTimeout, d.LastAbortReason);
    }

    [TestMethod]
    public void Decide_FrozenCount_SetsFrozenReason()
    {
        var d = new ShaderPrecompileDecider(100, 50, 200, 10_000, long.MaxValue);
        d.ResetForItem();
        d.Decide(7, 10, 1000, false);
        d.Decide(7, 100, 1150, false);
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(7, 250, 1210, false));
        Assert.AreEqual(PrecompileAbortReason.FrozenCount, d.LastAbortReason);
    }

    [TestMethod]
    public void ResetForItem_ClearsAbortReason()
    {
        var d = new ShaderPrecompileDecider(100, 50, 200, 10_000, long.MaxValue);
        d.ResetForItem();
        d.Decide(7, 10, 1000, false);
        d.Decide(7, 300, 1300, false); // frozen abort
        Assert.AreNotEqual(PrecompileAbortReason.None, d.LastAbortReason);
        d.ResetForItem();
        Assert.AreEqual(PrecompileAbortReason.None, d.LastAbortReason, "ResetForItem must clear the last abort reason");
    }

    [TestMethod]
    public void Decide_ChurnCapDisabledByDefault_DoesNotAbortChangingCount()
    {
        // Backward-compat: the default ctor (no maxActiveCompile) leaves churn disabled, so a changing
        // count that never settles keeps waiting exactly as before (only the absolute/frozen caps bound it).
        var d = new ShaderPrecompileDecider(100, 50, 1_000_000, 1_000_000); // 4-arg: maxActiveCompile defaults to disabled
        d.ResetForItem();
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(9, 10, 1000, false));
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(7, 5_000, 6000, false));   // changing, way past any short cap
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(5, 50_000, 51_000, false));
        Assert.AreEqual(PrecompileAbortReason.None, d.LastAbortReason);
    }

    [TestMethod]
    public void ResetForItemNoArg_RestartsChurnClock_NoCarryOverAbort()
    {
        // The no-arg ResetForItem() (used between items) must RESTART the continuous-compile clock, not
        // carry the previous item's start time — else a fresh item at a later wall-clock would abort instantly.
        var d = new ShaderPrecompileDecider(100, 50, 1_000_000, 1_000_000, 300);
        d.ResetForItem();
        d.Decide(9, 10, 1000, false);                                              // churn clock starts = 1000
        Assert.AreEqual(PrecompileAction.AbortItem, d.Decide(8, 400, 1400, false)); // 1400-1000 = 400 >= 300 -> churn abort
        Assert.AreEqual(PrecompileAbortReason.ChurnTimeout, d.LastAbortReason);

        d.ResetForItem();                                                          // restore ctor caps + clear churn clock/reason
        Assert.AreEqual(PrecompileAbortReason.None, d.LastAbortReason);
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(9, 10, 2000, false));      // churn clock restarts at 2000, NOT the old 1000
        Assert.AreEqual(PrecompileAction.Wait, d.Decide(8, 200, 2200, false));     // 2200-2000 = 200 < 300 -> wait (no carry-over abort)
    }
}
