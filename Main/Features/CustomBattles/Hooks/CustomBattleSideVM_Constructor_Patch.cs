using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.CustomBattle;
using TaleWorlds.MountAndBlade.CustomBattle.CustomBattle;

namespace TAOM.Features.CustomBattles.Hooks;

[HarmonyPatch(typeof(CustomBattleSideVM), MethodType.Constructor,
    new[] { typeof(TextObject), typeof(bool), typeof(TroopTypeSelectionPopUpVM), typeof(Action) })]
[HarmonyPatchCategory("Patch19_CustomBattles")]
public static class CustomBattleSideVM_Constructor_Patch
{
    private static IModLogger _logger;
    private static MethodInfo _onCultureSelectionMethod;

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
        _onCultureSelectionMethod = AccessTools.Method(typeof(CustomBattleSideVM), "OnCultureSelection");
        if (_onCultureSelectionMethod == null)
            _logger?.LogWarning("[CustomBattles] Could not resolve CustomBattleSideVM.OnCultureSelection MethodInfo at Initialize");
    }

    [HarmonyPostfix]
    public static void Postfix(CustomBattleSideVM __instance)
    {
        try
        {
            if (_onCultureSelectionMethod == null)
                return;

            var callback = (Action<BasicCultureObject>)Delegate.CreateDelegate(
                typeof(Action<BasicCultureObject>), __instance, _onCultureSelectionMethod);

            __instance.FactionSelectionGroup = new TaomFactionSelectionVM(callback);
            _logger?.LogInfo($"[CustomBattles] Injected TaomFactionSelectionVM with {__instance.FactionSelectionGroup.Factions.Count} factions");

            var initialFaction = __instance.FactionSelectionGroup.SelectedItem?.Faction;
            if (initialFaction != null)
                callback(initialFaction);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[CustomBattles] Failed to inject TaomFactionSelectionVM: {ex.Message}");
        }
    }
}
