using System.Collections.Generic;

namespace TAOM.Features.LordSpawnGuard;

/// <summary>Why vanilla's caravan template pick fails; <see cref="None"/> when it resolves.</summary>
public enum CaravanPickFailure
{
    None,

    /// <summary>The merchant is in no settlement, which the predicate reads unguarded.</summary>
    NoCurrentSettlement,

    /// <summary>The list holds a null entry or a template with no data; the predicate throws on it.</summary>
    BrokenTemplateEntry,

    /// <summary>The list is missing or empty, or none of its templates fits the settlement.</summary>
    NoTemplateFits,
}

/// <summary>
/// The rule behind <c>Patch147_CaravanTemplateGuard</c> (#482): does vanilla's caravan template
/// pick fail for this merchant?
///
/// Vanilla <c>CaravansCampaignBehavior.SpawnCaravan</c> (v1.5.7, lines 495-506) first chooses a
/// list, the elite one when <c>GetEliteCaravanSpawnChance(hero) &gt; hero.RandomFloat()</c>. It then
/// picks a template with <c>GetRandomElementWithPredicate(x =&gt; x.ShipHulls.Count == 0 !=
/// hero.CurrentSettlement.HasPort)</c> and reads <c>.ShipHulls</c> on the result with no null check.
/// The helper runs the predicate on every entry and returns null, not an exception, when the list
/// is empty or no entry matches, so a culture whose templates are all land templates matches
/// nothing while its merchant is in a port town. <see cref="Judge"/> repeats those two lines on
/// plain values, so the patch can tell before vanilla runs.
/// </summary>
public static class CaravanTemplateGuard
{
    /// <summary>
    /// Mirrors vanilla's list choice. <c>hero.RandomFloat()</c> is the hero's saved
    /// <c>RandomValue</c> divided by 2^31, so one hero gets the same list for the same chance.
    /// </summary>
    public static bool ReadsEliteList(float eliteSpawnChance, float heroRandomFloat) =>
        eliteSpawnChance > heroRandomFloat;

    /// <summary>
    /// Judges the pick on the list vanilla reads. Every result but <see cref="CaravanPickFailure.None"/>
    /// means vanilla throws before it changes any state.
    /// </summary>
    /// <param name="eliteSpawnChance">The caravan model's elite chance for the hero.</param>
    /// <param name="heroRandomFloat">The hero's <c>RandomFloat()</c>.</param>
    /// <param name="hasCurrentSettlement">The hero is in a settlement.</param>
    /// <param name="settlementHasPort">That settlement has a port.</param>
    /// <param name="normalHullCounts">ShipHulls.Count per normal template; a null entry for a
    /// template with no data; null if the list is missing.</param>
    /// <param name="eliteHullCounts">The same for the elite list.</param>
    /// <param name="readsEliteList">Which list vanilla reads.</param>
    public static CaravanPickFailure Judge(
        float eliteSpawnChance,
        float heroRandomFloat,
        bool hasCurrentSettlement,
        bool settlementHasPort,
        IEnumerable<int?> normalHullCounts,
        IEnumerable<int?> eliteHullCounts,
        out bool readsEliteList)
    {
        readsEliteList = ReadsEliteList(eliteSpawnChance, heroRandomFloat);
        var hullCounts = readsEliteList ? eliteHullCounts : normalHullCounts;

        var any = false;
        var broken = false;
        var match = false;
        if (hullCounts != null)
        {
            foreach (var hulls in hullCounts)
            {
                any = true;
                if (hulls == null)
                    broken = true;
                else if ((hulls == 0) != settlementHasPort)
                    match = true;
            }
        }

        // An empty list returns null before the predicate runs; otherwise the predicate runs on
        // every entry and reads the entry's hulls, then the settlement.
        if (!any)
            return CaravanPickFailure.NoTemplateFits;
        if (!hasCurrentSettlement)
            return CaravanPickFailure.NoCurrentSettlement;
        if (broken)
            return CaravanPickFailure.BrokenTemplateEntry;
        return match ? CaravanPickFailure.None : CaravanPickFailure.NoTemplateFits;
    }
}
