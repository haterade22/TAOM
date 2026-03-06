using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct KingdomBannerInfo
{
    public string StringId { get; }
    public string BannerCode { get; }

    public KingdomBannerInfo(string stringId, string bannerCode)
    {
        StringId = stringId;
        BannerCode = bannerCode;
    }
}

public interface IKingdomBannerAdapter
{
    IReadOnlyList<KingdomBannerInfo> GetAllKingdoms();
    void SetBanner(string kingdomStringId, string bannerCode);
    void InvalidateVisuals(string kingdomStringId);
}
