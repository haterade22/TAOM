using System;
using TaleWorlds.CampaignSystem;
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

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
