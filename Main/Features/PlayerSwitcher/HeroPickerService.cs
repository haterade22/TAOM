using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <inheritdoc cref="IHeroPickerService"/>
/// <remarks>
/// All policy, no engine. The adapter is allowed to over-return; every rule about who may be
/// taken over lives here so it can be proven without a running campaign.
/// </remarks>
public class HeroPickerService : IHeroPickerService
{
    private static readonly string[] PlaceholderMarkers = { "place holder", "placeholder" };

    private readonly IHeroPickerAdapter _adapter;

    public HeroPickerService(IHeroPickerAdapter adapter)
    {
        _adapter = adapter;
    }

    public HeroPickList BuildPickList(string cultureId, PlayerSwitchPolicy policy)
    {
        if (!policy.Enabled || string.IsNullOrEmpty(cultureId))
            return HeroPickList.Empty;

        var candidates = _adapter.GetCandidates(cultureId);
        if (candidates == null || candidates.Count == 0)
            return HeroPickList.Empty;

        var rulingHouse = new List<HeroPickRow>();
        var clanLeaders = new List<HeroPickRow>();
        var wanderers = new List<HeroPickRow>();

        // One hero appears once, in the first group that claims them. The ruling house wins over
        // the clan-leader list, which is why a ruler who also leads their clan is not listed twice.
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        // Pass 1 places the ruler at the head of their own house. Doing this as a separate pass
        // rather than sorting afterwards keeps the remaining rows in adapter order, which is the
        // order the campaign itself enumerates them in.
        foreach (var hero in candidates)
        {
            if (!IsEligible(hero, cultureId, policy) || !hero.IsKingdomLeader)
                continue;
            if (claimed.Add(hero.HeroId))
                rulingHouse.Add(ToRow(hero, HeroPickerGroup.RulingHouse));
        }

        foreach (var hero in candidates)
        {
            if (!IsEligible(hero, cultureId, policy))
                continue;
            if (claimed.Contains(hero.HeroId))
                continue;

            if (hero.IsSpouseOfKingdomLeader || hero.IsChildOfKingdomLeader)
            {
                if (claimed.Add(hero.HeroId))
                    rulingHouse.Add(ToRow(hero, HeroPickerGroup.RulingHouse));
            }
            else if (hero.IsClanLeader)
            {
                if (claimed.Add(hero.HeroId))
                    clanLeaders.Add(ToRow(hero, HeroPickerGroup.ClanLeaders));
            }
            else if (hero.IsWanderer && policy.IncludeWanderers)
            {
                if (claimed.Add(hero.HeroId))
                    wanderers.Add(ToRow(hero, HeroPickerGroup.Wanderers));
            }
        }

        return new HeroPickList(rulingHouse, clanLeaders, wanderers);
    }

    private static bool IsEligible(PickableHeroInfo hero, string cultureId, PlayerSwitchPolicy policy)
    {
        if (string.IsNullOrEmpty(hero.HeroId))
            return false;
        if (!string.Equals(hero.CultureId, cultureId, StringComparison.Ordinal))
            return false;

        // You cannot take over yourself, a child, or a notable. Notables anchor settlement
        // issues and quests; vanilla asserts when one is removed while holding an issue quest.
        if (hero.IsMainHero || hero.IsChild || hero.IsNotable)
            return false;

        // Sauron and the Nine. Patch76's two hooks both defer to vanilla when the hero is
        // Hero.MainHero, so a player-controlled dark lord silently loses the capture immunity
        // that docs/features/uncapturable-heroes.md promises. Opt-in only.
        if (hero.IsLoreLocked && !policy.AllowLoreLockedHeroes)
            return false;

        if (IsPlaceholder(hero.Name))
            return false;

        return true;
    }

    private static bool IsPlaceholder(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        foreach (var marker in PlaceholderMarkers)
        {
            if (name.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static HeroPickRow ToRow(PickableHeroInfo hero, HeroPickerGroup group)
        => new HeroPickRow(
            hero.HeroId,
            hero.Name,
            group,
            hero.Race,
            hero.IsFemale,
            hero.IsClanLeader,
            hasClan: !string.IsNullOrEmpty(hero.ClanId));
}
