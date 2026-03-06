using System.Collections.Generic;

namespace TAOM.Adapters;

public readonly struct ClanBannerInfo
{
    public string StringId { get; }
    public string BannerCode { get; }
    public bool IsRulingClan { get; }

    public ClanBannerInfo(string stringId, string bannerCode, bool isRulingClan)
    {
        StringId = stringId;
        BannerCode = bannerCode;
        IsRulingClan = isRulingClan;
    }
}

public interface IClanBannerAdapter
{
    IReadOnlyList<ClanBannerInfo> GetAllClans();
    void SetBanner(string clanStringId, string bannerCode);
    void InvalidateVisuals(string clanStringId);
    string GetPlayerClanId();
}
