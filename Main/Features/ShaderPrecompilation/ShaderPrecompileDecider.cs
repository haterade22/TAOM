namespace TAOM.Features.ShaderPrecompilation;

public enum PrecompileAction
{
    Wait,         // keep compiling / keep waiting on this item
    AdvanceItem,  // this item is done — move to the next
    AbortItem,    // hard timeout — give up on this item and move on
}

// Why the CURRENT item was aborted — surfaced so the runner logs a self-classifying line and a future
// stall report identifies its own mechanism without re-instrumenting (the 1.4.7 hang investigation).
public enum PrecompileAbortReason
{
    None,
    AbsoluteTimeout,  // item exceeded the absolute per-item wall (load + grace + compile)
    FrozenCount,      // count stuck > 0 and UNCHANGED — compiler hung on one shader
    ChurnTimeout,     // count > 0 CONTINUOUSLY (never dipped to 0) too long — churns without ever settling
}

// Pure per-item state machine for the shader walk. Generalizes the proven LoadingScreen latch
// logic (RCA 2026-05-04 initial-zero collision): completion requires having OBSERVED work first,
// so the engine's "not queued anything yet" zero on the first frames is not mistaken for "done".
// Adds a settle window (count must hold at 0 for a bit), a startup grace (a scene with no NEW
// shaders to compile advances after the grace instead of hanging forever), a hard per-item
// timeout, and a churn backstop (1.4.7: a count that changes every frame but never returns to 0
// slips past the frozen-count guard — bound it by continuous-nonzero time). All time inputs are
// caller-supplied ms so this is fully unit-testable.
public sealed class ShaderPrecompileDecider
{
    private readonly long _startupGraceMs;
    private readonly long _settleMs;
    // Ctor values are the DEFAULT per-item caps; the runner overrides them per item kind via the
    // ResetForItem(caps) overload (scene passes get tight caps, the character battle stays generous).
    private readonly long _defaultNoProgressTimeoutMs;
    private readonly long _defaultPerItemTimeoutMs;
    private readonly long _defaultMaxActiveCompileMs;

    // Defaults sized for the real workload: the all-characters battle legitimately compiles for
    // 20-70 min, so the absolute per-item cap is generous (90 min) and the responsive stuck-detector
    // is "count frozen for N min" (compiler hung on one shader), not a short fixed cap — that was the
    // 2026-05-04 premature-abort class of bug. The churn backstop defaults OFF (long.MaxValue) so the
    // ctor-only callers (and the character battle) are unchanged; the runner enables it for scene passes.
    public ShaderPrecompileDecider(
        long startupGraceMs = 30_000,   // render time with a zero count before declaring "cached"; gives margin for a load->first-shader gap (Codex watchpoint)
        long settleMs = 5_000,
        long noProgressTimeoutMs = 900_000,      // 15 min with no CHANGE in the count -> stuck on one shader
        long perItemTimeoutMs = 5_400_000,       // 90 min absolute backstop
        long maxActiveCompileMs = long.MaxValue) // continuously-nonzero cap; disabled by default
    {
        _startupGraceMs = startupGraceMs;
        _settleMs = settleMs;
        _defaultNoProgressTimeoutMs = noProgressTimeoutMs;
        _defaultPerItemTimeoutMs = perItemTimeoutMs;
        _defaultMaxActiveCompileMs = maxActiveCompileMs;
        _noProgressTimeoutMs = noProgressTimeoutMs;
        _perItemTimeoutMs = perItemTimeoutMs;
        _maxActiveCompileMs = maxActiveCompileMs;
    }

    // Live per-item caps (default to the ctor values; overridden by the ResetForItem(caps) overload).
    private long _noProgressTimeoutMs;
    private long _perItemTimeoutMs;
    private long _maxActiveCompileMs;

    private bool _observedWork;
    private long _idleSinceMs = -1;
    private int _lastRemaining = int.MinValue;
    private long _lastChangeMs = -1;
    private long _renderStartedMs = -1;
    // When the count last transitioned from 0 to >0 without an intervening zero (the continuous-compile
    // clock for the churn backstop). Reset to -1 whenever the count returns to 0.
    private long _activeCompileSinceMs = -1;

    public bool HasObservedWork => _observedWork;

    // The reason the last AbortItem was returned (None until an item aborts; cleared on ResetForItem).
    public PrecompileAbortReason LastAbortReason { get; private set; } = PrecompileAbortReason.None;

    // Reset per-item state, restoring the ctor default caps.
    public void ResetForItem() => ResetForItem(_defaultPerItemTimeoutMs, _defaultNoProgressTimeoutMs, _defaultMaxActiveCompileMs);

    // Reset per-item state with caps chosen by the runner for THIS item's kind (scene passes tight,
    // character battle generous). Keeps the pure/unit-testable shape — no engine types cross in.
    public void ResetForItem(long perItemTimeoutMs, long noProgressTimeoutMs, long maxActiveCompileMs)
    {
        _perItemTimeoutMs = perItemTimeoutMs;
        _noProgressTimeoutMs = noProgressTimeoutMs;
        _maxActiveCompileMs = maxActiveCompileMs;
        _observedWork = false;
        _idleSinceMs = -1;
        _lastRemaining = int.MinValue;
        _lastChangeMs = -1;
        _renderStartedMs = -1;
        _activeCompileSinceMs = -1;
        LastAbortReason = PrecompileAbortReason.None;
    }

    // remaining     = Utilities.GetNumberOfShaderCompilationsInProgress()
    // itemElapsedMs = ms since THIS item started (StartGame) — used only for the absolute backstop
    // nowMs         = monotonic ms clock
    // isLoading     = LoadingWindow.IsLoadingWindowActive — the scene hasn't rendered yet while true
    public PrecompileAction Decide(int remaining, long itemElapsedMs, long nowMs, bool isLoading)
    {
        if (itemElapsedMs >= _perItemTimeoutMs)
            return Abort(PrecompileAbortReason.AbsoluteTimeout);

        // Track when the count last changed (progress). First observation seeds the clock.
        if (remaining != _lastRemaining)
        {
            _lastRemaining = remaining;
            _lastChangeMs = nowMs;
        }

        // The "nothing to compile" grace must count RENDER time, not LOAD time: the item clock
        // starts at StartGame, but the scene can still be loading (loading window up) for tens of
        // seconds before it renders and queues shaders. Mark when rendering actually began.
        if (!isLoading && _renderStartedMs < 0)
            _renderStartedMs = nowMs;

        if (remaining > 0)
        {
            _observedWork = true;
            _idleSinceMs = -1;
            // Continuous-compile clock: starts on the first >0 after a zero, runs until the count dips to 0.
            if (_activeCompileSinceMs < 0)
                _activeCompileSinceMs = nowMs;
            // Count frozen while still compiling -> stuck on one shader -> give up on this item.
            if (_lastChangeMs >= 0 && (nowMs - _lastChangeMs) >= _noProgressTimeoutMs)
                return Abort(PrecompileAbortReason.FrozenCount);
            // Count churning but NEVER returning to 0 -> slips past the frozen guard -> bound by continuous time.
            if ((nowMs - _activeCompileSinceMs) >= _maxActiveCompileMs)
                return Abort(PrecompileAbortReason.ChurnTimeout);
            return PrecompileAction.Wait;
        }

        // remaining == 0 — a dip to zero resets the continuous-compile (churn) clock.
        _activeCompileSinceMs = -1;

        if (!_observedWork)
        {
            // Still loading (or first render frame not yet seen) — we CANNOT conclude "nothing to
            // compile" yet; the scene's shaders queue only once it renders. Wait. Only after the
            // scene has been RENDERING for the grace with a still-zero count is it genuinely cached.
            if (isLoading || _renderStartedMs < 0)
                return PrecompileAction.Wait;
            return (nowMs - _renderStartedMs) >= _startupGraceMs
                ? PrecompileAction.AdvanceItem
                : PrecompileAction.Wait;
        }

        // Work was observed and the count is back to 0 — require it to hold for the settle window
        // before declaring this item done (the count can momentarily dip to 0 between batches).
        if (_idleSinceMs < 0)
        {
            _idleSinceMs = nowMs;
            return PrecompileAction.Wait;
        }

        return (nowMs - _idleSinceMs) >= _settleMs ? PrecompileAction.AdvanceItem : PrecompileAction.Wait;
    }

    private PrecompileAction Abort(PrecompileAbortReason reason)
    {
        LastAbortReason = reason;
        return PrecompileAction.AbortItem;
    }
}
