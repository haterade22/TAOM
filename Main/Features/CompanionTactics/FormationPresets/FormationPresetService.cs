using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// In-memory CRUD service for formation presets. The <c>FormationPresetCampaignBehavior</c>
/// owns the actual <c>SyncData&lt;List&lt;HoNFormationPreset&gt;&gt;</c>; this service is the
/// state and validation layer between the SyncData buffer and the consumers (OOB UI / VMs).
///
/// MaxFormationPresets enforcement: triggered ONLY when the call adds a new preset to the
/// collection. Updates to an existing preset (matched by Id) skip the limit check — a save
/// at limit can still be edited.
/// </summary>
public sealed class FormationPresetService : IFormationPresetService
{
    private readonly ICompanionTacticsSettingsProvider _settings;
    private readonly IModLogger _logger;

    private readonly List<HoNFormationPreset> _presets = new();

    public FormationPresetService(ICompanionTacticsSettingsProvider settings, IModLogger logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public IReadOnlyList<HoNFormationPreset> Presets => _presets;

    public SaveResult SavePreset(HoNFormationPreset preset)
    {
        if (preset == null) return SaveResult.Invalid;
        if (string.IsNullOrWhiteSpace(preset.Name)) return SaveResult.Invalid;
        if (string.IsNullOrEmpty(preset.Id)) preset.Id = Guid.NewGuid().ToString();

        // Update path: matched by Id.
        var existingIndex = _presets.FindIndex(p => p.Id == preset.Id);
        if (existingIndex >= 0)
        {
            _presets[existingIndex] = preset;
            DebugLog($"Updated preset '{preset.Name}'");
            return SaveResult.Saved;
        }

        // Add path: enforce name uniqueness + limit.
        if (_presets.Exists(p => p.Name == preset.Name)) return SaveResult.NameInUse;
        if (_presets.Count >= _settings.MaxFormationPresets)
        {
            DebugLog($"Save refused — at limit ({_settings.MaxFormationPresets})");
            return SaveResult.LimitReached;
        }

        _presets.Add(preset);
        DebugLog($"Saved preset '{preset.Name}'");
        return SaveResult.Saved;
    }

    public HoNFormationPreset GetPresetById(string presetId)
    {
        if (string.IsNullOrEmpty(presetId)) return null;
        return _presets.Find(p => p.Id == presetId);
    }

    public bool DeletePreset(string presetId)
    {
        var idx = _presets.FindIndex(p => p.Id == presetId);
        if (idx < 0) return false;
        var name = _presets[idx].Name;
        _presets.RemoveAt(idx);
        DebugLog($"Deleted preset '{name}'");
        return true;
    }

    public void OnGameLoaded(List<HoNFormationPreset> loaded)
    {
        _presets.Clear();
        if (loaded != null) _presets.AddRange(loaded);
        DebugLog($"OnGameLoaded — {_presets.Count} preset(s)");
    }

    public List<HoNFormationPreset> GetPresetsForSaving() => new(_presets);

    public void OnMissionEnd()
    {
        // No per-mission state in the service today; this exists for symmetry with other
        // services and to give us a hook if we add a "currentPreset" pointer later.
    }

    private void DebugLog(string message)
    {
        if (_settings.FormationPresetsDebug)
            _logger.LogDebug($"[FormationPresets] {message}");
    }
}
