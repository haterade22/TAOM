using System;
using System.Collections.Generic;
using TAOM.Features.LotrIssues.Domain;

namespace TAOM.Features.LotrIssues;

/// <summary>
/// Pure rules of the TrainRetainers loan: how many lent troops are still alive, when a battle counts, how the
/// return is graded and which troops go home. A static helper like <see cref="LotrIssueTemplateRules"/>: nothing
/// else implements or fakes it. The grades follow the vanilla retainer quest (all back is a total success, more
/// than half is partial, half or fewer is weak, none is a failure).
/// </summary>
internal static class LotrRetainerLoanRules
{
    /// <summary>
    /// The survivor count after a look at the party: never above the last known count and never below zero. A
    /// player's own troops of the same line can hide a death, but they can never raise the count.
    /// </summary>
    internal static int TrackSurvivors(int lastKnown, int lineCountInParty)
    {
        int count = Math.Min(lastKnown, lineCountInParty);
        return count < 0 ? 0 : count;
    }

    /// <summary>A battle counts when the player won it with at least one retainer still alive.</summary>
    internal static bool CountsAsTrainingWin(bool playerWon, int survivors) => playerWon && survivors > 0;

    internal static LoanReturnGrade Grade(int returned, int lent)
    {
        if (lent <= 0 || returned <= 0) return LoanReturnGrade.Failed;
        if (returned >= lent) return LoanReturnGrade.Total;
        return returned > lent * 0.5f ? LoanReturnGrade.Partial : LoanReturnGrade.Weak;
    }

    internal static int RelationFor(LoanReturnGrade grade)
    {
        switch (grade)
        {
            case LoanReturnGrade.Total: return 5;
            case LoanReturnGrade.Partial: return 3;
            case LoanReturnGrade.Weak: return 1;
            default: return -5;
        }
    }

    /// <summary>
    /// How many to take from each stack to send <paramref name="count"/> retainers home, parallel to
    /// <paramref name="stacks"/>: highest tier first, ties in list order, heroes never.
    /// </summary>
    internal static int[] PlanReturn(IReadOnlyList<LotrTroopStack>? stacks, int count)
    {
        if (stacks == null) return new int[0];
        var plan = new int[stacks.Count];
        int remaining = count > 0 ? count : 0;
        while (remaining > 0)
        {
            int best = -1;
            for (int i = 0; i < stacks.Count; i++)
            {
                if (stacks[i].IsHero || stacks[i].Number - plan[i] <= 0) continue;
                if (best < 0 || stacks[i].Tier > stacks[best].Tier) best = i;
            }
            if (best < 0) break;
            int take = Math.Min(remaining, stacks[best].Number - plan[best]);
            plan[best] += take;
            remaining -= take;
        }
        return plan;
    }
}
