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

    /// <summary>
    /// The ONE unmet requirement worth showing the player, or null when nothing is unmet (or the
    /// ladder is exhausted). Deliberately a single string rather than the list above: the status
    /// board's value equality is its refresh throttle, and a <c>List&lt;string&gt;</c> compares by
    /// reference, so carrying the list would make the board differ on every rebuild and re-push the
    /// menu text every tick.
    /// </summary>
    public string MostBindingUnmetKey { get; set; }

    /// <summary>The threshold <see cref="MostBindingUnmetKey"/> has to reach, so the UI never
    /// re-derives a number the ladder already owns.</summary>
    public int MostBindingUnmetTarget { get; set; }
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

        // Locals rather than fields on the result: the running "best" is scratch state for the
        // scan, not part of the verdict. Consider() is a non-escaping local function, so the
        // capture compiles to a by-ref struct — no per-evaluation heap allocation on a path the
        // status board calls twice a second.
        string bestKey = null;
        var bestShortfall = 0;
        var bestScale = 1;

        // "Most binding" = the largest shortfall RELATIVE to its own threshold. The five gates are
        // on wildly different scales (60 days vs 800 XP vs trust 6), so a raw difference would name
        // the XP gate every single time; 400-of-800 XP (50%) rightly outranks 1-of-60 days (1.7%).
        //
        // TIE-BREAK: strictly-greater comparison, so on an exact tie the EARLIER call below wins —
        // i.e. the fixed order days > xp > leadership > dutySuccesses > trust. That makes the pick
        // stable for identical input, which the board's value-equality throttle depends on.
        //
        // Integer-only on purpose: cross-multiplication instead of division means no float exists
        // here to be NaN, and no divide-by-zero when a threshold is 0 (the first rank step requires
        // no Leadership at all) or negative (MinTrust defaults to -10). A non-positive threshold
        // degenerates to scale 1, i.e. an absolute shortfall — correct, because being below an
        // already-forgiving floor genuinely is the most binding thing on the list.
        void Consider(string key, int current, int threshold)
        {
            if (current >= threshold)
                return;

            result.UnmetRequirementKeys.Add(key);

            var shortfall = threshold - current;                 // >= 1, guaranteed by the guard
            var scale = threshold > 0 ? threshold : 1;
            if (bestKey != null && (long)shortfall * bestScale <= (long)bestShortfall * scale)
                return;

            bestKey = key;
            bestShortfall = shortfall;
            bestScale = scale;
            result.MostBindingUnmetKey = key;
            result.MostBindingUnmetTarget = threshold;
        }

        Consider("days", progress.DaysServed, requirement.MinDaysServed);
        Consider("xp", progress.ServiceXp, requirement.MinServiceXp);
        Consider("leadership", progress.LeadershipSkill, requirement.MinLeadershipSkill);
        Consider("dutySuccesses", progress.DutySuccesses, requirement.MinDutySuccesses);
        Consider("trust", progress.Trust, requirement.MinTrust);

        result.Promote = result.UnmetRequirementKeys.Count == 0;
        return result;
    }
}
