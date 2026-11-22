using TAOM.Adapters;

namespace TAOM.Features.BannerColorPersistence;

public interface IBannerColorService
{
    bool IsEnabled();
    bool ShouldUseClanColor(ClanColorInfo info);
    uint GetUniqueIconColor(uint backgroundColor, uint primaryIconColor);
    bool IsDriftGuardEnabled();
    bool IsBannerPasteEnabled();
    bool IsUniqueSecondaryColorEnabled();
    bool IsAgentVisualColorsEnabled();
    bool IsConversationTableauColorsEnabled();

    /// <summary>True on a co-op client inside Clan.PreAfterLoad: its banners are the server's, so the vanilla load repaint is skipped.</summary>
    bool ShouldSkipLoadRepaint(bool insideClanLoad);
}
