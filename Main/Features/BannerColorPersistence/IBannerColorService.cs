using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public interface IBannerColorService
{
    bool IsEnabled();
    bool ShouldUseClanColor(ClanColorInfo info);
    void ApplyClanColors(ref uint color1, ref uint color2, ClanColorInfo info);
    uint GetUniqueIconColor(uint backgroundColor, uint primaryIconColor);
    bool IsDriftGuardEnabled();
    bool IsBannerPasteEnabled();
    bool IsUniqueSecondaryColorEnabled();
    bool IsLayerLimitTranspilerEnabled();
}
