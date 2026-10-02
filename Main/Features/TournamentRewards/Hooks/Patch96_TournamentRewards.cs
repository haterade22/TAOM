using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.TournamentRewards.Hooks;

/// <summary>
/// Patch96 (docs/features/tournament-rewards.md): the MCM bet cap. <c>TournamentBehavior.GetMaximumBet</c> (SandBox,
/// public, non-virtual, v1.5.3) returns 150, doubled by Roguery's Deep Pockets, from a <c>const</c>; the bet
/// slider, the bet button and the "max amount" text all read this method (TournamentVM.cs:207, 922, 1066). Called
/// from the tournament UI and TournamentBehavior on the main thread. On any fault vanilla's cap stands.
/// </summary>
[HarmonyPatch(typeof(TournamentBehavior), nameof(TournamentBehavior.GetMaximumBet))]
[HarmonyPatchCategory(TournamentRewardsModule.PatchCategory)]
public static class Patch96_TournamentMaxBet
{
    private static TournamentRewardsService? _service;

    [HarmonyPostfix]
    public static void Postfix(ref int __result)
    {
        try
        {
            __result = (_service ??= TAOM.IoC.Resolve<TournamentRewardsService>()).MaximumBet(__result);
        }
        catch (Exception)
        {
            // Vanilla's cap stands; the bet UI keeps working.
        }
    }
}

/// <summary>
/// Patch96: notes how many heroes fought the tournament that just finished, before vanilla's handler asks the
/// model for the winner's renown and influence (<c>TournamentCampaignBehavior.OnTournamentFinished</c>, private,
/// v1.5.3:160). The model is asked by town only, at the award and again for the winner panel, so the count is
/// noted per town. Both callers of the event reach this handler: a played or watched tournament
/// (<c>TournamentBehavior.EndCurrentMatch</c>) and an off-screen one (<c>TournamentManager.ResolveTournament</c>).
/// </summary>
[HarmonyPatch(typeof(TournamentCampaignBehavior), "OnTournamentFinished")]
[HarmonyPatchCategory(TournamentRewardsModule.PatchCategory)]
public static class Patch96_TournamentFinishedHeroCount
{
    private static TournamentRewardsService? _service;

    [HarmonyPrefix]
    public static void Prefix(MBReadOnlyList<CharacterObject> participants, Town town)
    {
        try
        {
            (_service ??= TAOM.IoC.Resolve<TournamentRewardsService>())
                .NoteTournamentFinished(town?.Settlement?.StringId, participants?.Count(p => p != null && p.IsHero) ?? 0);
        }
        catch (Exception)
        {
            // No count: the rewards scale by zero heroes, which is vanilla plus the culture factor.
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
