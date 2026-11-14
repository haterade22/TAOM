using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Adapters;

/// <summary>
/// The one test for whether TAOM may raise a one-button popup (<c>InformationManager.ShowInquiry</c>) over the
/// campaign map: a main party exists, no encounter is open, the map state is not at a menu, and no other
/// inquiry is showing (no stacking). The Eye of Sauron hunter notices, the War Chronicle announcements and the
/// Hidden Hosts hint all ask here. Written once because three copies of it had already drifted into different
/// condition orders.
///
/// The main party check comes first on purpose: <c>PlayerEncounter.Current</c> throws with no campaign.
///
/// Field reads plus one delegate call (<c>IsAnyInquiryActive</c>, whose vanilla handler reads a field); no
/// allocation. Main thread. The Messenger feature's <c>IsPlayerAvailable</c> is the same test without the
/// inquiry term (#929 follow-up).
/// </summary>
internal static class MapPopupGate
{
    internal static bool IsMapFree() => IsMapFreeUnderPopup() && !InformationManager.IsAnyInquiryActive();

    /// <summary>
    /// The same test from a popup's own button, where the popup is the open inquiry: the rest of the campaign tick that raised the popup
    /// can still open an encounter, a map event or a map conversation under it (Story Missions' "Ride" button).
    /// </summary>
    internal static bool IsMapFreeUnderPopup() =>
        Campaign.Current?.MainParty != null
        && PlayerEncounter.Current == null
        && Campaign.Current.MainParty.MapEvent == null
        && GameStateManager.Current?.ActiveState is MapState { AtMenu: false, MapConversationActive: false };
}
