using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// The one list of conditions under which TAOM may push a game menu onto the campaign map. Every
/// menu-pushing entry point asks here: F6 and the Fiefs button (through <see cref="FiefHubHostAdapter"/>)
/// and the field camp's "Make camp" (through <c>MapScreenCampMenuActivationQuery</c>). Written once because
/// three copies drifted apart.
///
/// It mirrors vanilla <c>MapScreen.OnFrameTick</c>'s full suppression set for map actions (decompiled
/// MapScreen.cs: army management, battle simulation, the marriage and heir popups, map cheats, a map
/// incident, the overlay context menu, the encyclopedia, and <c>CampaignUIHelper.GetMapScreenActionIsEnabledWithReason</c>
/// for the prisoner, encounter, raft and ferry states). Codex review #38b caught that an IsInMenu-only
/// guard let F6 fire during those modals, each of which can read <c>MainParty.CurrentSettlement</c> while
/// the remote-fief swap has replaced it. On top of vanilla's set it also refuses while the escape menu is
/// open and while the campaign has a menu context, because <c>IsInMenu</c> can be one frame stale when a
/// click, not a tick, asks.
///
/// Field reads only, plus one vanilla helper that allocates a text only when it refuses: the Fiefs button
/// polls this every frame. Main thread.
/// </summary>
internal static class MapMenuGate
{
    public static bool IsMapClearForMenu()
    {
        if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return false;

        // From here on the campaign, the main hero and the main party exist: the vanilla helper below
        // reads Hero.MainHero and MobileParty.MainParty unguarded.
        var campaign = Campaign.Current;
        if (campaign == null || campaign.CurrentMenuContext != null) return false;

        var screen = MapScreen.Instance;
        if (screen == null) return false;
        if (screen.IsInMenu) return false;
        if (screen.IsInBattleSimulation) return false;
        if (screen.IsInArmyManagement) return false;
        if (screen.IsMarriageOfferPopupActive) return false;
        if (screen.IsHeirSelectionPopupActive) return false;
        if (screen.IsMapCheatsActive) return false;
        if (screen.IsMapIncidentActive) return false;
        if (screen.IsOverlayContextMenuEnabled) return false;
        if (screen.IsEscapeMenuOpened) return false;
        if (screen.EncyclopediaScreenManager?.IsEncyclopediaOpen == true) return false;
        if (!CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out _)) return false;
        return true;
    }
}
