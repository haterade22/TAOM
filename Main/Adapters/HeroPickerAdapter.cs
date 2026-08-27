using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Features.UncapturableHeroes;

namespace TAOM.Adapters;

/// <inheritdoc cref="IHeroPickerAdapter"/>
/// <remarks>
/// One pass over Hero.AllAliveHeroes, plus one lookup of the culture's ruling house. Deliberately
/// over-returns: it tags relationships and leaves every eligibility judgement to HeroPickerService,
/// which is the half that can be unit tested.
///
/// No DeadOrDisabledHeroes union. The old LOTRAOM feature unioned the two sets to catch
/// not-yet-spawned wanderers, but CampaignObjectManager.OnHeroAdded buckets into
/// DeadOrDisabledHeroes only for Dead or Disabled, so a NotSpawned wanderer is already in
/// AllAliveHeroes and the union only ever risked offering genuinely dead heroes.
/// </remarks>
public class HeroPickerAdapter : IHeroPickerAdapter
{
    private readonly IUncapturableRegistry _uncapturable;

    public HeroPickerAdapter(IUncapturableRegistry uncapturable)
    {
        _uncapturable = uncapturable;
    }

    public IReadOnlyList<PickableHeroInfo> GetCandidates(string cultureId)
    {
        var results = new List<PickableHeroInfo>();

        if (string.IsNullOrEmpty(cultureId) || Campaign.Current == null)
            return results;

        var rulingLeader = FindRulingLeader(cultureId);
        var rulerSpouseId = rulingLeader?.Spouse?.StringId ?? string.Empty;
        var rulerChildIds = CollectChildIds(rulingLeader);

        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero?.CharacterObject == null)
                continue;
            if (!string.Equals(hero.Culture?.StringId, cultureId, StringComparison.Ordinal))
                continue;

            var clan = hero.Clan;
            var race = hero.CharacterObject.Race;

            results.Add(new PickableHeroInfo(
                heroId: hero.StringId,
                name: hero.Name?.ToString() ?? string.Empty,
                cultureId: cultureId,
                clanId: clan?.StringId ?? string.Empty,
                race: race,
                isFemale: hero.IsFemale,
                isChild: hero.IsChild,
                isWanderer: hero.IsWanderer,
                isNotable: hero.IsNotable,
                isMainHero: hero == Hero.MainHero,
                isClanLeader: clan?.Leader == hero,
                isKingdomLeader: rulingLeader != null && rulingLeader == hero,
                isSpouseOfKingdomLeader:
                    rulerSpouseId.Length > 0 &&
                    string.Equals(rulerSpouseId, hero.StringId, StringComparison.Ordinal),
                isChildOfKingdomLeader: rulerChildIds.Contains(hero.StringId),
                isLoreLocked: _uncapturable.IsUncapturable(hero.StringId, race)));
        }

        return results;
    }

    /// <summary>
    /// The leader of the kingdom whose culture this is. Null for a culture with no kingdom, which
    /// is normal: 19 of TAOM's 39 cultures field no ruling house, and the group simply renders empty.
    /// </summary>
    private static Hero? FindRulingLeader(string cultureId)
    {
        foreach (var kingdom in Campaign.Current.Kingdoms)
        {
            if (kingdom == null || kingdom.IsEliminated)
                continue;
            if (!string.Equals(kingdom.Culture?.StringId, cultureId, StringComparison.Ordinal))
                continue;

            var leader = kingdom.Leader;
            if (leader != null)
                return leader;
        }

        return null;
    }

    private static HashSet<string> CollectChildIds(Hero? leader)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (leader == null)
            return ids;

        var children = leader.Children;
        if (children == null)
            return ids;

        foreach (var child in children)
        {
            if (child != null)
                ids.Add(child.StringId);
        }

        return ids;
    }
}
