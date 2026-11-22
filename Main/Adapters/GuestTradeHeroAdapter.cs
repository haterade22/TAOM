using System;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.TroopWeight;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGuestTradeHeroAdapter"/> (#1329): reads a guest's hero on the host and
/// builds the per-hero player and party adapters the emissary and armoury services take.
/// </summary>
public sealed class GuestTradeHeroAdapter : IGuestTradeHeroAdapter
{
    private readonly ISettlementOwnerAdapter _owners;
    private readonly ITroopWeightService _troopWeight;
    private readonly IModLogger _logger;

    public GuestTradeHeroAdapter(ISettlementOwnerAdapter owners, ITroopWeightService troopWeight, IModLogger logger)
    {
        _owners = owners;
        _troopWeight = troopWeight;
        _logger = logger;
    }

    public GuestTradeHero? Find(string heroId)
    {
        // Hero.Find dereferences Campaign.Current unguarded: go through the object manager only when there is one.
        var manager = Campaign.Current?.CampaignObjectManager;
        var hero = string.IsNullOrEmpty(heroId) || manager == null ? null : manager.Find<Hero>(heroId);
        if (hero == null)
            return null;

        var party = hero.PartyBelongedTo;
        var isFreeLeader = hero.IsAlive && !hero.IsPrisoner && party != null && party.IsActive && party.LeaderHero == hero;
        var settlement = party?.CurrentSettlement;
        // MobileParty.SiegeEvent covers only the besieging side, so a defender is caught by the settlement's siege.
        var inBattle = party != null && (party.MapEvent != null || party.SiegeEvent != null || settlement?.IsUnderSiege == true);
        var settlementFaction = settlement?.MapFaction;
        var heroFaction = hero.MapFaction;
        var atWar = settlementFaction != null && heroFaction != null && settlementFaction.IsAtWarWith(heroFaction);

        return new GuestTradeHero(
            hero.StringId,
            isFreeLeader,
            settlement?.StringId,
            inBattle,
            atWar,
            settlement == null ? null : _owners.GetOwnerInfo(settlement),
            ArmouryPlayerAdapter.ForHero(hero),
            PlayerPartyAdapter.ForHero(hero, _logger, _troopWeight),
            hero.IsAlive);
    }
}
