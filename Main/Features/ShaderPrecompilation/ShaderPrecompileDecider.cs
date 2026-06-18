namespace TAOM.Features.ShaderPrecompilation;

public enum PrecompileAction
{
    Wait,         // keep compiling / keep waiting on this item
    AdvanceItem,  // this item is done — move to the next
    AbortItem,    // hard timeout — give up on this item and move on
}

// Pure per-item state machine for the shader walk. Generalizes the proven LoadingScreen latch
// logic (RCA 2026-05-04 initial-zero collision): completion requires having OBSERVED work first,
// so the engine's "not queued anything yet" zero on the first frames is not mistaken for "done".
// Adds a settle window (count must hold at 0 for a bit), a startup grace (a scene with no NEW
// shaders to compile advances after the grace instead of hanging forever), and a hard per-item
// timeout. All time inputs are caller-supplied ms so this is fully unit-testable.
public sealed class ShaderPrecompileDecider
{
    private readonly long _startupGraceMs;
    private readonly long _settleMs;
    private readonly long _noProgressTimeoutMs;
    private readonly long _perItemTimeoutMs;

    // Defaults sized for the real workload: the all-characters battle legitimately compiles for
    // 20-70 min, so the absolute per-item cap is generous (90 min) and the responsive stuck-detector
    // is "count frozen for N min" (compiler hung on one shader), not a short fixed cap — that was the
    // 2026-05-04 premature-abort class of bug.
    public ShaderPrecompileDecider(
        long startupGraceMs = 30_000,   // render time with a zero count before declaring "cached"; gives margin for a load->first-shader gap (Codex watchpoint)
        long settleMs = 5_000,
        long noProgressTimeoutMs = 900_000,   // 15 min with no change in the count -> stuck
        long perItemTimeoutMs = 5_400_000)    // 90 min absolute backstop
    {
        _startupGraceMs = startupGraceMs;
        _settleMs = settleMs;
        _noProgressTimeoutMs = noProgressTimeoutMs;
        _perItemTimeoutMs = perItemTimeoutMs;
    }

    private bool _observedWork;
    private long _idleSinceMs = -1;
    private int _lastRemaining = int.MinValue;
    private long _lastChangeMs = -1;
    private long _renderStartedMs = -1;

    public bool HasObservedWork => _observedWork;

    public void ResetForItem()
    {
        _observedWork = false;
        _idleSinceMs = -1;
        _lastRemaining = int.MinValue;
        _lastChangeMs = -1;
        _renderStartedMs = -1;
    }

    // remaining     = Utilities.GetNumberOfShaderCompilationsInProgress()
    // itemElapsedMs = ms since THIS item started (StartGame) — used only for the absolute backstop
    // nowMs         = monotonic ms clock
    // isLoading     = LoadingWindow.IsLoadingWindowActive — the scene hasn't rendered yet while true
    public PrecompileAction Decide(int remaining, long itemElapsedMs, long nowMs, bool isLoading)
    {
        if (itemElapsedMs >= _perItemTimeoutMs)
            return PrecompileAction.AbortItem;

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
            // Count frozen while still compiling -> stuck on one shader -> give up on this item.
            if (_lastChangeMs >= 0 && (nowMs - _lastChangeMs) >= _noProgressTimeoutMs)
                return PrecompileAction.AbortItem;
            return PrecompileAction.Wait;
        }

        // remaining == 0
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
}
