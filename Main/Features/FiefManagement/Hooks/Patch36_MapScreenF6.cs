using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefManagement.Hooks;

[HarmonyPatch(typeof(MapScreen), "OnFrameTick")]
[HarmonyPatchCategory("Patch36_FiefManagement")]
public static class Patch36_MapScreenF6
{
    private static IFiefManagementSettingsProvider _settings;
    private static IMapScreenInputAdapter _input;
    private static IFiefHubService _service;
    private static Features.Enlistment.IEnlistmentStateQuery _enlistment;
    private static IModLogger _logger;
    private static bool _exceptionLogged;

    [HarmonyPostfix]
    public static void Postfix(MapScreen __instance)
    {
        try
        {
            var settings = _settings ??= IoC.Resolve<IFiefManagementSettingsProvider>();
            if (settings == null || !settings.EnableFiefManagement) return;

            // Guards mirror vanilla MapScreen's full modal-suppression set (see decompiled
            // MapScreen.OnFrameTick line 1355: vanilla suppresses map actions when ANY of these
            // sub-states is active). Codex review #38b caught that the original `IsInMenu`-only
            // guard let F6 fire during army management, marriage popup, map cheats, etc. —
            // each of which can read MainParty.CurrentSettlement and would see the swapped value.
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState)) return;
            if (__instance == null) return;
            if (__instance.IsInMenu) return;
            if (__instance.IsInBattleSimulation) return;
            if (__instance.IsInArmyManagement) return;
            if (__instance.IsMarriageOfferPopupActive) return;
            if (__instance.IsHeirSelectionPopupActive) return;
            if (__instance.IsMapCheatsActive) return;
            if (__instance.IsMapIncidentActive) return;
            if (__instance.IsOverlayContextMenuEnabled) return;
            if (__instance.EncyclopediaScreenManager?.IsEncyclopediaOpen == true) return;

            // Enlisted-attached service is a modal state of its own (parked at the wait menu;
            // the menu guard owns transitions) — F6 must not push fief_hub into it. The other
            // service states (duty/grace/captive) are free-roam and keep F6.
            var enlistment = _enlistment ??= IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>();
            if (enlistment != null && enlistment.State == Features.Enlistment.Domain.EnlistmentState.EnlistedAttached) return;

            var input = _input ??= IoC.Resolve<IMapScreenInputAdapter>();
            if (input == null || !input.IsF6Pressed) return;

            var service = _service ??= IoC.Resolve<IFiefHubService>();
            if (service == null) return;

            if (service.Count <= 0)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=taom_fief_no_fiefs}You don't own any fiefs yet.").ToString(),
                    Colors.Yellow));
                if (settings.IsDebugMode)
                    (_logger ??= IoC.Resolve<IModLogger>())?.LogInfo("[FiefManagement] F6 pressed but player owns no fiefs");
                return;
            }

            if (settings.IsDebugMode)
                (_logger ??= IoC.Resolve<IModLogger>())?.LogInfo($"[FiefManagement] F6 — opening fief_hub menu (count={service.Count})");
            GameMenu.ActivateGameMenu("fief_hub");
        }
        catch (System.Exception ex)
        {
            // F6 polling fires every frame — never throw out of the postfix. Log once per process
            // so a recurring exception is visible without spamming the log.
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try
                {
                    (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapScreenF6.Postfix threw: {ex}");
                }
                catch
                {
                    // If even the logger resolution fails, swallow — never throw from a per-frame postfix.
                }
            }
        }
    }
}
