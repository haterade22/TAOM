using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Duties;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Thin event router (ADR-002) for the duty engine: hourly lifecycle, daily offer roll,
/// and the two completion triggers (settlement arrival, target-party destroyed). All
/// policy lives in <see cref="IDutyOrchestrationService"/>; duty state persists inside the
/// content record, so this behavior is stateless (no SyncData).
///
/// CO-OP: host-only — duties spawn/destroy world parties and pay rewards.
/// </summary>
public class EnlistmentDutyBehavior : CampaignBehaviorBase
{
    private readonly IDutyOrchestrationService _duties;
    private readonly ICoopSessionProvider _coopSession;

    public EnlistmentDutyBehavior(IDutyOrchestrationService duties, ICoopSessionProvider coopSession)
    {
        _duties = duties;
        _coopSession = coopSession;
    }

    public override void RegisterEvents()
    {
        // Two ticks, no completion triggers. Field duties are camp work resolved by a timed skill
        // check, so nothing outside this behavior can complete one — there is no target party to
        // destroy and no settlement to arrive at. The SettlementEntered and MobilePartyDestroyed
        // subscriptions that used to live here went with the travel model (#428); the second was
        // also the entry point for the #375 stack overflow, and unsubscribing removes that
        // re-entrancy surface entirely rather than guarding it.
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore) { }

    internal void OnHourlyTick()
    {
        if (!_coopSession.IsAuthority)
            return;
        _duties.HourlyTick(CampaignTime.Now.ToDays);
    }

    internal void OnDailyTick()
    {
        if (!_coopSession.IsAuthority)
            return;
        _duties.DailyOfferTick(CampaignTime.Now.ToDays, CampaignTime.Now.CurrentHourInDay);
    }

}
