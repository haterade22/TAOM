using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace TAOM.Features.ShaderPrecompilation.Hooks;

// Patches LoadingWindowViewModel.Update() to show shader compilation progress.
// Update() is internal (confirmed via decompilation) — AccessTools.Method() bypasses the access modifier.
// DescriptionText has a public setter and is MVVM data-bound — safe to write from any thread.
//
// Display: once per second we write
//   "Compiling shaders... N remaining (elapsed: 2m 15s) ..."
// so the user can see liveness even when the count holds steady on a heavy material.
// We also log a progress line to taom_debug_*.log every 30 s for post-mortem visibility.
//
// Stuck detection: only fires when remaining <= StuckTailRemainingMax (tail-end hangs).
// At StuckAbortSeconds we call MBGameManager.EndGame() to abort. Large-count pauses
// are normal during heavy material compilation and don't trigger stuck logic.
//
// Latch: completion is detected by transitioning from positive remaining to zero,
// guarded by _hasObservedWork. The first frame we run, the engine may not have
// queued any shaders yet (warm-cache fast loads); the original implementation
// hit the count==0 branch on that frame and disabled itself before any real work
// arrived. The _hasObservedWork flag prevents that.
[HarmonyPatch]
[HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
public static class LoadingScreen_ShaderProgress_Patch
{
    private const int StuckWarnSeconds      = 300;
    private const int StuckAbortSeconds     = 600;
    private const int StuckTailRemainingMax = 5;
    private const int TextUpdateIntervalMs  = 1000;
    private const int FileLogIntervalMs     = 30_000;

    private static IModLogger _logger;
    private static int  _lastShaderCount  = -1;
    private static long _stuckSinceMs     = -1;
    private static bool _abortTriggered   = false;
    private static bool _hasObservedWork  = false;
    private static long _battleStartedMs  = 0;
    private static long _lastTextUpdateMs = 0;
    private static long _lastFileLogMs    = 0;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    public static void ResetForNewBattle()
    {
        _lastShaderCount   = -1;
        _stuckSinceMs      = -1;
        _abortTriggered    = false;
        _hasObservedWork   = false;
        _battleStartedMs   = NowMs();
        _lastTextUpdateMs  = 0;
        _lastFileLogMs     = 0;
    }

    static MethodBase TargetMethod()
        => AccessTools.Method(typeof(LoadingWindowViewModel), "Update");

    [HarmonyPostfix]
    public static void Postfix(LoadingWindowViewModel __instance)
    {
        try
        {
            if (!__instance.Enabled) return;
            if (!TaomShaderGameManager.IsShaderBattleActive) return;

            int remaining = Utilities.GetNumberOfShaderCompilationsInProgress();
            long nowMs = NowMs();

            // First time we observe real work in this battle.
            if (remaining > 0 && !_hasObservedWork)
            {
                _hasObservedWork = true;
                _stuckSinceMs    = nowMs;
                _logger?.LogInfo($"[ShaderPrecompilation] First shaders queued: {remaining} remaining");
            }

            // Completion: count back to zero AFTER having seen work.
            if (remaining == 0 && _hasObservedWork)
            {
                int elapsedSeconds = (int)((nowMs - _battleStartedMs) / 1000);
                _logger?.LogInfo($"[ShaderPrecompilation] Compilation complete after {FormatElapsed(elapsedSeconds)}");
                TaomShaderGameManager.ResetShaderBattleActive();
                if (__instance.DescriptionText?.StartsWith("Compiling shaders") == true)
                    __instance.DescriptionText = string.Empty;
                _lastShaderCount = remaining;
                return;
            }

            // Initial 0 — engine hasn't queued anything yet. Wait, do NOT disable the latch.
            if (remaining == 0)
            {
                _lastShaderCount = remaining;
                return;
            }

            // remaining > 0 from here on.
            // Reset stuck timer whenever the count changes (compiler is making progress).
            if (remaining != _lastShaderCount)
            {
                _stuckSinceMs   = nowMs;
                _abortTriggered = false;
            }
            _lastShaderCount = remaining;

            // Throttle UI/file writes to 1 Hz / 30 s respectively.
            if (nowMs - _lastTextUpdateMs < TextUpdateIntervalMs) return;
            _lastTextUpdateMs = nowMs;

            int stuckSeconds   = (int)((nowMs - _stuckSinceMs) / 1000);
            int totalElapsed   = (int)((nowMs - _battleStartedMs) / 1000);
            string elapsedFmt  = FormatElapsed(totalElapsed);
            string heartbeat   = HeartbeatChars(totalElapsed);

            // Tail-end auto-abort.
            if (remaining <= StuckTailRemainingMax && stuckSeconds >= StuckAbortSeconds && !_abortTriggered)
            {
                _abortTriggered = true;
                _logger?.LogWarning($"[ShaderPrecompilation] Shader stuck for {stuckSeconds}s — aborting");
                __instance.DescriptionText = $"Compiling shaders... {remaining} remaining — stuck {stuckSeconds}s, aborting...";
                TaomShaderGameManager.ResetShaderBattleActive();
                MBGameManager.EndGame();
                return;
            }

            // Tail-end stuck warning.
            if (remaining <= StuckTailRemainingMax && stuckSeconds >= StuckWarnSeconds)
            {
                int abortIn = StuckAbortSeconds - stuckSeconds;
                __instance.DescriptionText =
                    $"Compiling shaders... {remaining} remaining (elapsed: {elapsedFmt}, stuck {stuckSeconds}s, aborting in {abortIn}s) {heartbeat}";
            }
            else
            {
                __instance.DescriptionText =
                    $"Compiling shaders... {remaining} remaining (elapsed: {elapsedFmt}) {heartbeat}";
            }

            // Periodic post-mortem progress line.
            if (nowMs - _lastFileLogMs >= FileLogIntervalMs)
            {
                _lastFileLogMs = nowMs;
                _logger?.LogInfo($"[ShaderPrecompilation] Progress: {remaining} remaining (elapsed: {elapsedFmt})");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] LoadingScreen patch error: {ex.Message}");
        }
    }

    private static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

    private static string FormatElapsed(int seconds)
    {
        int min = seconds / 60;
        int sec = seconds % 60;
        return min > 0 ? $"{min}m {sec}s" : $"{sec}s";
    }

    private static string HeartbeatChars(int seconds) => new string('.', (seconds % 4) + 1);
}
