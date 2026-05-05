using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles.Hooks;

internal static class CommanderSelectorRebuilder
{
    private static FieldInfo _onChangeField;
    private static IModLogger _logger;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
        _onChangeField = AccessTools.Field(typeof(SelectorVM<CharacterItemVM>), "_onChange");
        if (_onChangeField == null)
        {
            _logger?.LogError(
                "[CustomBattles] AccessTools.Field could not resolve SelectorVM<CharacterItemVM>._onChange. " +
                "Commander dropdown rebuild will be skipped to avoid permanently severing OnCharacterSelection wiring. " +
                "Likely cause: TaleWorlds renamed the backing field in a game update — check SelectorVM<T> in TaleWorlds.Core.ViewModelCollection.dll.");
        }
    }

    public static void Apply(SelectorVM<CharacterItemVM> selector, IReadOnlyList<BasicCharacterObject> commanders)
    {
        if (selector == null || commanders == null || commanders.Count == 0)
            return;

        // If reflection failed at Initialize, bail loudly rather than passing null to Refresh,
        // which would set _onChange = null and permanently break OnCharacterSelection wiring.
        if (_onChangeField == null)
        {
            _logger?.LogError("[CustomBattles] CommanderSelectorRebuilder.Apply skipped — _onChangeField unresolved. See Initialize warning.");
            return;
        }

        // Vanilla SelectorVM<T>.Refresh handles the entire safe-rebuild sequence:
        // ItemList.Clear() -> _selectedIndex = -1 (direct field) -> AddItem loop ->
        // HasSingleItem update -> _onChange = onChange -> SelectedIndex = selectedIndex.
        // We must preserve the existing _onChange (vanilla OnCharacterSelection bound to the
        // side VM instance) -- read it via reflection and pass it back so Refresh's overwrite
        // is a no-op on the wiring.
        var existingOnChange = (Action<SelectorVM<CharacterItemVM>>)_onChangeField.GetValue(selector);
        var items = commanders.Select(c => new CharacterItemVM(c));
        selector.Refresh(items, 0, existingOnChange);
    }
}
