using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Rally;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle's per-campaign state as one unit: it assembles the save payload from the effect
/// registry, the baselines and the rally tiers, hands it to the behavior in chunks, and restores each
/// section from a load. It also holds the chronicle section as an opaque JSON array until the event
/// milestone gives it an owner, and resets every registry for a new session.
///
/// The chunks come from <see cref="MomentumSyncChunker"/>: one SyncData string over 32,767 UTF-8
/// bytes corrupts the save at write time, and a developed campaign's effects and baselines can grow
/// past that.
/// </summary>
public sealed class WarChronicleStateService
{
    private readonly IWarEffectService _effects;
    private readonly WarBaselineService _baselines;
    private readonly RallyTierStore _rallyTiers;
    private readonly IModLogger _logger;

    private string _chronicleJson = WarChronicleSaveCodec.EmptyArray;

    public WarChronicleStateService(
        IWarEffectService effects, WarBaselineService baselines, RallyTierStore rallyTiers, IModLogger logger)
    {
        _effects = effects;
        _baselines = baselines;
        _rallyTiers = rallyTiers;
        _logger = logger;
    }

    /// <summary>The payload split into chunks that each stay far under the engine's per-entry limit.</summary>
    public IReadOnlyList<string> CaptureChunks()
    {
        var payload = new WarChroniclePayload
        {
            ChronicleJson = _chronicleJson,
        };
        payload.Effects.AddRange(_effects.Snapshot());
        foreach (var pair in _baselines.Snapshot())
            payload.Baselines[pair.Key] = pair.Value;
        foreach (var pair in _rallyTiers.Snapshot())
            payload.RallyTiers[pair.Key] = pair.Value;

        return MomentumSyncChunker.Split(WarChronicleSaveCodec.Serialize(payload));
    }

    /// <summary>
    /// Restores every section from the saved chunks. An empty list means a corrupt or out-of-range record:
    /// a save from before this feature never reaches SyncData (CampaignBehaviorDataStore.LoadBehaviorData).
    /// </summary>
    public void RestoreFromChunks(IReadOnlyList<string> chunks)
    {
        ResetForNewSession();

        var payload = WarChronicleSaveCodec.Parse(MomentumSyncChunker.Join(chunks));
        foreach (var warning in payload.Warnings)
            _logger.LogWarning("[WarChronicle] Save: " + warning + ".");

        _effects.RestoreFromSave(payload.Effects);
        _baselines.RestoreFromSave(payload.Baselines);
        _rallyTiers.Restore(payload.RallyTiers);
        _chronicleJson = payload.ChronicleJson;
    }

    /// <summary>Empties every registry. Called from the campaign behavior's constructor (the session-reset rule).</summary>
    public void ResetForNewSession()
    {
        _effects.ResetForNewSession();
        _baselines.ResetForNewSession();
        _rallyTiers.ResetForNewSession();
        _chronicleJson = WarChronicleSaveCodec.EmptyArray;
    }
}
