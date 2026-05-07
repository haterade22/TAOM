using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Features.EquipPresets;

/// <summary>
/// Per-hero equipment-preset CRUD with full <c>ItemModifier</c> preservation. Services see only
/// StringIds/primitives — TaleWorlds types stay inside the adapters.
///
/// <para><b>Load via vanilla InventoryLogic:</b> the service builds a list of
/// <see cref="PresetSlotRequest"/>s (one per slot in the included sets) and delegates to
/// <see cref="IInventoryScreenAdapter.LoadEquipment"/>. The adapter submits a
/// <c>TransferCommand</c> batch, which: (a) consumes inventory items, (b) deposits displaced
/// equipment back to inventory, (c) refreshes slot VMs via <c>AfterTransfer</c>, (d) honors
/// vanilla slot-fit rules. This replaced the original direct-mutation path that Codex flagged
/// CRITICAL on 2026-05-07.</para>
/// </summary>
public sealed class EquipmentPresetService : IEquipmentPresetService
{
    private readonly IEquipPresetsSettingsProvider _settings;
    private readonly IEquipmentSlotAdapter _slots;
    private readonly IInventoryScreenAdapter _screen;
    private readonly IItemModifierLookupAdapter _modifierLookup;
    private readonly IModLogger _logger;

    private Dictionary<string, List<HoNEquipmentPreset>> _state = new();

    public EquipmentPresetService(
        IEquipPresetsSettingsProvider settings,
        IEquipmentSlotAdapter slots,
        IInventoryScreenAdapter screen,
        IItemModifierLookupAdapter modifierLookup,
        IModLogger logger)
    {
        _settings = settings;
        _slots = slots;
        _screen = screen;
        _modifierLookup = modifierLookup;
        _logger = logger;
    }

    public Dictionary<string, List<HoNEquipmentPreset>> SerializableState => _state;

    /// <summary>L9 fix (Codex 2026-05-07): normalize nulls so a corrupt save / future
    /// migration doesn't leave the service vulnerable to NullReferenceException downstream.
    /// Drops null keys, replaces null lists with empty, drops null preset entries, ensures
    /// <c>Items</c>/<c>CivilianItems</c> are non-null lists.</summary>
    public void RestoreFromSerializableState(Dictionary<string, List<HoNEquipmentPreset>>? state)
    {
        var normalized = new Dictionary<string, List<HoNEquipmentPreset>>();
        if (state != null)
        {
            int droppedNullKeys = 0, droppedNullPresets = 0, normalizedListSlots = 0;
            foreach (var kvp in state)
            {
                if (string.IsNullOrEmpty(kvp.Key))
                {
                    droppedNullKeys++;
                    continue;
                }
                var list = kvp.Value ?? new List<HoNEquipmentPreset>();
                var clean = new List<HoNEquipmentPreset>(list.Count);
                foreach (var preset in list)
                {
                    if (preset == null) { droppedNullPresets++; continue; }
                    if (preset.Items == null) { preset.Items = new List<HoNPresetItemReference>(); normalizedListSlots++; }
                    if (preset.CivilianItems == null) { preset.CivilianItems = new List<HoNPresetItemReference>(); normalizedListSlots++; }
                    clean.Add(preset);
                }
                if (clean.Count > 0) normalized[kvp.Key] = clean;
            }
            if (_settings.IsDebugMode && (droppedNullKeys + droppedNullPresets + normalizedListSlots) > 0)
                _logger.LogDebug($"[EquipPresets] RestoreFromSerializableState normalized: dropped {droppedNullKeys} null-key entries, {droppedNullPresets} null presets; replaced {normalizedListSlots} null lists.");
        }
        _state = normalized;
    }

    public IReadOnlyList<HoNEquipmentPreset> GetPresets(string heroStringId)
    {
        if (string.IsNullOrEmpty(heroStringId)) return Array.Empty<HoNEquipmentPreset>();
        return _state.TryGetValue(heroStringId, out var list) ? list : Array.Empty<HoNEquipmentPreset>();
    }

    public SaveOutcome SaveCurrent(string heroStringId, string presetName,
                                   bool includeMount, bool includeCivilian)
    {
        if (!_settings.IsEnabled) return SaveOutcome.Disabled;
        if (string.IsNullOrEmpty(heroStringId)) return SaveOutcome.NoActiveHero;
        if (string.IsNullOrWhiteSpace(presetName)) return SaveOutcome.InvalidName;
        if (!_slots.HeroExists(heroStringId)) return SaveOutcome.NoActiveHero;

        if (!_state.TryGetValue(heroStringId, out var list))
        {
            list = new List<HoNEquipmentPreset>();
            _state[heroStringId] = list;
        }

        if (list.Count >= _settings.MaxPresetsPerCharacter)
            return SaveOutcome.MaxReached;

        var preset = new HoNEquipmentPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = presetName.Trim(),
            IncludesMount = includeMount,
            IncludesCivilianEquipment = includeCivilian,
        };
        CapturePreset(heroStringId, preset, includeMount, includeCivilian);
        list.Add(preset);

        if (_settings.IsDebugMode)
            _logger.LogDebug($"[EquipPresets] saved '{preset.Name}' for hero {heroStringId} ({preset.Items.Count} battle / {preset.CivilianItems.Count} civilian)");

        return SaveOutcome.Saved;
    }

    public UpdateOutcome UpdatePreset(string heroStringId, string presetId)
    {
        if (!_settings.IsEnabled) return UpdateOutcome.Disabled;
        if (string.IsNullOrEmpty(heroStringId)) return UpdateOutcome.NoActiveHero;
        if (!_slots.HeroExists(heroStringId)) return UpdateOutcome.NoActiveHero;
        if (string.IsNullOrEmpty(presetId)) return UpdateOutcome.NotFound;

        if (!_state.TryGetValue(heroStringId, out var list)) return UpdateOutcome.NotFound;
        var preset = list.Find(p => p.Id == presetId);
        if (preset == null) return UpdateOutcome.NotFound;

        // Preserve toggles; rebuild Items/CivilianItems from the current equipment.
        preset.Items.Clear();
        preset.CivilianItems.Clear();
        CapturePreset(heroStringId, preset, preset.IncludesMount, preset.IncludesCivilianEquipment);

        if (_settings.IsDebugMode)
            _logger.LogDebug($"[EquipPresets] updated '{preset.Name}' for hero {heroStringId}");
        return UpdateOutcome.Updated;
    }

    public PresetLoadResult LoadPresetWithReport(string heroStringId, string presetId)
    {
        if (!_settings.IsEnabled) return PresetLoadResult.ForDisabled();
        if (string.IsNullOrEmpty(heroStringId) || !_slots.HeroExists(heroStringId))
            return PresetLoadResult.ForNoActiveHero();
        if (string.IsNullOrEmpty(presetId)) return PresetLoadResult.ForNotFound();
        if (!_state.TryGetValue(heroStringId, out var list)) return PresetLoadResult.ForNotFound();
        var preset = list.Find(p => p.Id == presetId);
        if (preset == null) return PresetLoadResult.ForNotFound();

        var battleRequests = BuildSlotRequests(preset.Items, preset.IncludesMount);
        var battleResult = _screen.LoadEquipment(battleRequests, civilian: false);

        var civilianResult = LoadEquipmentResult.Empty;
        if (preset.IncludesCivilianEquipment)
        {
            var civilianRequests = BuildSlotRequests(preset.CivilianItems, preset.IncludesMount);
            civilianResult = _screen.LoadEquipment(civilianRequests, civilian: true);
        }

        var equipped = battleResult.EquippedCount + civilianResult.EquippedCount;
        var missingItems = Combine(battleResult.MissingItems, civilianResult.MissingItems);
        var missingModifiers = Combine(battleResult.MissingModifiers, civilianResult.MissingModifiers);
        var invalidSlots = CombineInts(battleResult.InvalidSlots, civilianResult.InvalidSlots);

        if (_settings.IsDebugMode)
            _logger.LogDebug($"[EquipPresets] loaded '{preset.Name}': equipped={equipped} missingItems={missingItems.Count} missingModifiers={missingModifiers.Count} invalidSlots={invalidSlots.Count}");

        return new PresetLoadResult(equipped, missingItems, missingModifiers, invalidSlots,
                                    disabled: false, notFound: false, noActiveHero: false);
    }

    public DeleteOutcome DeletePreset(string heroStringId, string presetId)
    {
        if (!_settings.IsEnabled) return DeleteOutcome.Disabled;
        if (string.IsNullOrEmpty(heroStringId) || string.IsNullOrEmpty(presetId)) return DeleteOutcome.NotFound;
        if (!_state.TryGetValue(heroStringId, out var list)) return DeleteOutcome.NotFound;
        var idx = list.FindIndex(p => p.Id == presetId);
        if (idx < 0) return DeleteOutcome.NotFound;
        var name = list[idx].Name;
        list.RemoveAt(idx);
        if (list.Count == 0) _state.Remove(heroStringId);
        if (_settings.IsDebugMode)
            _logger.LogDebug($"[EquipPresets] deleted '{name}' for hero {heroStringId}");
        return DeleteOutcome.Deleted;
    }

    public int PruneOrphanedEntries(IReadOnlyCollection<string> liveHeroIds)
    {
        if (liveHeroIds == null || liveHeroIds.Count == 0)
        {
            // Empty live-set is a transient load state — never prune. The behavior layer
            // builds the live set from Hero.AllAliveHeroes; an empty result there means we
            // don't yet know who's alive (mid-deserialization), so the safe default is keep all.
            return 0;
        }
        var live = liveHeroIds is HashSet<string> hs ? hs : new HashSet<string>(liveHeroIds, StringComparer.Ordinal);

        var toRemove = new List<string>();
        foreach (var key in _state.Keys)
        {
            if (!live.Contains(key)) toRemove.Add(key);
        }
        foreach (var key in toRemove) _state.Remove(key);
        if (toRemove.Count > 0)
            _logger.LogInfo($"[EquipPresets] Pruned {toRemove.Count} orphaned preset bundle(s)");
        return toRemove.Count;
    }

    // === Internal helpers ===

    private void CapturePreset(string heroStringId, HoNEquipmentPreset preset,
                               bool includeMount, bool includeCivilian)
    {
        var battle = _slots.Capture(heroStringId, civilian: false);
        foreach (var s in battle)
        {
            if (!includeMount && IsMountSlot(s.SlotIndex)) continue;
            preset.Items.Add(new HoNPresetItemReference(s.SlotIndex, s.ItemStringId, s.ItemModifierStringId ?? string.Empty));
        }

        if (includeCivilian)
        {
            var civilian = _slots.Capture(heroStringId, civilian: true);
            foreach (var s in civilian)
            {
                if (!includeMount && IsMountSlot(s.SlotIndex)) continue;
                preset.CivilianItems.Add(new HoNPresetItemReference(s.SlotIndex, s.ItemStringId, s.ItemModifierStringId ?? string.Empty));
            }
        }
    }

    private List<PresetSlotRequest> BuildSlotRequests(List<HoNPresetItemReference> refs, bool includeMount)
    {
        var requests = new List<PresetSlotRequest>(refs.Count);
        foreach (var r in refs)
        {
            if (!includeMount && IsMountSlot(r.SlotIndex)) continue;

            // Validate modifier BEFORE the lookup (per "Lookup Functions With Fallbacks" rule).
            // If pre-validation flags it missing, send null to the adapter so the equip happens
            // bare rather than relying on the adapter to retry the lookup. Race path inside the
            // adapter (modifier exists at validate-time, gone at lookup-time) is also reported
            // by the adapter — caller dedupes via list.
            var modifier = string.IsNullOrEmpty(r.ItemModifierStringId) ? null : r.ItemModifierStringId;
            if (modifier != null && !_modifierLookup.ExistsOrEmpty(modifier))
            {
                modifier = null;
            }
            requests.Add(new PresetSlotRequest(r.SlotIndex, r.ItemStringId, modifier));
        }
        return requests;
    }

    // EquipmentIndex.Horse = 10, EquipmentIndex.HorseHarness = 11. (Verified ilspy 2026-05-06:
    // Equipment.EquipmentSlotLength == 12; Horse exposed as `_itemSlots[10]`.)
    private static bool IsMountSlot(int slotIndex) => slotIndex == 10 || slotIndex == 11;

    private static IReadOnlyList<string> Combine(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0) return b;
        if (b.Count == 0) return a;
        var result = new List<string>(a.Count + b.Count);
        result.AddRange(a);
        result.AddRange(b);
        return result;
    }

    private static IReadOnlyList<int> CombineInts(IReadOnlyList<int> a, IReadOnlyList<int> b)
    {
        if (a.Count == 0) return b;
        if (b.Count == 0) return a;
        var result = new List<int>(a.Count + b.Count);
        result.AddRange(a);
        result.AddRange(b);
        return result;
    }
}
