using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class CommanderLordAdapter : ICommanderLordAdapter
{
    private readonly IModLogger _logger;

    public CommanderLordAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public CommanderSnapshot GetSnapshot(string heroId)
    {
        try
        {
            var hero = FindHero(heroId);
            if (hero == null)
                return CommanderSnapshot.Missing;

            var party = hero.PartyBelongedTo;
            return new CommanderSnapshot(
                exists: true,
                isAlive: hero.IsAlive,
                isPrisoner: hero.IsPrisoner,
                partyId: party?.StringId,
                partyIsActive: party?.IsActive ?? false,
                partyIsInMapEvent: party?.MapEvent != null,
                partyIsInSettlement: party?.CurrentSettlement != null,
                settlementId: hero.CurrentSettlement?.StringId,
                cultureId: hero.Culture?.StringId,
                factionId: hero.Clan?.MapFaction?.StringId,
                name: hero.Name?.ToString());
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CommanderLordAdapter.GetSnapshot('{heroId}') failed: {ex.Message}");
            return CommanderSnapshot.Missing;
        }
    }

    public string GetCultureId(string heroId)
    {
        try
        {
            return FindHero(heroId)?.Culture?.StringId;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CommanderLordAdapter.GetCultureId('{heroId}') failed: {ex.Message}");
            return null;
        }
    }

    public bool IsLord(string heroId)
    {
        try
        {
            return FindHero(heroId)?.IsLord ?? false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CommanderLordAdapter.IsLord('{heroId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool IsAtWarWithFaction(string heroId, string factionId)
    {
        try
        {
            var heroFaction = FindHero(heroId)?.Clan?.MapFaction;
            if (heroFaction == null || string.IsNullOrEmpty(factionId))
                return false;

            var other = Campaign.Current?.CampaignObjectManager?.Find<Kingdom>(factionId)
                ?? (IFaction)Campaign.Current?.CampaignObjectManager?.Find<Clan>(factionId);
            if (other == null)
                return false;

            return heroFaction.IsAtWarWith(other);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CommanderLordAdapter.IsAtWarWithFaction('{heroId}', '{factionId}') failed: {ex.Message}");
            return false;
        }
    }

    public bool ApplyPlayerRelation(string heroId, int delta)
    {
        if (delta == 0)
            return true;
        try
        {
            var hero = FindHero(heroId);
            if (hero == null)
                return false;
            ChangeRelationAction.ApplyPlayerRelation(hero, delta, affectRelatives: true, showQuickNotification: false);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] ApplyPlayerRelation('{heroId}', {delta}) failed: {ex.Message}");
            return false;
        }
    }

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
