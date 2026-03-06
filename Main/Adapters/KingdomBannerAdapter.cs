using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

public class KingdomBannerAdapter : IKingdomBannerAdapter
{
    public IReadOnlyList<KingdomBannerInfo> GetAllKingdoms()
    {
        return Kingdom.All
            .Select(k => new KingdomBannerInfo(k.StringId, k.Banner?.BannerCode ?? string.Empty))
            .ToList();
    }

    public void SetBanner(string kingdomStringId, string bannerCode)
    {
        var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomStringId);
        if (kingdom != null)
        {
            kingdom.Banner = new Banner(bannerCode);
        }
    }

    public void InvalidateVisuals(string kingdomStringId)
    {
        var kingdom = Kingdom.All.FirstOrDefault(k => k.StringId == kingdomStringId);
        if (kingdom == null) return;

        foreach (var clan in kingdom.Clans)
        {
            foreach (var party in clan.WarPartyComponents)
            {
                party.MobileParty?.Party?.SetVisualAsDirty();
            }
        }
    }
}
