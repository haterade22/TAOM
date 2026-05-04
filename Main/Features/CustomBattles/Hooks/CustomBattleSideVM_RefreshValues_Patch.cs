using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleSideVM), nameof(CustomBattleSideVM.RefreshValues))]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_RefreshValues_Patch
{
    private static ISideCommanderFilter _filter;
    private static IModLogger _logger;

    public static void Initialize(ISideCommanderFilter filter, IModLogger logger)
    {
        _filter = filter;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(CustomBattleSideVM __instance)
    {
        try
        {
            if (_filter == null || __instance?.CharacterSelectionGroup == null)
                return;

            var selectedFaction = __instance.FactionSelectionGroup?.SelectedItem?.Faction;
            if (selectedFaction == null)
                return;

            var commanders = _filter.ResolveCommandersForCulture(selectedFaction.StringId);
            if (commanders.Count == 0)
            {
                _logger?.LogWarning($"[CustomBattles] No commanders matched culture '{selectedFaction.StringId}' on RefreshValues — dropdown left unfiltered. Verify lords.xml culture tags align with the BasicCultureObject.StringId surfaced by TaomFactionSelectionVM.");
                return;
            }

            CommanderSelectorRebuilder.Apply(__instance.CharacterSelectionGroup, commanders);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] RefreshValues patch error: {ex.Message}");
        }
    }
}
