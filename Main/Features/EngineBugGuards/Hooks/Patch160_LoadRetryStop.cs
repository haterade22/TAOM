using System;
using HarmonyLib;
using SandBox;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem.Load;

namespace TAOM.Features.EngineBugGuards.Hooks;

/// <summary>
/// Patch160 (#1429; ported from Silas He's v1.5.6 fix package): a saved-game load that fails in step 3 spins on the
/// loading screen for ever, with "Initializing saved game begin..." as the last engine log line.
///
/// v1.5.7 <c>SandBoxGameManager.DoLoadingForGameManager</c> step 3 (SandBoxGameManager.cs:66-86) sets
/// <c>_loadedGameResult</c> to null before <c>Game.Current.DoLoading()</c>. When a later line throws, no engine frame
/// catches it before the native tick (<c>GameLoadingState.OnTick</c> :22-32, <c>Module.OnApplicationTick</c>). TAOM's
/// crash-capture finalizer on <c>Module.OnApplicationTick</c> (Patch37, on by default) swallows it, the step number in
/// <c>GameManagerBase.DoLoadingForGameManager()</c> (:213-219) does not advance, and the next frame runs step 3 again with
/// a null result, which fails at once, every frame.
///
/// The finalizer logs the first failure of each load with its stack; it is void, so the exception continues as before.
/// The prefix stops the retry (step 3 of a saved-game load with no result left can only be one) and offers to quit once.
/// The target is on <c>PatchShieldPolicy.ExcludedTargetMethods</c>: the override sets <c>nextStep</c> to <c>None</c> on
/// its first line, and step 5 reads <c>None</c> as "loading finished", so a PatchShield swallow would open a half-loaded
/// campaign. Callers: only <c>GameManagerBase.DoLoadingForGameManager()</c>. No installed type derives from
/// <c>SandBoxGameManager</c>; TAOM's battle game managers derive from <c>CustomGameManager</c>.
/// </summary>
[HarmonyPatch(typeof(SandBoxGameManager), "DoLoadingForGameManager",
    new[] { typeof(GameManagerLoadingSteps), typeof(GameManagerLoadingSteps) },
    new[] { ArgumentType.Normal, ArgumentType.Out })]
[HarmonyPatchCategory(Category)]
public static class Patch160_LoadRetryStop
{
    internal const string Category = "Patch160_LoadRetryStop";

    private static IModLogger? _logger;
    // Per load: each load builds a new SandBoxGameManager, so an earlier failure in the process does not hide this
    // load's stack. Holds the last failed manager until the next failure or the process ends.
    private static object? _reportedFor;
    // Per process: the prompt offers only Quit Game, so no later load follows in this process.
    private static bool _prompted;

    internal static void Initialize(IModLogger? logger)
    {
        _logger = logger;
        _reportedFor = null;
        _prompted = false;
    }

    internal static bool ShouldStopRetry(GameManagerLoadingSteps step, bool loadingSavedGame, bool hasLoadResult)
        => step == GameManagerLoadingSteps.SecondInitializeThirdState && loadingSavedGame && !hasLoadResult;

    // FOUR underscores: Harmony strips three, and the engine field is "_loadedGameResult".
    [HarmonyPrefix]
    public static bool Prefix(SandBoxGameManager __instance, GameManagerLoadingSteps gameManagerLoadingStep,
        ref GameManagerLoadingSteps nextStep, LoadResult ____loadedGameResult)
    {
        if (!ShouldStopRetry(gameManagerLoadingStep, __instance.LoadingSavedGame, ____loadedGameResult != null))
            return true;

        nextStep = GameManagerLoadingSteps.None;
        if (!_prompted)
        {
            _prompted = true;
            _logger?.LogError("[EngineBugGuards] the saved game failed to load in step 3; the retry loop is stopped (Patch160)");
            ShowQuitPrompt();
        }
        return false;
    }

    [HarmonyFinalizer]
    public static void Finalizer(Exception? __exception, object __instance, GameManagerLoadingSteps gameManagerLoadingStep)
    {
        if (__exception == null || ReferenceEquals(_reportedFor, __instance))
            return;

        _reportedFor = __instance;
        // Not "saved-game": SandBoxGameManager runs these steps for a new campaign too.
        _logger?.LogError($"[EngineBugGuards] load step {gameManagerLoadingStep} threw: {__exception}");
    }

    private static void ShowQuitPrompt()
    {
        try
        {
            // The loading window sits above the inquiry layer and holds input focus. Vanilla's own cancel path
            // (PreloadScreen.OnLoadCancelled) disables it the same way before it leaves the loading state.
            LoadingWindow.DisableGlobalLoadingWindow();
            InformationManager.ShowInquiry(new InquiryData(
                new TextObject("{=taom_load_failed_title}The saved game failed to load").ToString(),
                new TextObject("{=taom_load_failed_body}An error occurred while this save loaded, and the game cannot continue the load. The error is in the TAOM log. Quit the game, then send that log with a bug report.").ToString(),
                isAffirmativeOptionShown: true,
                isNegativeOptionShown: false,
                affirmativeText: new TextObject("{=taom_load_failed_quit}Quit Game").ToString(),
                negativeText: string.Empty,
                affirmativeAction: Utilities.QuitGame,
                negativeAction: null));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[EngineBugGuards] the load-failure prompt could not open: {ex.Message}");
        }
    }
}
