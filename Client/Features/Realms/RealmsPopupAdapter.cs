using TaleWorlds.Localization;
using TAOM.Client.Features.EventWindow;
using TAOM.Features.Realms;

namespace TAOM.Client.Features.Realms;

/// <summary>
/// The client's Realms windows (ADR-013): replaces core's NullRealmsPopupAdapter in TaomClientComposition. The coronation
/// is a story event window, queued with the War Chronicle's so neither replaces the other, under its own category.
/// </summary>
internal sealed class RealmsPopupAdapter : IRealmsPopupAdapter
{
    internal const string Category = "{=taom_event_category_coronation}Coronation";

    public void ShowCoronation(CoronationPopup popup) =>
        EventWindows.Enqueue(new TAOM.Adapters.EventWindow(popup.Heading, popup.Text, popup.Image, popup.Button, popup.BannerCode, popup.Lines,
            category: new TextObject(Category).ToString()));
}
