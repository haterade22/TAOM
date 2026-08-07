using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Settlements;
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
                settlementMenuId: MenuIdFor(hero.CurrentSettlement),
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

    // One-slot cache used only to mint a stable-within-a-battle identity for a MapEvent. The engine
    // gives map events no identity of its own: MapEvent derives from MBObjectBase but every
    // instance is `new`'d and never registered with MBObjectManager, so StringId/Id are unset.
    // One slot suffices because a pass asks about exactly one party (the commander), so A->B->A
    // alternation cannot occur inside a pass. The single retained finished-MapEvent reference is
    // the accepted cost; MapEvent never crosses the adapter boundary, only the int.
    private MapEvent _lastSeenMapEvent;
    private int _lastMapEventToken;
    private int _nextMapEventToken = 1;

    private int TokenFor(MapEvent mapEvent)
    {
        if (mapEvent == null)
            return 0;
        if (!ReferenceEquals(mapEvent, _lastSeenMapEvent))
        {
            _lastSeenMapEvent = mapEvent;
            _lastMapEventToken = _nextMapEventToken++;
        }
        return _lastMapEventToken;
    }

    public CommanderTickSnapshot GetTickSnapshot(string heroId)
    {
        try
        {
            var hero = FindHero(heroId);
            if (hero == null)
                return CommanderTickSnapshot.Missing;

            var party = hero.PartyBelongedTo;
            return new CommanderTickSnapshot(
                exists: true,
                isAlive: hero.IsAlive,
                isPrisoner: hero.IsPrisoner,
                partyId: party?.StringId,
                partyIsActive: party?.IsActive ?? false,
                partyIsInMapEvent: party?.MapEvent != null,
                partySettlementId: party?.CurrentSettlement?.StringId,
                mapEventToken: TokenFor(party?.MapEvent));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetTickSnapshot('{heroId}') failed: {ex.Message}");
            return CommanderTickSnapshot.Missing;
        }
    }

    public string GetPartyId(string heroId)
    {
        try
        {
            return FindHero(heroId)?.PartyBelongedTo?.StringId;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] CommanderLordAdapter.GetPartyId('{heroId}') failed: {ex.Message}");
            return null;
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

    /// <summary>The vanilla menu id for a settlement, so a discharge lands the player somewhere usable.</summary>
    private static string MenuIdFor(Settlement settlement)
    {
        if (settlement == null) return null;
        if (settlement.IsTown) return "town";
        if (settlement.IsCastle) return "castle";
        if (settlement.IsVillage) return "village";
        return null;
    }

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
