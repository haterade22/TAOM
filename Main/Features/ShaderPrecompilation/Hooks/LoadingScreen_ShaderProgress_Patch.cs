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
// Stuck detection: if the remaining count stays non-zero for StuckTimeoutSeconds, calls
// MBGameManager.EndGame() (async, fire-and-forget) to abort and return to the main menu.
// Only active when TaomShaderGameManager.IsShaderBattleActive is true.
//
// No shader name API exists in v1.3.12 — TaleWorlds only exposes the count.
// Stuck duration is shown in the text so the user can see how long it has been blocked.
[HarmonyPatch]
[HarmonyPatchCategory("Patch21_ShaderPrecompilation")]
public static class LoadingScreen_ShaderProgress_Patch
{
    // Bannerlord's shader compiler is single-threaded native; a single heavy material
    // can hold the count at one value for several minutes on slower hardware. Only
    // treat as "stuck" when remaining is small (tail end) — large-count pauses are
    // normal and recover. Raise warn/abort to give compilation room to finish.
    private const int StuckWarnSeconds      = 300;
    private const int StuckAbortSeconds     = 600;
    private const int StuckTailRemainingMax = 5;

    private static IModLogger _logger;
    private static int  _lastShaderCount  = -1;
    private static long _stuckSinceMs     = -1;   // DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond when count last changed
    private static bool _abortTriggered   = false;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
    }

    public static void ResetForNewBattle()
    {
        _lastShaderCount = -1;
        _stuckSinceMs    = -1;
        _abortTriggered  = false;
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

            // Count changed — reset stuck timer and update text
            if (remaining != _lastShaderCount)
            {
                _lastShaderCount = remaining;
                _stuckSinceMs    = remaining > 0 ? DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond : -1;
                _abortTriggered  = false;

                if (remaining > 0)
                {
                    __instance.DescriptionText = $"Compiling shaders... {remaining} remaining";
                }
                else
                {
                    // All shaders compiled — clear the latch so later loading screens
                    // don't inherit TAOM's text override and stuck-abort logic
                    TaomShaderGameManager.ResetShaderBattleActive();
                    if (__instance.DescriptionText?.StartsWith("Compiling shaders") == true)
                        __instance.DescriptionText = string.Empty;
                }

                return;
            }

            // Count unchanged — check for stuck condition.
            // Only treat as stuck when we're in the tail (remaining <= StuckTailRemainingMax);
            // large-count pauses are normal during heavy material compilation and recover.
            if (remaining <= 0 || _stuckSinceMs < 0) return;
            if (remaining > StuckTailRemainingMax) return;

            int stuckSeconds = (int)((DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond - _stuckSinceMs) / 1000);

            if (stuckSeconds >= StuckAbortSeconds && !_abortTriggered)
            {
                _abortTriggered = true;
                _logger?.LogWarning($"[ShaderPrecompilation] Shader stuck for {stuckSeconds}s — aborting");
                __instance.DescriptionText = $"Compiling shaders... {remaining} remaining — stuck {stuckSeconds}s, aborting...";
                // Clear the latch so any later loading screen (new campaign etc.) doesn't
                // inherit our shader-progress text override while the engine continues
                // compiling stragglers in the background.
                TaomShaderGameManager.ResetShaderBattleActive();
                MBGameManager.EndGame();
                return;
            }

            if (stuckSeconds >= StuckWarnSeconds)
            {
                int abortIn = StuckAbortSeconds - stuckSeconds;
                __instance.DescriptionText = $"Compiling shaders... {remaining} remaining — stuck {stuckSeconds}s (aborting in {abortIn}s)";
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] LoadingScreen patch error: {ex.Message}");
        }
    }
}
