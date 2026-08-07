using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin boundary (ADR-002) for the continuous service loop: converts campaign ticks and
/// map-event-join edges into calls on <see cref="IServiceMaintenanceService"/>. No logic here.
///
/// Why <c>OnPartyAddedToMapEventEvent</c> matters: <c>CampaignEventDispatcher.OnMapEventStarted</c>
/// is dispatched exactly ONCE, as the last statement of <c>MapEvent.Initialize</c>, so it only ever
/// announces battle CREATION. Every way the commander joins an ALREADY-RUNNING fight — riding into
/// one in progress, reinforcing an ally — is invisible to it. This event is dispatched from
/// <c>MapEvent.AddInvolvedPartyInternal</c> and catches exactly that, with no polling.
///
/// It fires for EVERY party joining EVERY battle in the world, so the handler must stay near-free
/// on the non-match path. Deliberately no logging here.
/// </summary>
public class EnlistmentMaintenanceBehavior : CampaignBehaviorBase
{
    private readonly IServiceMaintenanceService _maintenance;
    private readonly ICoopSessionProvider _coopSession;

    public EnlistmentMaintenanceBehavior(
        IServiceMaintenanceService maintenance,
        ICoopSessionProvider coopSession)
    {
        _maintenance = maintenance;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnPartyAddedToMapEventEvent.AddNonSerializedListener(this, OnPartyAddedToMapEvent);
    }

    public override void SyncData(IDataStore dataStore) { }

    private void OnTick(float dt)
    {
        if (!_coopSession.IsAuthority)
            return;

        _maintenance.Pump(dt, CampaignTime.Now.ToHours);
    }

    private void OnPartyAddedToMapEvent(PartyBase party)
    {
        if (!_coopSession.IsAuthority)
            return;

        _maintenance.OnPartyJoinedRunningMapEvent(party?.MobileParty?.StringId);
    }
}
