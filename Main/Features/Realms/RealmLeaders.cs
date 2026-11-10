using TaleWorlds.CampaignSystem;
using TAOM.Features.Realms.Domain.Services;

namespace TAOM.Features.Realms;

/// <summary>Answers who leads a clan or rules a kingdom right now.</summary>
internal sealed class RealmLeaders : IRealmLeaders
{
    public string? ClanLeaderOf(string clanId) => FindClan(clanId)?.Leader?.StringId;

    public bool ClanExists(string clanId)
    {
        var clan = FindClan(clanId);
        return clan != null && !clan.IsEliminated;
    }

    public string? RulerOf(string kingdomId) => FindKingdom(kingdomId)?.Leader?.StringId;

    public bool KingdomExists(string kingdomId)
    {
        var kingdom = FindKingdom(kingdomId);
        return kingdom != null && !kingdom.IsEliminated;
    }

    private static Clan? FindClan(string id) =>
        TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Clan>(id);

    private static Kingdom? FindKingdom(string id) =>
        TaleWorlds.CampaignSystem.Campaign.Current?.CampaignObjectManager.Find<Kingdom>(id);
}
