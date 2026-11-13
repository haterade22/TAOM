using System;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarPledge;

/// <summary>
/// War Pledge's campaign entry point (#1113): the per-campaign reset, the save record, and the daily step.
/// <para>
/// <see cref="WarPledgeModule"/> builds it with <c>new</c> for every campaign, before any campaign event and
/// before a load's SyncData, so the constructor is the reset of the process-wide <see cref="WarPledgeStore"/>
/// (WarPledgeWiringTests pins that shape). A load then restores its record over the reset, and a save without
/// one keeps the reset.
/// </para>
/// <para>
/// Only the co-op authority courts: a pledge declares wars, and TAOM's save data is not replicated. The test
/// reads <c>ShouldDeferToHost</c> as well, because <c>IsAuthority</c> alone is true on both peers under a
/// co-op mod TAOM cannot probe. Nothing here runs on the load path. A declaration that a join could not make
/// is retried by the War of the Ring's own reconcile at session launch, when autoWarBetweenHostileTiers is on,
/// in kingdom-list order.
/// </para>
/// </summary>
public sealed class WarPledgeBehavior : CampaignBehaviorBase
{
    internal const string RowsKey = "_taom_war_pledge_rows";
    internal const string NextCourtingDayKey = "_taom_war_pledge_nextCourtingDay";

    private readonly WarPledgeService _service;
    private readonly WarPledgeStore _store;
    private readonly IKingdomWarSnapshotAdapter _clock;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public WarPledgeBehavior(
        WarPledgeService service, WarPledgeStore store, IKingdomWarSnapshotAdapter clock, ICoopSessionProvider coop,
        IModLogger logger)
    {
        _service = service;
        _store = store;
        _clock = clock;
        _coop = coop;
        _logger = logger;
        _store.ResetForNewSession();
    }

    public override void RegisterEvents() =>
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);

    /// <summary>A save captures the store; a load fills from the record (a missing key leaves "no pledge").</summary>
    public override void SyncData(IDataStore dataStore)
    {
        var rows = dataStore.IsSaving ? _store.CaptureRows() : string.Empty;
        var nextCourtingDay = dataStore.IsSaving ? _store.NextCourtingDay : WarPledgeStore.NoCourtingDay;
        dataStore.SyncData(RowsKey, ref rows);
        dataStore.SyncData(NextCourtingDayKey, ref nextCourtingDay);
        if (dataStore.IsLoading)
            _store.LoadFrom(rows, nextCourtingDay);
    }

    // internal for TAOM.Tests (InternalsVisibleTo). Guarded: a campaign event that throws takes the game down.
    internal void OnDailyTick()
    {
        try
        {
            if (!_coop.IsAuthority || _coop.ShouldDeferToHost)
                return;
            // An invalid clock runs nothing: day 0 would set the courting day from a day that is not today.
            if (!_clock.TryGetElapsedDay(out var day))
                return;
            _service.RunDailyCourting(day);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[WarPledge] daily step failed: {ex}");
        }
    }
}
