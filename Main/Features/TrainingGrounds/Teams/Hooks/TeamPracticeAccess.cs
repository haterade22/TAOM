using System;
using System.Collections.Generic;
using HarmonyLib;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.TrainingGrounds.Teams.Hooks;

/// <summary>
/// #1528: the two private fields of vanilla's <see cref="ArenaPracticeFightMissionController"/> the team fight needs,
/// resolved once. <c>_AIParticipantTeams</c> is the six AI teams <c>InitializeTeams</c> builds (:358-365);
/// <c>_aliveOpponentCount</c> is the count of fighters in the yard, which <c>RemainingOpponentCount</c> reads (:69).
/// A field that fails to resolve leaves the fight vanilla: <see cref="IsAvailable"/> is false and the patches stand down.
/// Both are rows of <c>reflection-sites.md</c> Category B, pinned by <c>ReflectionSiteBindingTests</c>. The fields are
/// bound through the <c>FieldInfo</c> overload because BUTR's BHA0006 compares the generic <c>List&lt;Team&gt;</c> with
/// the open <c>List&lt;T&gt;</c> and warns on the string form.
/// </summary>
internal static class TeamPracticeAccess
{
    private static readonly AccessTools.FieldRef<ArenaPracticeFightMissionController, List<Team>>? Teams;
    private static readonly AccessTools.FieldRef<ArenaPracticeFightMissionController, int>? Alive;

    static TeamPracticeAccess()
    {
        try
        {
            Teams = AccessTools.FieldRefAccess<ArenaPracticeFightMissionController, List<Team>>(
                AccessTools.Field(typeof(ArenaPracticeFightMissionController), "_AIParticipantTeams"));
            Alive = AccessTools.FieldRefAccess<ArenaPracticeFightMissionController, int>(
                AccessTools.Field(typeof(ArenaPracticeFightMissionController), "_aliveOpponentCount"));
        }
        catch (Exception ex)
        {
            Teams = null;
            Alive = null;
            Outcomes.Swallowed("TeamPractice", "TeamPracticeAccess.Resolve", ex);
        }
    }

    internal static bool IsAvailable => Teams != null && Alive != null;

    /// <summary>The six AI teams, or null when the field did not resolve.</summary>
    internal static List<Team>? AiTeams(ArenaPracticeFightMissionController controller) => Teams?.Invoke(controller);

    /// <summary>The fighters alive in the yard. Call only when <see cref="IsAvailable"/>.</summary>
    internal static ref int AliveCount(ArenaPracticeFightMissionController controller) => ref Alive!.Invoke(controller);
}
