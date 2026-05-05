using HarmonyLib;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleSideVM), nameof(CustomBattleSideVM.UpdateCharacterVisual))]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_UpdateCharacterVisual_Patch
{
    // Companion guard for CustomBattleSideVM_OnCharacterSelection_Patch.
    // Vanilla UpdateCharacterVisual derefs SelectedCharacter.Equipment[(EquipmentIndex)5] directly.
    // RefreshValues() calls UpdateCharacterVisual() unconditionally after SelectedIndex assignment;
    // when our OnCharacterSelection Prefix skipped the body (because the SelectorVM had a null SelectedItem),
    // SelectedCharacter is never set, and UpdateCharacterVisual NREs on the equipment indexer.
    // Returning false here aborts the visual update cleanly, letting RefreshValues continue past it.
    [HarmonyPrefix]
    public static bool Prefix(CustomBattleSideVM __instance)
        => __instance?.SelectedCharacter != null;
}
