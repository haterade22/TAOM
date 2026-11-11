using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>The live campaign as the claim rules see it. Lookups only, nothing is cached but the neighbor table.</summary>
internal sealed class ClaimWorld : IOfficeWorld
{
    private FiefNeighbors? _neighbors;
    private int _neighborCount;

    public bool IsAlive(string heroId) => Hero.Find(heroId)?.IsAlive == true;

    public string? RulerOf(string kingdomId)
    {
        var kingdom = TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(kingdomId);
        return kingdom == null || kingdom.IsEliminated ? null : kingdom.Leader?.StringId;
    }

    public string? OwnerOf(string settlementId) => Landlord(settlementId)?.Owner?.StringId;

    public string? KingdomOfSettlement(string settlementId) => Landlord(settlementId)?.OwnerClan?.Kingdom?.StringId;

    public string? KingdomOfHero(string heroId) => Hero.Find(heroId)?.Clan?.Kingdom?.StringId;

    public string? ClanOf(string heroId) => Hero.Find(heroId)?.Clan?.StringId;

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
