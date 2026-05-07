using System.Collections.Generic;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Features.EquipPresets;

/// <summary>
/// Per-hero equipment-preset CRUD with full <c>ItemModifier</c> preservation. The service is
/// adapter-clean — no <c>Hero</c>, <c>Equipment</c>, or <c>ItemModifier</c> types cross the
/// boundary. The campaign behavior owns SyncData; the service owns the in-memory dictionary
/// (<see cref="SerializableState"/>) and the CRUD rules.
/// </summary>
public interface IEquipmentPresetService
{
    /// <summary>Read-only view of presets for a single hero. Empty when none exist.</summary>
    IReadOnlyList<HoNEquipmentPreset> GetPresets(string heroStringId);

    /// <summary>
    /// Captures the hero's current equipment as a new preset. Honors the "save promise = code"
    /// rule: returns <see cref="SaveOutcome.MaxReached"/> when at the per-hero cap (the VM
    /// surfaces this as a player-visible message rather than silently overwriting).
    /// </summary>
    SaveOutcome SaveCurrent(string heroStringId, string presetName,
                            bool includeMount, bool includeCivilian);

    /// <summary>Overwrites an existing preset by id with the hero's current equipment.</summary>
    UpdateOutcome UpdatePreset(string heroStringId, string presetId);

    /// <summary>Applies a preset to the hero. Returns the full per-slot report.</summary>
    PresetLoadResult LoadPresetWithReport(string heroStringId, string presetId);

    DeleteOutcome DeletePreset(string heroStringId, string presetId);

    /// <summary>
    /// Removes preset bundles whose hero StringId is not in the live set. Returns count pruned.
    /// Called by <see cref="Hooks.EquipmentPresetCampaignBehavior.OnGameLoaded"/>; the live-id
    /// set is computed at the boundary so this method stays free of TaleWorlds types.
    /// </summary>
    int PruneOrphanedEntries(IReadOnlyCollection<string> liveHeroIds);

    /// <summary>Backing dictionary used by SyncData. Behavior is the only intended caller.</summary>
    Dictionary<string, List<HoNEquipmentPreset>> SerializableState { get; }

    /// <summary>Replaces the in-memory state from a deserialized save (or null on first-load).</summary>
    void RestoreFromSerializableState(Dictionary<string, List<HoNEquipmentPreset>>? state);
}
