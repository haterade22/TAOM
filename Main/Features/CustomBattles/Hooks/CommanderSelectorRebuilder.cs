using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles.Hooks;

internal static class CommanderSelectorRebuilder
{
    private static FieldInfo _selectedIndexField;

    public static void Initialize()
    {
        _selectedIndexField = AccessTools.Field(typeof(SelectorVM<CharacterItemVM>), "_selectedIndex");
    }

    public static void Apply(SelectorVM<CharacterItemVM> selector, IReadOnlyList<BasicCharacterObject> commanders)
    {
        if (selector == null || commanders == null || commanders.Count == 0)
            return;

        ((Collection<CharacterItemVM>)(object)selector.ItemList).Clear();
        foreach (var commander in commanders)
        {
            selector.AddItem(new CharacterItemVM(commander));
        }

        // SelectorVM<T>.SelectedIndex setter early-returns when value == _selectedIndex,
        // leaving SelectedItem pointing at a CharacterItemVM no longer in ItemList.
        // Reset _selectedIndex directly (mirrors vanilla SelectorVM.Refresh) so the
        // SelectedIndex = 0 setter actually fires, updates SelectedItem, and invokes
        // OnCharacterSelection -- which propagates SelectedCharacter to CustomBattleSideVM.
        _selectedIndexField?.SetValue(selector, -1);
        selector.SelectedIndex = 0;
    }
}
