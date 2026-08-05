using System.Collections.Generic;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>Result of a promotion check. UnmetRequirementKeys drive the progress text — one data source for logic AND UI.</summary>
public sealed class PromotionEvaluation
{
    public bool Promote { get; set; }
    public ServiceRank ToRank { get; set; }
    public List<string> UnmetRequirementKeys { get; } = new List<string>();

    public bool AtTopRank { get; set; }
}

/// <summary>
/// Pure promotion evaluation over the config ladder. Exactly two call points exist by
/// design (daily grant + battle end) — the donor evaluated from 12 sites including
/// per-kill mid-mission, which is the bug class this shape removes.
/// </summary>
public static class PromotionEvaluator
{
    public static PromotionEvaluation Evaluate(ServiceProgressSnapshot progress, IReadOnlyList<PromotionRequirement> ladder)
    {
        var result = new PromotionEvaluation();
        if (progress == null || ladder == null)
            return result;

        var nextIndex = (int)progress.Rank;
        if (nextIndex >= ladder.Count)
        {
            result.AtTopRank = true;
            return result;
        }

        var requirement = ladder[nextIndex];
        result.ToRank = requirement.ToRank;

        if (progress.DaysServed < requirement.MinDaysServed)
            result.UnmetRequirementKeys.Add("days");
        if (progress.ServiceXp < requirement.MinServiceXp)
            result.UnmetRequirementKeys.Add("xp");
        if (progress.LeadershipSkill < requirement.MinLeadershipSkill)
            result.UnmetRequirementKeys.Add("leadership");
        if (progress.DutySuccesses < requirement.MinDutySuccesses)
            result.UnmetRequirementKeys.Add("dutySuccesses");
        if (progress.Trust < requirement.MinTrust)
            result.UnmetRequirementKeys.Add("trust");

        result.Promote = result.UnmetRequirementKeys.Count == 0;
        return result;
    }
}
