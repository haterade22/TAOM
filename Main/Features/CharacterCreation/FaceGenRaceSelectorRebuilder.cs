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

        int currentGlobalRace = (int)_selectedRaceField.GetValue(faceGenVM);
        int filteredSelected = MapGlobalIndexToFiltered(currentGlobalRace, globalIndices);
        bool needToSwitchRace = filteredSelected < 0;
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
    /// Pure helper: builds the filtered → global index map by intersecting the engine's
    /// race list with the allow-list (case-insensitive).
    /// </summary>
    public static IReadOnlyList<int> BuildGlobalIndexMap(string[] allRaces, IReadOnlyList<string> allowed)
    {
        var map = new List<int>(allowed?.Count ?? 0);
        if (allRaces == null || allowed == null || allowed.Count == 0) return map;
        var allowedSet = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allRaces.Length; i++)
            if (allowedSet.Contains(allRaces[i])) map.Add(i);
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
