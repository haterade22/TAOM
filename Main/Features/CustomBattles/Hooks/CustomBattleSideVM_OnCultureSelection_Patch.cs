using System;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

// Phase 9b #162 — v1.3.15 verified via ilspycmd on
// Modules/CustomBattle/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.CustomBattle.dll:
//   private void OnCultureSelection(BasicCultureObject selectedCulture)
// Exact signature match. Type lives in TaleWorlds.MountAndBlade.CustomBattle (NOT
// TaleWorlds.MountAndBlade or SandBox.GauntletUI — v1.4 decompile may show this under a different
// namespace path). If a future TaleWorlds refactor moves the type, AccessTools.Method returns
// null and Harmony throws at PatchCategory("Patch19_CustomBattles") application time, which
// would cascade to break all sibling Patch19 patches.
[HarmonyPatch(typeof(CustomBattleSideVM), "OnCultureSelection", new[] { typeof(BasicCultureObject) })]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_OnCultureSelection_Patch
{
    private static ISideCommanderFilter _filter;
    private static IModLogger _logger;

    public static void Initialize(ISideCommanderFilter filter, IModLogger logger)
    {
        _filter = filter;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(CustomBattleSideVM __instance, BasicCultureObject selectedCulture)
    {
        try
        {
            if (_filter == null || __instance?.CharacterSelectionGroup == null || selectedCulture == null)
                return;

            var commanders = _filter.ResolveCommandersForCulture(selectedCulture.StringId);
            if (commanders.Count == 0)
            {
                _logger?.LogWarning($"[CustomBattles] No commanders matched culture '{selectedCulture.StringId}' — dropdown left unfiltered. Verify lords.xml culture tags align with the BasicCultureObject.StringId surfaced by TaomFactionSelectionVM.");
                return;
            }

            CommanderSelectorRebuilder.Apply(__instance.CharacterSelectionGroup, commanders);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] OnCultureSelection patch error: {ex.Message}");
        }
    }
}
