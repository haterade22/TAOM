using System;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// Patch174 (#1528), the second class of the category: after vanilla's post-fight condition has set the prize and the
/// opponent count and paid the gold, a won team fight says its own debrief. Vanilla's "last one standing" line
/// (<c>str_arena_take_down.6</c>) names a free-for-all. This postfix changes <c>FIGHT_DEBRIEF</c> and nothing else, and
/// only when <see cref="TeamPracticeRules.ShowsTeamWin"/> holds: a team fight that ended with no fighter left to enter or
/// stand (the same <c>RemainingOpponentCountFromLastPractice == 0</c> that vanilla pays 250 denars on) with the player
/// still standing (<c>IsPlayerSurvived</c>).
/// </summary>
[HarmonyPatch(typeof(ArenaMasterCampaignBehavior), "conversation_arena_master_post_fight_on_condition")]
[HarmonyPatchCategory(ArenaPracticeFightMissionController_TeamPractice_Patch.Category)]
public static class ArenaMasterCampaignBehavior_PostFightDebrief_Patch
{
    [HarmonyPostfix]
    public static void Postfix(bool __result)
    {
        if (!__result)
            return;
        try
        {
            var controller = Mission.Current?.GetMissionBehavior<ArenaPracticeFightMissionController>();
            if (controller == null || !TeamPracticeRules.ShowsTeamWin(TeamPracticeState.IsTeams(controller), controller.RemainingOpponentCountFromLastPractice, controller.IsPlayerSurvived))
                return;

            TeamPracticeTexts.SetGoldIcon();
            MBTextManager.SetTextVariable("FIGHT_DEBRIEF", TeamPracticeTexts.WinDebrief());
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "ArenaMasterCampaignBehavior.PostFightCondition", ex);
        }
    }
}
