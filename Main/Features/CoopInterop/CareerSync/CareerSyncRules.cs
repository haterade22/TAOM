using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CoopInterop.CareerSync;

/// <summary>Why the co-op server refused a career a player reported, or <see cref="None"/>.</summary>
public enum CareerSyncRefusal
{
    None,
    Oversized,
    UnknownCareer,
    NotCleared,
    RootNotFirst,
    ChoiceNotAllowed,
    OverBudget,
    BadTier,
    UnknownFlag,
}

/// <summary>
/// The co-op server's check of a reported career, with TAOM's own career rules (docs/features/coop-interop.md,
/// "Career sync"). The choices are replayed in the order the player took them through
/// <see cref="CareerChoiceRules.CanTake"/>, against the budget <see cref="ICareerRegistry.GetMaxChoicesForHero"/>
/// gives the hero's level on the server. Tier unlocks and quest flags come from career quests that run on the
/// player's own machine, so the server can only bound them: tiers 1 to 3, flags that a quest reward names or the
/// career's tree-version stamp. Eligibility for the hero's culture is not checked here (the caller logs it).
/// </summary>
public static class CareerSyncRules
{
    /// <summary>Longest id accepted (hero, career, choice, flag).</summary>
    public const int MaxIdLength = 128;

    /// <summary>Most entries accepted in one list. The shipped careers allow at most 31 choices.</summary>
    public const int MaxEntries = 64;

    public static CareerSyncRefusal Check(CareerSyncSnapshot sent, ICareerRegistry registry, int heroLevel, ICollection<string> questFlags)
    {
        if (!Bounded(sent)) return CareerSyncRefusal.Oversized;

        if (string.IsNullOrEmpty(sent.CareerId))
            return sent.Choices.Count == 0 && sent.Tiers.Count == 0 && sent.Flags.Count == 0
                ? CareerSyncRefusal.None
                : CareerSyncRefusal.NotCleared;

        var career = registry.GetCareer(sent.CareerId!);
        if (career == null) return CareerSyncRefusal.UnknownCareer;

        // The book never holds a tier or a flag twice (UnlockTier and HeroCareerData.AddFlag skip a repeat).
        if (sent.Tiers.Any(tier => tier < 1 || tier > 3) || sent.Tiers.Distinct().Count() != sent.Tiers.Count) return CareerSyncRefusal.BadTier;
        if (sent.Flags.Distinct(StringComparer.Ordinal).Count() != sent.Flags.Count) return CareerSyncRefusal.UnknownFlag;

        var choiceRefusal = CheckChoices(sent, career, registry, heroLevel);
        if (choiceRefusal != CareerSyncRefusal.None) return choiceRefusal;

        var treeFlag = CareerTreeVersion.FlagFor(career.TreeVersion);
        foreach (var flag in sent.Flags)
        {
            if (career.TreeVersion >= 2 && flag == treeFlag) continue;
            if (questFlags != null && questFlags.Contains(flag)) continue;
            return CareerSyncRefusal.UnknownFlag;
        }

        return CareerSyncRefusal.None;
    }

    /// <summary>Every flag a career quest reward can grant (GrantAttributeFlag, CareerQuestService.ApplyRewards).</summary>
    public static HashSet<string> QuestFlags(IEnumerable<CareerQuestDefinition>? quests)
    {
        var flags = new HashSet<string>(StringComparer.Ordinal);
        if (quests == null) return flags;

        foreach (var reward in quests.Where(q => q?.Rewards != null).SelectMany(q => q.Rewards))
        {
            if (reward != null && reward.Type == CareerQuestRewardType.GrantAttributeFlag && !string.IsNullOrEmpty(reward.Param))
                flags.Add(reward.Param);
        }
        return flags;
    }

    private static CareerSyncRefusal CheckChoices(CareerSyncSnapshot sent, CareerDefinition career, ICareerRegistry registry, int heroLevel)
    {
        if (sent.Choices.Count == 0) return CareerSyncRefusal.None;
        if (sent.Choices[0] != career.RootChoiceId) return CareerSyncRefusal.RootNotFirst;

        var budget = registry.GetMaxChoicesForHero(career, heroLevel);
        if (sent.Choices.Count > budget) return CareerSyncRefusal.OverBudget;

        var held = new HeroCareerData(sent.HeroId) { CareerStringId = career.Id };
        held.ChoiceIds.Add(career.RootChoiceId);
        bool TierOpen(int tier) => registry.IsTierAvailable(heroLevel, tier) || sent.Tiers.Contains(tier);

        foreach (var choiceId in sent.Choices.Skip(1))
        {
            var choice = registry.GetChoice(choiceId);
            var reason = CareerChoiceRules.CanTake(career, choice, registry, held, budget - held.ChoiceIds.Count, TierOpen);
            if (reason == ChoiceLockReason.NoPoints) return CareerSyncRefusal.OverBudget;
            if (reason != ChoiceLockReason.None) return CareerSyncRefusal.ChoiceNotAllowed;
            held.ChoiceIds.Add(choiceId);
        }
        return CareerSyncRefusal.None;
    }

    private static bool Bounded(CareerSyncSnapshot sent) =>
        sent != null
        && IdOk(sent.HeroId)
        && (sent.CareerId == null || sent.CareerId.Length <= MaxIdLength)
        && ListOk(sent.Choices)
        && ListOk(sent.Flags)
        && sent.Tiers != null && sent.Tiers.Count <= MaxEntries;

    private static bool IdOk(string? id) => !string.IsNullOrEmpty(id) && id!.Length <= MaxIdLength;

    private static bool ListOk(IReadOnlyList<string>? list) => list != null && list.Count <= MaxEntries && list.All(IdOk);
}
