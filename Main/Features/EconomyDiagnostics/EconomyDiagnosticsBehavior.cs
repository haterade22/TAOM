using TaleWorlds.CampaignSystem;

namespace TAOM.Features.EconomyDiagnostics;

/// <summary>
/// Thin entry point (ADR-002) that rolls the town-gold ledger onto a fresh day and clears it between
/// campaigns.
///
/// Always registered rather than gated behind a toggle, for the same reason
/// <c>Patch61_SaveLoadDiagnostics</c> is always-on: the questions it answers ("where did this town's
/// gold go") are asked about a session that has *already happened*, and a diagnostic the user has to
/// switch on in advance is one they never have data from when it matters. The cost is a handful of
/// dictionary writes per settlement per day against a bounded ring.
/// </summary>
public class EconomyDiagnosticsBehavior : CampaignBehaviorBase
{
    private readonly ITownGoldLedger _ledger;

    public EconomyDiagnosticsBehavior(ITownGoldLedger ledger)
    {
        _ledger = ledger;
    }

    public override void RegisterEvents()
    {
        // Fires on both new game and load. The ledger is a process-level singleton and settlement
        // ids repeat across campaigns, so without this a second campaign in the same session
        // inherits the first one's buckets.
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persistence — a diagnostic ring has no business in the save file.
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        _ledger.ClearAll();
        TownGoldFlowScope.Reset();
    }

    private void OnDailyTick()
    {
        // Reset first: a Harmony postfix does not run when the patched method throws, so an engine
        // exception inside a tagged call would leave the ambient tag stuck and mislabel every flow
        // from then on. A daily reset bounds that damage to one day.
        TownGoldFlowScope.Reset();
        _ledger.BeginDay();
    }
}
