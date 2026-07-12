using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Thin entry point (ADR-002, no logic) that feeds <see cref="ICaravanVisitMemory"/>. Records each
/// caravan's town entries via <c>SettlementEntered</c> (the same signal vanilla's own caravan
/// bookkeeping uses) so the <c>GetTradeScoreForTown</c> reweight can penalize just-visited towns, and
/// evicts a caravan's memory on <c>MobilePartyDestroyed</c> so the dictionary stays bounded to live
/// caravans over a long campaign.
///
/// Registered unconditionally (not gated on the master toggle) so a mid-session toggle-on works
/// immediately; the recording is a cheap dictionary write per caravan town-entry and the penalty only
/// bites when the reweight service is active. No <c>SyncData</c> — the memory is ephemeral runtime
/// state that rebuilds as caravans move (matches the feature's save-clean design; the worst case is
/// one sub-optimal hop after a load before the ring repopulates).
/// </summary>
public class CaravanVisitMemoryBehavior : CampaignBehaviorBase
{
    private readonly ICaravanVisitMemory _memory;

    public CaravanVisitMemoryBehavior(ICaravanVisitMemory memory)
    {
        _memory = memory;
    }

    public override void RegisterEvents()
    {
        // Clear cross-campaign residue at session launch (fires on both new game and load): the memory
        // is a process-level singleton and MobileParty.StringId is reused across campaigns, so a stale
        // ring from a prior/abandoned campaign would otherwise mis-penalize a fresh caravan's first hops.
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
        CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persistence — ephemeral recency memory, rebuilt from live caravan movement.
    }

    private void OnSessionLaunched(CampaignGameStarter starter) => _memory.ClearAll();

    private void OnSettlementEntered(MobileParty party, Settlement settlement, Hero hero)
    {
        // Candidate space is Town.AllTowns, so only town entries matter; village/castle entries would
        // waste a ring slot without ever matching a candidate.
        if (party == null || settlement == null || !settlement.IsTown || !party.IsCaravan)
            return;

        _memory.RecordVisit(party.StringId, settlement.StringId);
    }

    private void OnMobilePartyDestroyed(MobileParty party, PartyBase partyBase)
    {
        // Clear unconditionally on a valid id — a no-op for a party that was never recorded, and
        // robust against IsCaravan being reset during teardown. Mirrors vanilla's own per-caravan
        // dictionary eviction in CaravansCampaignBehavior.OnMobilePartyDestroyed.
        if (party != null)
            _memory.Clear(party.StringId);
    }
}
