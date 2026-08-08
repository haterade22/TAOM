using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IEnlistmentStore _store;
    private readonly ICoopSessionProvider _coopSession;

    public EnlistmentMaintenanceBehavior(
        IServiceMaintenanceService maintenance,
        IEnlistmentReconciler reconciler,
        IEnlistmentStore store,
        ICoopSessionProvider coopSession)
    {
        _maintenance = maintenance;
        _reconciler = reconciler;
        _store = store;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        CampaignEvents.OnPartyAddedToMapEventEvent.AddNonSerializedListener(this, OnPartyAddedToMapEvent);

        // TWO re-attach edges, not the donor's six. The pump already re-derives everything within
        // 250 ms, so an edge only earns its place if it beats that — and only these two do
        // anything the pump cannot: they fire at moments the pump is structurally silent, because
        // CampaignEvents.TickEvent does not run inside encounter/settlement menus or a map
        // conversation, and the wait-menu tick only runs on OUR menu. A commander walking out of a
        // town while the player sits in a menu is exactly that gap.
        //
        // The other four the donor arms (player battle end, army joined, siege joined/left, siege
        // completed) are DECLINED: each only re-arms a flag whose own retry budget is an hour
        // anyway, so they buy nothing the hourly pass does not already give. Recorded in
        // docs/features/enlistment.md so this is a decision rather than an oversight.
        CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
        CampaignEvents.OnPartyLeftArmyEvent.AddNonSerializedListener(this, OnPartyLeftArmy);
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

    private void OnSettlementLeft(MobileParty party, Settlement settlement) =>
        ReconcileIfCommander(party, "settlement left");

    private void OnPartyLeftArmy(MobileParty party, TaleWorlds.CampaignSystem.Army army) =>
        ReconcileIfCommander(party, "army left");

    /// <summary>
    /// Both edges fire for EVERY party in the world, so filter on the commander's id before doing
    /// anything. The reconciler's own re-entrancy guard covers the case that matters most here:
    /// settlement following makes the reconciler call LeaveSettlementAction, which dispatches
    /// OnSettlementLeft straight back into this handler.
    /// </summary>
    private void ReconcileIfCommander(MobileParty party, string trigger)
    {
        if (!_coopSession.IsAuthority)
            return;

        var commanderId = _store.Record.CommanderHeroId;
        if (string.IsNullOrEmpty(commanderId))
            return;
        if (party?.LeaderHero?.StringId != commanderId)
            return;

        _reconciler.ReconcileNow(CampaignTime.Now.ToDays, trigger);
    }
}
