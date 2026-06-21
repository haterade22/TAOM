using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets.Hooks;

/// <summary>
/// Owns the SyncData buffer for formation presets. The try/catch around <c>dataStore.SyncData</c>
/// guards the LOAD path (deserialize / BaseId collision with another mod) and the save-time ref
/// population — on a parse error it degrades gracefully (presets reset, behavior continues).
///
/// It does NOT and cannot guard the SAVE byte-serialization: the engine writes the buffer later on the
/// AsyncFileSaveDriver background thread, outside this method, so an unserializable field would crash
/// there regardless of this catch. The fix for that class of bug lives in the saveable model itself —
/// keep every <see cref="HoNFormationPreset"/> <c>[SaveableField]</c> a serializable type (pinned by
/// <c>HoNFormationPresetSerializationTests</c>). History: a <c>DateTime</c> field used to crash every save.
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
            // Phase 9b #139 P1 — surface to player. Pre-fix only LogWarning to TAOM internal log;
            // player never saw the cause and lost presets repeatedly with no idea why.
            try
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    "[TAOM] Formation presets could not be loaded — reset to empty (see log for details).",
                    Color.FromUint(0xFFFFAA00)));
            }
            catch { /* InformationManager may not be available in some load paths */ }
        }
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // Phase 9b #139 P2 — was OnGameLoaded(empty) (semantic mismatch: load-path entry point
        // used for new-game reset). Now calls Reset() explicitly.
        _savedPresets = new List<HoNFormationPreset>();
        _service.Reset();
    }
}
