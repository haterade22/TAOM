using System;
using System.Reflection;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.TournamentGames;
using TAOM.Core.Logging;

namespace TAOM.Features.TournamentRewards.Hooks;

/// <summary>
/// Patch96 (docs/features/tournament-rewards.md): the MCM bet cap. <c>TournamentBehavior.GetMaximumBet</c> (SandBox,
/// public, non-virtual, v1.5.3) returns 150, doubled by Roguery's Deep Pockets, from a <c>const</c>; the bet
/// slider, the bet button and the "max amount" text all read this method (TournamentVM.cs:207, 922, 1066). Called
/// only from the tournament UI (TournamentVM), on the main thread. On any fault vanilla's cap stands and one
/// warning is logged, because the UI reads the cap repeatedly.
/// </summary>
[HarmonyPatch(typeof(TournamentBehavior), nameof(TournamentBehavior.GetMaximumBet))]
[HarmonyPatchCategory(TournamentRewardsModule.PatchCategory)]
public static class Patch96_TournamentMaxBet
{
    private static TournamentRewardsService? _service;
    private static bool _warned;

    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        try
        {
            __result = (_service ??= TAOM.IoC.Resolve<TournamentRewardsService>()).MaximumBet(__result);
        }
        catch (Exception ex)
        {
            // Vanilla's cap stands; the bet UI keeps working. The likely fault is a failed IoC resolve, which can
            // fail the logger's resolve as well, so the log line has a catch of its own.
            if (_warned)
                return;
            _warned = true;
            try { TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TournamentRewards] max-bet postfix failed ({ex.GetType().Name}: {ex.Message}); vanilla's cap stands"); } catch { }
        }
    }
}

/// <summary>
/// Patch96: the Join choices. Vanilla's "Join" consequence (<c>game_menu_tournament_join_current_game_on_consequence</c>,
/// private, v1.5.3:274) switches to the town menu, opens the tournament mission and tells the manager. This prefix
/// skips it, shows the prize and skill dialogs, and on the final pick re-enters the WHOLE vanilla method through a
/// thread-static bypass (lessons/harmony-il.md "Re-enter vanilla via a thread-static bypass flag"), so nothing
/// vanilla does is re-implemented. Closing a dialog leaves the player in the join menu, as before the click. On
/// any fault before the dialogs, vanilla's join runs: a safe default, since it is exactly the unpatched game. A fault
/// inside vanilla's own resumed run propagates with vanilla's exception and never runs vanilla twice. From the final
/// dialog it surfaces in the popup's Choose handler (MultiSelectionQueryPopUpVM.ExecuteAffirmativeAction, which then
/// skips CloseQuery) rather than in the menu's consequence chain; the engine's InvokeWithLog logs and rethrows it in
/// both cases.
/// </summary>
[HarmonyPatch(typeof(TournamentCampaignBehavior), "game_menu_tournament_join_current_game_on_consequence")]
[HarmonyPatchCategory(TournamentRewardsModule.PatchCategory)]
public static class Patch96_TournamentJoinChoices
{
    internal static readonly MethodInfo? Original =
        AccessTools.Method(typeof(TournamentCampaignBehavior), "game_menu_tournament_join_current_game_on_consequence");

    [ThreadStatic]
    private static bool _resuming;

    [HarmonyPrefix]
    public static bool Prefix(TournamentCampaignBehavior __instance, MenuCallbackArgs args)
    {
        if (_resuming || Original == null)
            return true;
        return RunChoices(
            proceed => TAOM.IoC.Resolve<TournamentJoinService>().BeginJoin(proceed),
            () => Resume(__instance, args),
            ex => TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TournamentRewards] Join choices failed ({ex.GetType().Name}: {ex.Message}); joining as vanilla"));
    }

    /// <summary>
    /// True when the prefix must let vanilla run. Only a fault before vanilla was resumed falls back to vanilla: once
    /// <paramref name="resume"/> has started, vanilla has run (or thrown on its own), so a second run would join twice.
    /// </summary>
    internal static bool RunChoices(Action<Action> beginJoin, Action resume, Action<Exception> warn)
    {
        bool resumed = false;
        try
        {
            beginJoin(() =>
            {
                resumed = true;
                resume();
            });
            return false;
        }
        catch (Exception ex) when (!resumed)
        {
            // swallow-ok: warn logs the fault, and the true return runs vanilla's own Join in its place.
            warn(ex);
            return true;
        }
    }

    private static void Resume(TournamentCampaignBehavior instance, MenuCallbackArgs args)
    {
        _resuming = true;
        try
        {
            Original!.Invoke(instance, new object[] { args });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Surface vanilla's own exception, not the reflection wrapper, as the unpatched game would.
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // not reached: Throw() always throws; the compiler cannot see that.
        }
        finally
        {
            _resuming = false;
        }
    }
}
