using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles.Hooks;

internal static class CommanderSelectorRebuilder
{
    private static FieldInfo _onChangeField;

    public static void Initialize()
    {
        _onChangeField = AccessTools.Field(typeof(SelectorVM<CharacterItemVM>), "_onChange");
    }

    public static void Apply(SelectorVM<CharacterItemVM> selector, IReadOnlyList<BasicCharacterObject> commanders)
    {
        if (selector == null || commanders == null || commanders.Count == 0)
            return;

        // Vanilla SelectorVM<T>.Refresh handles the entire safe-rebuild sequence:
        // ItemList.Clear() -> _selectedIndex = -1 (direct field) -> AddItem loop ->
        // HasSingleItem update -> _onChange = onChange -> SelectedIndex = selectedIndex.
        // We must preserve the existing _onChange (vanilla OnCharacterSelection bound to the
        // side VM instance) -- read it via reflection and pass it back so Refresh's overwrite
        // is a no-op on the wiring.
        var existingOnChange = (Action<SelectorVM<CharacterItemVM>>)_onChangeField?.GetValue(selector);
        var items = commanders.Select(c => new CharacterItemVM(c));
        selector.Refresh(items, 0, existingOnChange);
    }
}
