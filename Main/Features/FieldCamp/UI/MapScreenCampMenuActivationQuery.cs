using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;

namespace TAOM.Features.FieldCamp.UI;

/// <summary>
/// Engine-backed <see cref="ICampMenuActivationQuery"/>. Constructed at the MapView boundary
/// (the sanctioned engine-instantiated exception); everything here is a static engine read, so
/// the VM that consumes it stays constructible with mocks only.
/// </summary>
public sealed class MapScreenCampMenuActivationQuery : ICampMenuActivationQuery
{
    // The one map-menu gate (F6 and the Fiefs button use it too): opening the camp menu under any modal
    // would push a menu context into UI that is not expecting one, the class of bug Codex review #38b
    // caught for F6.
    public bool IsMapScreenClear => MapMenuGate.IsMapClearForMenu();

    // Null main party answers "not stationary" so every gate fails closed.
    public bool IsMainPartyStationary => MobileParty.MainParty?.IsMoving == false;

    public bool IsMainPartyInSettlement => MobileParty.MainParty?.CurrentSettlement != null;

    public bool IsMainPartyInEncounter =>
        MobileParty.MainParty?.MapEvent != null || PlayerEncounter.Current != null;

    public bool IsMainPartyDisorganized => MobileParty.MainParty?.IsDisorganized == true;
}
