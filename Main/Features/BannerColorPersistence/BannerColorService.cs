using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public class BannerColorService : IBannerColorService
{
    private readonly IBannerColorConfigProvider _configProvider;

    public BannerColorService(IBannerColorConfigProvider configProvider)
    {
        _configProvider = configProvider;
    }

    public bool IsEnabled() => _configProvider.GetConfig().EnableColorPersistence;

    public bool ShouldUseClanColor(ClanColorInfo info)
    {
        if (!IsEnabled())
            return false;

        return info.HasCustomColors;
    }

    public void ApplyClanColors(ref uint color1, ref uint color2, ClanColorInfo info)
    {
        color1 = info.Color1;
        color2 = info.Color2;
    }

    public uint GetUniqueIconColor(uint backgroundColor, uint primaryIconColor)
    {
        if (primaryIconColor != backgroundColor)
            return primaryIconColor;

        uint alpha = backgroundColor & 0xFF000000;
        uint invertedRgb = ~backgroundColor & 0x00FFFFFF;
        return alpha | invertedRgb;
    }

    public bool IsDriftGuardEnabled() => _configProvider.GetConfig().EnableDriftGuard;

    public bool IsBannerPasteEnabled() => _configProvider.GetConfig().EnableBannerPaste;

    public bool IsUniqueSecondaryColorEnabled() => _configProvider.GetConfig().EnableUniqueSecondaryColor;

    public bool IsLayerLimitTranspilerEnabled() => _configProvider.GetConfig().EnableLayerLimitTranspiler;
}
