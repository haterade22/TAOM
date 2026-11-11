using TaleWorlds.Core;
using TAOM.Client.Features.Realms.Popups;
using TAOM.Features.Realms;

namespace TAOM.Client.Features.Realms;

/// <summary>The client's Realms windows (ADR-013): replaces core's NullRealmsPopupAdapter in TaomClientComposition.</summary>
internal sealed class RealmsPopupAdapter : IRealmsPopupAdapter
{
    public void ShowCoronation(CoronationPopup popup)
    {
        var banner = popup.BannerCode != null ? new Banner(popup.BannerCode) : new Banner();
        UiQueue.PostPopup(() => RealmsPopups.OpenCoronation(close =>
            new CoronationPopupVM(popup.Heading, popup.Text, popup.Image, popup.Button, banner, popup.Lines, close)));
    }
}
