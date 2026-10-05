using System;
using System.Reflection;
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
/// any fault before the dialogs, vanilla's join runs: a safe default, since it is exactly the unpatched game.
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
        try
        {
            TAOM.IoC.Resolve<TournamentJoinService>().BeginJoin(() => Resume(__instance, args));
            return false;
        }
        catch (Exception ex)
        {
            TAOM.IoC.Resolve<IModLogger>()?.LogWarning($"[TournamentRewards] Join choices failed ({ex.GetType().Name}: {ex.Message}); joining as vanilla");
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
        finally
        {
            _resuming = false;
        }
    }
}
