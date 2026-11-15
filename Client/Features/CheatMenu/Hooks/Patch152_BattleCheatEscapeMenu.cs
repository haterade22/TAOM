using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer;
using TaleWorlds.MountAndBlade.ViewModelCollection.EscapeMenu;
using TAOM.Core.Logging;
using TAOM.Features.CheatMenu.Battle;

namespace TAOM.Features.CheatMenu.Hooks;

/// <summary>
/// Patch152 (docs/reference/harmony-patch-registry.md, "Patch152_BattleCheatMenu"): postfix on the protected
/// <c>MissionGauntletSingleplayerEscapeMenu.GetEscapeMenuItems()</c>, which builds the single-player Esc menu's items each time
/// it opens and when Hide Battle UI changes. While the battle cheat menu is shown (MCM switch on, single player) it inserts one
/// "Cheats" item just before Photo Mode, the place where vanilla adds its gamepad "Cheat Menu" item. A click closes the Esc menu
/// (the public <c>OnEscape</c>, which toggles an open menu shut as "Return to the Game" does) and opens the battle list. In
/// TAOM.Client because the target is client-only (ADR-013). Never throws out of the Esc menu: a failure is logged once and
/// leaves vanilla's items.
/// </summary>
[HarmonyPatch(typeof(MissionGauntletSingleplayerEscapeMenu), "GetEscapeMenuItems")]
[HarmonyPatchCategory(CheatMenuModule.BattlePatchCategory)]
public static class Patch152_BattleCheatEscapeMenu
{
    private static BattleCheatOpener? _opener;
    private static bool _exceptionLogged;

    [HarmonyPostfix]
    public static void Postfix(MissionGauntletSingleplayerEscapeMenu __instance, List<EscapeMenuItemVM> __result)
    {
        try
        {
            if (__result == null) return;
            var opener = _opener ??= IoC.Resolve<BattleCheatOpener>();
            if (opener == null) return;

            var index = BattleCheatOpener.EscapeItemIndex(opener.IsShown,
                __result.Select(item => item?.ActionText).ToList(),
                new TextObject("{=VklN5Wm6}Photo Mode").ToString(),
                BattleCheatTexts.Item().ToString());
            if (index < 0) return;

            var menu = __instance;
            __result.Insert(index, new EscapeMenuItemVM(BattleCheatTexts.Item(), _ => OnClick(menu, opener), null,
                () => new Tuple<bool, TextObject>(false, null!)));
        }
        catch (Exception ex)
        {
            if (_exceptionLogged) return;
            _exceptionLogged = true;
            try { IoC.Resolve<IModLogger>()?.LogError($"[CheatMenu] Patch152_BattleCheatEscapeMenu.Postfix threw: {ex}"); }
            catch { /* swallow-ok: never throw out of the Esc menu's item list */ }
        }
    }

    // Inside the campaign menu's guard: a failure shows a warning instead of escaping the Esc menu's click.
    private static void OnClick(MissionGauntletSingleplayerEscapeMenu menu, BattleCheatOpener opener) =>
        CheatMenuMenus.Guarded("battle_open", () =>
        {
            if (menu.IsActive) menu.OnEscape();
            opener.TryOpen();
        });
}
