using System.Collections.Generic;
using TAOM.Features.CompanionTactics.FormationPresets.Models;

namespace TAOM.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pure-state CRUD over <see cref="HoNFormationPreset"/> entries. Application of a preset
/// to an actual <c>OrderOfBattleVM</c> happens at the boundary (the OOBOverlayService /
/// MissionView) — the sealed VM never crosses this interface.
/// </summary>
public interface IFormationPresetService
{
    IReadOnlyList<HoNFormationPreset> Presets { get; }

    /// <summary>Persist a preset. Enforces <c>MaxFormationPresets</c> on additions.</summary>
    SaveResult SavePreset(HoNFormationPreset preset);

    /// <summary>Look up by id.</summary>
    HoNFormationPreset GetPresetById(string presetId);

    /// <summary>Remove by id; returns true if found.</summary>
    bool DeletePreset(string presetId);

    /// <summary>Replace all presets (called from <c>OnGameLoaded</c> with the saved list).</summary>
    void OnGameLoaded(List<HoNFormationPreset> loaded);

    /// <summary>Snapshot for <c>SyncData</c> on save.</summary>
    List<HoNFormationPreset> GetPresetsForSaving();

    /// <summary>Mission lifecycle reset — clears any per-battle state.</summary>
    void OnMissionEnd();
}
