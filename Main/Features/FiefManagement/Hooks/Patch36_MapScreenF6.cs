using HarmonyLib;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.FiefManagement.Hooks;

/// <summary>
/// F6 on the campaign map opens the fief hub. The map-state and modal guards live in
/// <see cref="MapMenuGate"/>, which the field camp's "Make camp" query shares; this postfix adds the MCM
/// toggle, the key, the enlisted-attached refusal and the "no fiefs" message.
/// </summary>
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
    public static void Postfix()
    {
        try
        {
            var settings = _settings ??= IoC.Resolve<IFiefManagementSettingsProvider>();
            if (settings == null || !settings.EnableFiefManagement) return;

            // The key first, so the gate and the vanilla helper it calls run only on a press.
            var input = _input ??= IoC.Resolve<IMapScreenInputAdapter>();
            if (input == null || !input.IsF6Pressed) return;

            // The one map-menu gate: vanilla MapScreen's full modal set (v1.4.8 MapScreen.cs:1355), the
            // escape menu, a live menu context and vanilla's map-action check. Codex review #38b caught that an
            // IsInMenu-only guard let F6 fire during modals that read the swapped MainParty.CurrentSettlement.
            if (!MapMenuGate.IsMapClearForMenu()) return;

            // Enlisted-attached service is a modal state of its own (parked at the wait menu; the menu guard
            // owns transitions): F6 must not push fief_hub into it. The other service states (duty, grace,
            // captive) are free-roam and keep F6.
            var enlistment = _enlistment ??= IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>();
            if (enlistment != null && enlistment.State == Features.Enlistment.Domain.EnlistmentState.EnlistedAttached) return;

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
                (_logger ??= IoC.Resolve<IModLogger>())?.LogInfo($"[FiefManagement] F6: opening fief_hub menu (count={service.Count})");
            GameMenu.ActivateGameMenu("fief_hub");
        }
        catch (System.Exception ex)
        {
            // F6 polling fires every frame: never throw out of the postfix. Log once per process so a
            // recurring exception is visible without spamming the log.
            if (!_exceptionLogged)
            {
                _exceptionLogged = true;
                try
                {
                    (_logger ??= IoC.Resolve<IModLogger>())?.LogError($"[FiefManagement] Patch36_MapScreenF6.Postfix threw: {ex}");
                }
                catch
                {
                    // If even the logger resolution fails, swallow: never throw from a per-frame postfix.
                }
            }
        }
    }
}
