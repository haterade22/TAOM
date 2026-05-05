using HarmonyLib;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle.SelectionItem;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleSideVM), "OnCharacterSelection")]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_OnCharacterSelection_Patch
{
    // Vanilla OnCharacterSelection dereferences selector.SelectedItem.Character without a null guard.
    // SelectorVM<T>.SelectedIndex setter fires _onChange.Invoke even when GetCurrentItem returns null
    // (empty ItemList or out-of-range index). Without this guard, vanilla NREs.
    [HarmonyPrefix]
    public static bool Prefix(SelectorVM<CharacterItemVM> selector)
        => selector?.SelectedItem != null;
}
