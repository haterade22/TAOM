using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Owns the SyncData buffer for formation presets. Wraps the SyncData call in try/catch in
/// case the SaveableTypeDefiner BaseId 726900601 collides with another mod — a parse error
/// degrades gracefully (presets reset, behavior continues).
/// </summary>
public sealed class FormationPresetCampaignBehavior : CampaignBehaviorBase
{
    private readonly IFormationPresetService _service;
    private readonly IModLogger _logger;

    private List<HoNFormationPreset> _savedPresets = new();

    public FormationPresetCampaignBehavior(IFormationPresetService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        // P2-3 fix (Codex review #36, 2026-05-06): the service is a DryIoc singleton, so its
        // _presets survive across campaign sessions in the same process. SyncData(IsLoading)
        // resets them when loading a save, but a NEW campaign has no load snapshot, so without
        // OnNewGameCreated the previous campaign's presets bleed into the new one. Clear the
        // service on new-game creation. Mirrors the EquipPresets pattern.
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    public override void SyncData(IDataStore dataStore)
    {
        // P2-2 fix (Codex review #36, 2026-05-06): vanilla SyncData<T> records the ref value
        // at call time. We MUST populate _savedPresets BEFORE calling SyncData on save, or
        // the engine writes the previous (loaded/default) buffer and the new presets are
        // not persisted until the NEXT save. Mirror the load path: pull the save snapshot
        // first, then call SyncData, then on load hand the deserialized list to the service.
        try
        {
            if (dataStore.IsSaving)
            {
                _savedPresets = _service.GetPresetsForSaving();
            }

            dataStore.SyncData("TAOM_FormationPresets", ref _savedPresets);

            if (dataStore.IsLoading)
            {
                _service.OnGameLoaded(_savedPresets ?? new List<HoNFormationPreset>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[CompanionTactics] FormationPreset SyncData failed (likely BaseId collision): {ex.Message}");
            // On any deserialization error, reset to empty rather than crashing the load.
            _savedPresets = new List<HoNFormationPreset>();
            _service.OnGameLoaded(_savedPresets);
        }
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // Reset the singleton's preset list so the previous campaign's presets don't leak
        // into the new campaign. SyncData on subsequent saves will record an empty buffer
        // (correct), and reloads will populate from disk.
        _savedPresets = new List<HoNFormationPreset>();
        _service.OnGameLoaded(_savedPresets);
    }
}
