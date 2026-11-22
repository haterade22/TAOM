using System;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Teams.Hooks;

namespace TAOM.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: two lines of the Training Master that vanilla words for a free-for-all ("There are no rules, no teams"):
/// his rules speech and his prize speech. Each is added at priority 110, above vanilla's 100, with vanilla's input and
/// output tokens and no consequence, so the conversation continues exactly as it does in vanilla. The engine takes the
/// highest-priority line whose condition holds (<c>ConversationManager.GetSentenceOptions</c> walks the sentences in
/// descending priority and returns the first match), and a line is true only while the current fight is a team fight, so
/// any other town keeps vanilla's line. Vanilla's tokens are read from <c>ArenaMasterCampaignBehavior.AddDialogs</c>.
/// </summary>
internal sealed class TeamPracticeDialogBehavior : CampaignBehaviorBase
{
    internal const int Priority = 110;
    internal const string RulesInput = "arena_practice_fight_rules";
    internal const string RulesOutput = "arena_intro_4";
    internal const string RewardInput = "arena_practice_fight_reward";
    internal const string RewardOutput = "arena_joining_ask";
    internal const string VanillaRewardCondition = "conversation_arena_practice_fight_explain_reward_on_condition";

    private static readonly Func<bool>? VanillaRewardVariables = ResolveVanillaRewardCondition();

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore)
    {
    }

    private static void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddDialogLine("taom_tg_team_rules", RulesInput, RulesOutput, TeamPracticeTexts.Rules, InTeamPractice, null, Priority);
        starter.AddDialogLine("taom_tg_team_reward", RewardInput, RewardOutput, TeamPracticeTexts.Reward, SetRewardVariables, null, Priority);
    }

    private static bool InTeamPractice()
    {
        try
        {
            var controller = Mission.Current?.GetMissionBehavior<ArenaPracticeFightMissionController>();
            return controller != null && TeamPracticeState.IsTeams(controller);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "TeamPracticeDialogBehavior.InTeamPractice", ex);
            return false;
        }
    }

    /// <summary>
    /// Vanilla's reward condition sets OPPONENT_COUNT_1 to 3 and PRIZE_1 to 5, and the next three pairs and PRIZE_5 (their numbers live there, not here), so the
    /// line's figures cannot drift from vanilla's payout. If it cannot be reached the line stays false and vanilla's speaks.
    /// </summary>
    private static bool SetRewardVariables()
    {
        if (!InTeamPractice())
            return false;
        try
        {
            if (VanillaRewardVariables == null || !VanillaRewardVariables())
                return false;
            TeamPracticeTexts.SetGoldIcon();
            return true;
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "TeamPracticeDialogBehavior.SetRewardVariables", ex);
            return false;
        }
    }

    private static Func<bool>? ResolveVanillaRewardCondition()
    {
        try
        {
            var method = AccessTools.Method(typeof(ArenaMasterCampaignBehavior), VanillaRewardCondition);
            return method == null ? null : (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), method);
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("TeamPractice", "TeamPracticeDialogBehavior.ResolveVanillaRewardCondition", ex);
            return null;
        }
    }
}
