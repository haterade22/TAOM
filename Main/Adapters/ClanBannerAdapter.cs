using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

public class ClanBannerAdapter : IClanBannerAdapter
{
    /// <summary>
    /// Each clan's own banner (<c>ClanOriginalBanner</c>), also for a ruling clan, whose <c>Banner</c> the engine hands its
    /// kingdom's banner unless Realms keeps the house banner apart. <see cref="SetBanner"/> writes that same own banner.
    /// </summary>
    public IReadOnlyList<ClanBannerInfo> GetAllClans()
    {
        return Clan.All
            .Select(c => new ClanBannerInfo(
                c.StringId,
                c.ClanOriginalBanner?.BannerCode ?? string.Empty,
                c.Kingdom?.RulingClan == c))
            .ToList();
    }

    public void SetBanner(string clanStringId, string bannerCode)
    {
        var clan = Clan.All.FirstOrDefault(c => c.StringId == clanStringId);
        if (clan != null)
        {
            clan.Banner = new Banner(bannerCode);
        }
    }

    public void InvalidateVisuals(string clanStringId)
    {
        var clan = Clan.All.FirstOrDefault(c => c.StringId == clanStringId);
        if (clan == null) return;

        foreach (var party in clan.WarPartyComponents)
        {
            party.MobileParty?.Party?.SetVisualAsDirty();
        }

        clan.Leader?.PartyBelongedTo?.Party?.SetVisualAsDirty();
    }

    public string GetPlayerClanId()
    {
        return Clan.PlayerClan?.StringId;
    }
}
