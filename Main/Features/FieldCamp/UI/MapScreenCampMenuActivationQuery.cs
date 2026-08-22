using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// Engine-backed <see cref="ICampMenuActivationQuery"/>. Constructed at the MapView boundary
/// (the sanctioned engine-instantiated exception); everything here is a static engine read, so
/// the VM that consumes it stays constructible with mocks only.
/// </summary>
public sealed class MapScreenCampMenuActivationQuery : ICampMenuActivationQuery
{
    public bool IsMapScreenClear
    {
        get
        {
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState))
                return false;

            var screen = MapScreen.Instance;
            if (screen == null)
                return false;

            // Vanilla MapScreen's full modal-suppression set, mirrored from Patch36_MapScreenF6
            // (IMapScreenInputAdapter only exposes the F6 key, not these guards). Opening the camp
            // menu under any of these modals would push a menu context into UI that is not
            // expecting one - the same class of bug Codex review #38b caught for F6.
            if (screen.IsInMenu)
                return false;
            if (screen.IsInBattleSimulation)
                return false;
            if (screen.IsInArmyManagement)
                return false;
            if (screen.IsMarriageOfferPopupActive)
                return false;
            if (screen.IsHeirSelectionPopupActive)
                return false;
            if (screen.IsMapCheatsActive)
                return false;
            if (screen.IsMapIncidentActive)
                return false;
            if (screen.IsOverlayContextMenuEnabled)
                return false;
            if (screen.EncyclopediaScreenManager?.IsEncyclopediaOpen == true)
                return false;

            return true;
        }
    }

    // Null main party answers "not stationary" so every gate fails closed.
    public bool IsMainPartyStationary => MobileParty.MainParty?.IsMoving == false;

    public bool IsMainPartyInSettlement => MobileParty.MainParty?.CurrentSettlement != null;

    public bool IsMainPartyInEncounter =>
        MobileParty.MainParty?.MapEvent != null || PlayerEncounter.Current != null;

    public bool IsMainPartyDisorganized => MobileParty.MainParty?.IsDisorganized == true;
}
