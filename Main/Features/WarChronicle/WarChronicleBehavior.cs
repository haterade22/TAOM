using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarChronicle.Ledger;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Thin entry point for the War Chronicle (docs/features/war-chronicle.md, issue #765): the daily tick,
/// the kingdom-destroyed ledger line and the SyncData halves. Always registered, so the save payload
/// survives a toggle. Every singleton holding campaign state is reset in the CONSTRUCTOR: the module
/// builds this behavior fresh in each campaign's OnGameStart (WarChronicleModule, pinned by
/// WarChronicleWiringTests), before any campaign event and before a load's SyncData, which then
/// restores the saved state over the reset. A save without a record stays reset. No latch.
/// </summary>
public class WarChronicleBehavior : CampaignBehaviorBase
{
    private const string CountKey = "_taom_war_chronicle_v1_count";
    private const string ChunkKeyPrefix = "_taom_war_chronicle_v1_";

    // 4,096 chunks of 10,000 chars is 40 MB of payload: far past any real save, so a count beyond it is
    // a corrupt or foreign value, not a payload to loop over.
    private const int MaxChunks = 4096;

    private readonly WarChronicleTickService _tick;
    private readonly WarChronicleStateService _state;
    private readonly WarLedgerService _ledger;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public WarChronicleBehavior(
        WarChronicleTickService tick, WarChronicleStateService state, WarLedgerService ledger,
        ICoopSessionProvider coop, IModLogger logger)
    {
        _tick = tick;
        _state = state;
        _ledger = ledger;
        _coop = coop;
        _logger = logger;

        _state.ResetForNewSession();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var chunks = _state.CaptureChunks();
            var count = chunks.Count;
            dataStore.SyncData(CountKey, ref count);
            for (var i = 0; i < count; i++)
            {
                var chunk = chunks[i];
                dataStore.SyncData(ChunkKeyPrefix + i, ref chunk);
            }

            return;
        }

        if (!dataStore.IsLoading)
            return;

        var savedCount = 0;
        dataStore.SyncData(CountKey, ref savedCount);
        if (savedCount < 0 || savedCount > MaxChunks)
        {
            _logger.LogWarning($"[WarChronicle] Save: the chunk count {savedCount} is out of range, so the payload is ignored.");
            savedCount = 0;
        }

        var saved = new List<string>(savedCount);
        for (var i = 0; i < savedCount; i++)
        {
            string chunk = string.Empty;
            dataStore.SyncData(ChunkKeyPrefix + i, ref chunk);
            saved.Add(chunk ?? string.Empty);
        }

        _state.RestoreFromChunks(saved);
    }

    // internal for TAOM.Tests (InternalsVisibleTo): the co-op gate is asserted directly.
    internal void OnDailyTick()
    {
        // World work and save-backed writes (effects, baselines) are the authority's: a co-op client
        // takes the host's state through the save, and a second writer would diverge it.
        if (!_coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
            return;

        _tick.RunDaily();
    }

    private void OnKingdomDestroyed(Kingdom kingdom) => HandleKingdomDestroyed(kingdom?.StringId);

    internal void HandleKingdomDestroyed(string? kingdomId)
    {
        if (!_coop.IsAuthority)
            return;

        _ledger.LogDestroyed(kingdomId);
    }
}
