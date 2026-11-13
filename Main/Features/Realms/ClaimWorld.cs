using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>
/// The live campaign as the claim rules see it. Lookups only: nothing is cached but the neighbor table and, during a
/// sweep (<see cref="BeginSweep"/>), which hero object an id names. A hero's clan, realm and life are always read live.
/// </summary>
internal sealed class ClaimWorld : IOfficeWorld
{
    private FiefNeighbors? _neighbors;
    private int _neighborCount;

    /// <summary>The heroes by id while a sweep runs; null otherwise.</summary>
    private Dictionary<string, Hero?>? _heroes;

    /// <summary>
    /// Starts a sweep: until <see cref="EndSweep"/> heroes are found in a table built once, not by
    /// <see cref="Hero.Find"/>, which walks every hero the campaign ever had. The weekly rules ask for thousands of
    /// heroes per clan, so this is what keeps the weekly tick from freezing the map. Ids it does not know (a hero made
    /// during the sweep) still fall back to <see cref="Hero.Find"/>.
    /// </summary>
    public void BeginSweep()
    {
        var campaign = TaleWorlds.CampaignSystem.Campaign.Current;
        _heroes = campaign == null
            ? null
            : IndexFirst<Hero?>(campaign.DeadOrDisabledHeroes.Concat(campaign.AliveHeroes), h => h?.StringId);
    }

    public void EndSweep() => _heroes = null;

    /// <summary>
    /// Each id's first item, in order: the same one a front-to-back search finds (the campaign registers dead and
    /// disabled heroes before the living, and <see cref="Hero.Find"/> walks them in that order). Items without an id are left out.
    /// </summary>
    internal static Dictionary<string, T> IndexFirst<T>(IEnumerable<T> items, Func<T, string?> idOf)
    {
        var index = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var id = idOf(item);
            if (id != null && !index.ContainsKey(id))
                index[id] = item;
        }

        return index;
    }

    private Hero? FindHero(string heroId)
    {
        if (_heroes == null || heroId == null)
            return Hero.Find(heroId);
        if (_heroes.TryGetValue(heroId, out var hero))
            return hero;
        return _heroes[heroId] = Hero.Find(heroId);
    }

    public bool IsAlive(string heroId) => FindHero(heroId)?.IsAlive == true;

    public string? RulerOf(string kingdomId)
    {
        var kingdom = TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(kingdomId);
        return kingdom == null || kingdom.IsEliminated ? null : kingdom.Leader?.StringId;
    }

    public string? OwnerOf(string settlementId) => Landlord(settlementId)?.Owner?.StringId;

    public string? KingdomOfSettlement(string settlementId) => Landlord(settlementId)?.OwnerClan?.Kingdom?.StringId;

    public string? KingdomOfHero(string heroId) => FindHero(heroId)?.Clan?.Kingdom?.StringId;

    public string? ClanOf(string heroId) => FindHero(heroId)?.Clan?.StringId;

    public string? CultureOf(string heroId) => FindHero(heroId)?.Culture?.StringId;

    /// <summary>Towns and castles never move, so the table is built once per session (again if the count changes).</summary>
    public FiefNeighbors Neighbors(int count)
    {
        if (_neighbors != null && _neighborCount == count)
            return _neighbors;
        _neighborCount = count;
        return _neighbors = FiefNeighbors.Build(
            Settlement.All.Where(s => s != null && (s.IsTown || s.IsCastle))
                .Select(s => (s.StringId, s.GetPosition2D.x, s.GetPosition2D.y)),
            count);
    }

    /// <summary>
    /// The hero as a buyer: their clan, realm and the towns and castles they reach claims from. A ruler reaches from every
    /// town and castle of the realm, since they hand fiefs to vassal clans; anyone else from their own clan's.
    /// </summary>
    public static ClaimBuyer BuyerOf(Hero hero)
    {
        var clan = hero.Clan;
        var kingdom = clan?.Kingdom;
        var lands = kingdom != null && kingdom.Leader == hero ? kingdom.Settlements : clan?.Settlements;
        var fiefs = lands?.Where(s => s != null && (s.IsTown || s.IsCastle)).Select(s => s.StringId).ToList()
                    ?? new System.Collections.Generic.List<string>();
        return new ClaimBuyer(hero.StringId, clan?.StringId, kingdom?.StringId, fiefs);
    }

    /// <summary>A village belongs to whoever holds its town or castle.</summary>
    private static Settlement? Landlord(string settlementId)
    {
        var settlement = Settlement.Find(settlementId);
        return settlement?.IsVillage == true ? settlement.Village?.Bound ?? settlement : settlement;
    }
}
