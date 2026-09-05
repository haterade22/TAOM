using System;
using System.Threading;

namespace TAOM.Features.BattleLoadDiagnostics;

// Main-thread-to-watchdog-thread handoff for the engine's live shader-compilation count.
//
// Why it exists (bundle b18f3441, 2026-09-04): a player load sat 305 s past
// FinishMissionLoadingDone and the stall watchdog wrote a crash bundle for it. The engine's
// rgl_log for that exact window holds 818 compile_shader lines and two other lines — the load was
// WORKING. MissionState.OnTick withholds the first Mission.Tick behind
// Handler.RenderIsReady() -> MissionScreen.MissionStartedRendering() -> SceneView.ReadyToRender(),
// which stays false while the scene's shaders compile, so no tick meant no BattlePlayable, no
// window close, and a false-positive bundle.
//
// Utilities.GetNumberOfShaderCompilationsInProgress() is a native call and is read ONLY from the
// main thread (MissionState_OnTick_RenderWait_Patch). The background watchdog reads this static
// instead of calling into the engine off-thread. Volatile/Interlocked for the same reason
// BattleLoadLoadingWindow uses them.
public static class BattleLoadRenderWaitProbe
{
    // -1 = never sampled. That is NOT the same as 0 (nothing compiling): a binding failure or a
    // throwing native read must never buy the watchdog a deferral, so the two readings are kept
    // distinct all the way to BattleLoadStallWatchdog.Decide.
    public const int NeverSampled = -1;

    private static volatile int _shadersInFlight = NeverSampled;
    private static long _lastChangeUtcTicks;

    // UTC ticks since the compile queue last went from empty to non-empty, or 0 while it is
    // empty. This is the CONTINUOUS-compile clock the churn backstop caps, and it deliberately
    // resets on every dip to zero: a load whose queue keeps draining and refilling is healthy,
    // while one that never drains is the churn case. Copied in semantics from
    // ShaderPrecompileDecider's _activeCompileSinceMs, which the 1.4.7 precompile hang produced.
    private static long _compilingSinceUtcTicks;

    public static int ShadersInFlight => _shadersInFlight;

    public static long LastChangeUtcTicks => Interlocked.Read(ref _lastChangeUtcTicks);

    // Records a reading, stamping the time ONLY when the count actually moved. "Moving" is the
    // whole signal: a queue that keeps changing is draining, a frozen one is a wedge.
    public static void Publish(int shadersInFlight, DateTime utcNow)
    {
        if (_shadersInFlight == shadersInFlight) return;
        // Store the timestamps BEFORE the volatile write publishes the new count, so a reader that
        // sees the new count also sees its stamps (BattleLoadLoadingWindow.Enter's ordering).
        Interlocked.Exchange(ref _lastChangeUtcTicks, utcNow.Ticks);

        if (shadersInFlight > 0)
        {
            // Only the empty-to-non-empty edge starts the clock; a change from 5 to 4 must not
            // restart it, or a churning queue would reset its own backstop on every sample.
            if (Interlocked.Read(ref _compilingSinceUtcTicks) == 0L)
                Interlocked.Exchange(ref _compilingSinceUtcTicks, utcNow.Ticks);
        }
        else
        {
            Interlocked.Exchange(ref _compilingSinceUtcTicks, 0L);
        }

        _shadersInFlight = shadersInFlight;
    }

    // Null when nothing has been sampled yet — an unmeasured value is absent, never zero.
    public static double? SecondsSinceLastChange(DateTime utcNow)
    {
        long ticks = LastChangeUtcTicks;
        if (ticks == 0L) return null;
        return (utcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
    }

    // Called when a load window opens. Carrying the previous mission's reading forward would let a
    // stale "still compiling" defer a genuine wedge on the next load.
    // Null while the queue is empty or nothing has been sampled. The churn backstop caps THIS,
    // not the time since the loading window opened: shader compilation only starts at
    // Scene.ResumeLoadingRenderings, so a load that spends minutes in native scene setup before
    // the first shader must not have that time deducted from its compile allowance.
    public static double? SecondsCompilingContinuously(DateTime utcNow)
    {
        long ticks = Interlocked.Read(ref _compilingSinceUtcTicks);
        if (ticks == 0L) return null;
        return (utcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalSeconds;
    }

    public static void Reset()
    {
        _shadersInFlight = NeverSampled;
        Interlocked.Exchange(ref _lastChangeUtcTicks, 0L);
        Interlocked.Exchange(ref _compilingSinceUtcTicks, 0L);
    }
}
