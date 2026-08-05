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
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
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

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        if (!_coopSession.IsAuthority || party != MobileParty.MainParty || settlement == null)
            return;
        _duties.OnSettlementEntered(settlement.StringId, CampaignTime.Now.ToDays);
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase destroyer)
    {
        if (!_coopSession.IsAuthority || party == null)
            return;
        _duties.OnMobilePartyDestroyed(party.StringId);
    }
}
