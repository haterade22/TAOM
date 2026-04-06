using HarmonyLib;
using TaleWorlds.CampaignSystem.CharacterCreationContent;

namespace TAOM.Features.FactionMap.Hooks;

/// <summary>
/// Fixes a vanilla crash when TrySwitchToNextMenu is called but no option was selected
/// for the current menu (happens with custom cultures that have no valid narrative options).
/// Without this patch, SelectedOptions[CurrentMenu] throws KeyNotFoundException.
/// </summary>
[HarmonyPatch(typeof(CharacterCreationManager), nameof(CharacterCreationManager.TrySwitchToNextMenu))]
[HarmonyPatchCategory("Patch7_FactionMap")]
public static class TrySwitchToNextMenu_Patch
{
    [HarmonyPrefix]
    static bool Prefix(CharacterCreationManager __instance, ref bool __result)
    {
        if (!__instance.SelectedOptions.ContainsKey(__instance.CurrentMenu))
        {
            // No option selected for this menu — skip consequence and find next menu
            string stringId = __instance.CurrentMenu.StringId;
            foreach (var narrativeMenu in __instance.NarrativeMenus)
            {
                if (narrativeMenu.InputMenuId.Equals(stringId))
                {
                    var currentMenuProp = AccessTools.Property(typeof(CharacterCreationManager), nameof(CharacterCreationManager.CurrentMenu));
                    currentMenuProp.SetValue(__instance, narrativeMenu);
                    // Vanilla calls ModifyMenuCharacters() after advancing — preserve that side effect
                    var modifyMethod = AccessTools.Method(typeof(CharacterCreationManager), "ModifyMenuCharacters");
                    modifyMethod?.Invoke(__instance, null);
                    __result = true;
                    return false; // skip original
                }
            }
            __result = false;
            return false; // skip original
        }
        return true; // run original
    }
}
