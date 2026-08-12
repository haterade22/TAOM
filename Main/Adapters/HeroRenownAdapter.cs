using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <inheritdoc />
public sealed class HeroRenownAdapter : IHeroRenownAdapter
{
    private readonly IModLogger _logger;

    public HeroRenownAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public bool AddClanRenown(string heroId, int amount)
    {
        if (string.IsNullOrEmpty(heroId) || amount <= 0)
            return false;

        try
        {
            var hero = FindHero(heroId);
            if (hero?.Clan == null)
                return false;

            // GainRenownAction, NOT Clan.Renown += n. The action dispatches
            // CampaignEventDispatcher.OnRenownGained, which is what clan-tier progression, the
            // notification feed and any listening mod are hung off. ServeAsSoldier writes the
            // property directly and every one of those listeners silently misses the gain.
            GainRenownAction.Apply(hero, amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] AddClanRenown('{heroId}', {amount}) failed: {ex.Message}");
            return false;
        }
    }

    private static Hero FindHero(string heroId)
    {
        foreach (var hero in Hero.AllAliveHeroes)
        {
            if (hero?.StringId == heroId)
                return hero;
        }
        return null;
    }
}
