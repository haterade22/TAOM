using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterCreationContent;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;

namespace TAOM.Features.CharacterCreation;

/// <summary>
/// Rebuilds <see cref="FaceGenVM"/>'s race <see cref="SelectorVM{T}"/> so the dropdown shows
/// only the races allowed for the active character-creation culture, while preserving the
/// engine's global race-index contract via an index-translating onChange wrapper.
///
/// Vanilla <c>FaceGenVM</c> stores the engine's race ID as <c>SelectorVM.SelectedIndex</c>
/// (the dropdown position equals the global race index). Filtering the dropdown shifts
/// indices and breaks that contract — this rebuilder restores it by mutating the
/// SelectorVM's private <c>_selectedIndex</c> to the global value before vanilla
/// <c>OnSelectRace</c> reads it, then restoring it afterward.
///
/// Dependencies (filter service, logger) are passed in from the patch boundary so this
/// helper does not service-locate.
/// </summary>
public static class FaceGenRaceSelectorRebuilder
{
    [ThreadStatic] private static bool _inForceSwitch;

    private static FieldInfo _selectedRaceField;
    private static FieldInfo _selectorVmSelectedIndexField;
    private static FieldInfo _selectorVmSelectedItemField;
    private static FieldInfo _selectorVmOnChangeField;

    // Per-VM session state. Lets us detect "first Apply for this culture" so we can default
    // to cultures.json Races[0] instead of preserving the engine-default _selectedRace
    // (which is 0/human regardless of culture). Subsequent Refresh(true) calls within the
    // same culture preserve the player's actual choice.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FaceGenVM, RaceFilterSession> _sessions
        = new System.Runtime.CompilerServices.ConditionalWeakTable<FaceGenVM, RaceFilterSession>();

    private class RaceFilterSession
    {
        public string LastAppliedCultureId;
    }

    public static void Apply(FaceGenVM faceGenVM, ICultureRaceFilterService filterService)
    {
        if (faceGenVM == null || filterService == null) return;

        var ccState = Game.Current?.GameStateManager?.ActiveState as CharacterCreationState;
        var cultureId = ccState?.CharacterCreationManager?.CharacterCreationContent?.SelectedCulture?.StringId;
        if (string.IsNullOrEmpty(cultureId)) return;

        if (!filterService.HasFilter(cultureId)) return;

        var allRaces = FaceGen.GetRaceNames();
        if (allRaces == null || allRaces.Length == 0) return;

        var allowed = filterService.GetAllowedRaces(cultureId);
        if (allowed.Count == 0 || allowed.Count >= allRaces.Length) return;

        var globalIndices = BuildGlobalIndexMap(allRaces, allowed);
        if (globalIndices.Count == 0 || globalIndices.Count >= allRaces.Length) return;

        EnsureFields();

        var currentSelector = faceGenVM.RaceSelector;
        if (currentSelector == null) return;

        var session = _sessions.GetOrCreateValue(faceGenVM);
        bool firstApplyForThisCulture = !string.Equals(
            session.LastAppliedCultureId, cultureId, StringComparison.OrdinalIgnoreCase);
        session.LastAppliedCultureId = cultureId;

        int currentGlobalRace = (int)_selectedRaceField.GetValue(faceGenVM);
        int filteredSelected = MapGlobalIndexToFiltered(currentGlobalRace, globalIndices);
        bool needToSwitchRace = ShouldForceSwitchToDefault(filteredSelected, firstApplyForThisCulture);
        if (needToSwitchRace) filteredSelected = 0;

        var vanillaOnChange = (Action<SelectorVM<SelectorItemVM>>)_selectorVmOnChangeField.GetValue(currentSelector);
        var wrapped = WrapOnChange(vanillaOnChange, globalIndices);
        var newSelector = BuildSelector(allRaces, globalIndices, filteredSelected, wrapped);

        // Use the public property setter instead of mutating _raceSelector via reflection +
        // invoking the generic OnPropertyChangedWithValue manually. The setter both updates the
        // field and fires the property-change notification the GUI prefab is bound to. Codex
        // review #N (2026-05-06) caught that reflecting a generic method by name+param-types
        // does not produce a constructed method — Invoke would fail at runtime, leaving the UI
        // un-rebound after a successful swap.
        faceGenVM.RaceSelector = newSelector;

        if (needToSwitchRace && !_inForceSwitch)
        {
            _inForceSwitch = true;
            try { wrapped(newSelector); }
            finally { _inForceSwitch = false; }
        }
    }

    /// <summary>
    /// Pure helper: decides whether the dropdown should snap to filtered position 0 (Races[0])
    /// rather than preserving the player's current race selection. Force-switch happens when:
    /// (a) the current race is not in the culture's allow-list (e.g., player switched culture
    /// from Mordor to Erebor with uruk still selected), OR
    /// (b) this is the first Apply for the active culture AND the player's current race is not
    /// already at filtered position 0 (i.e., the engine-default `_selectedRace = 0` lands the
    /// player on a non-canonical race because human happens to be in the allow-list).
    /// Subsequent Apply calls on the same culture preserve the player's choice.
    /// </summary>
    public static bool ShouldForceSwitchToDefault(int currentFilteredIdx, bool firstApplyForThisCulture)
    {
        bool currentRaceNotAllowed = currentFilteredIdx < 0;
        bool currentRaceNotDefault = currentFilteredIdx != 0;
        return currentRaceNotAllowed || (firstApplyForThisCulture && currentRaceNotDefault);
    }

    /// <summary>
    /// Pure helper: maps a filtered dropdown position to the engine's global race index.
    /// Returns -1 if the filtered index is out of range.
    /// </summary>
    public static int MapFilteredIndexToGlobal(int filteredIdx, IReadOnlyList<int> globalIndices)
    {
        if (globalIndices == null) return -1;
        if (filteredIdx < 0 || filteredIdx >= globalIndices.Count) return -1;
        return globalIndices[filteredIdx];
    }

    /// <summary>
    /// Pure helper: maps a global race index back to its position in the filtered dropdown.
    /// Returns -1 if the global race is not in the allowed set.
    /// </summary>
    public static int MapGlobalIndexToFiltered(int globalIdx, IReadOnlyList<int> globalIndices)
    {
        if (globalIndices == null) return -1;
        for (int i = 0; i < globalIndices.Count; i++)
            if (globalIndices[i] == globalIdx) return i;
        return -1;
    }

    /// <summary>
    /// Pure helper: builds the filtered → global index map. Iterates the allow-list (config order)
    /// and resolves each name to the engine's global race index. Result preserves the order in
    /// cultures.json `races[]` so the dropdown shows the culture-canonical race first
    /// (e.g., Mordor → uruk, then orc, then human — not human first because the engine puts
    /// human at index 0). Names not present in the engine's race list are skipped silently.
    /// Comparison is case-insensitive.
    /// </summary>
    public static IReadOnlyList<int> BuildGlobalIndexMap(string[] allRaces, IReadOnlyList<string> allowed)
    {
        var map = new List<int>(allowed?.Count ?? 0);
        if (allRaces == null || allowed == null || allowed.Count == 0) return map;

        var engineLookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allRaces.Length; i++)
            engineLookup[allRaces[i]] = i;

        for (int i = 0; i < allowed.Count; i++)
        {
            if (engineLookup.TryGetValue(allowed[i], out int globalIdx))
                map.Add(globalIdx);
        }
        return map;
    }

    private static Action<SelectorVM<SelectorItemVM>> WrapOnChange(
        Action<SelectorVM<SelectorItemVM>> vanillaOnChange,
        IReadOnlyList<int> globalIndices)
    {
        return s =>
        {
            int globalIdx = MapFilteredIndexToGlobal(s.SelectedIndex, globalIndices);
            if (globalIdx < 0)
            {
                vanillaOnChange?.Invoke(s);
                return;
            }
            var saved = (int)_selectorVmSelectedIndexField.GetValue(s);
            _selectorVmSelectedIndexField.SetValue(s, globalIdx);
            try { vanillaOnChange?.Invoke(s); }
            finally { _selectorVmSelectedIndexField.SetValue(s, saved); }
        };
    }

    private static SelectorVM<SelectorItemVM> BuildSelector(
        string[] allRaces,
        IReadOnlyList<int> globalIndices,
        int filteredSelected,
        Action<SelectorVM<SelectorItemVM>> wrapped)
    {
        var selector = new SelectorVM<SelectorItemVM>(filteredSelected, null);
        for (int i = 0; i < globalIndices.Count; i++)
            selector.AddItem(new SelectorItemVM(allRaces[globalIndices[i]]));

        _selectorVmSelectedIndexField.SetValue(selector, filteredSelected);
        var item = selector.ItemList[filteredSelected];
        item.IsSelected = true;
        _selectorVmSelectedItemField.SetValue(selector, item);
        selector.SetOnChangeAction(wrapped);
        return selector;
    }

    private static void EnsureFields()
    {
        if (_selectedRaceField != null) return;

        _selectedRaceField = AccessTools.Field(typeof(FaceGenVM), "_selectedRace");

        var selectorVmType = typeof(SelectorVM<SelectorItemVM>);
        _selectorVmSelectedIndexField = AccessTools.Field(selectorVmType, "_selectedIndex");
        _selectorVmSelectedItemField = AccessTools.Field(selectorVmType, "_selectedItem");
        _selectorVmOnChangeField = AccessTools.Field(selectorVmType, "_onChange");
    }
}
