using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch66 (#830): the encounter menu's 'Send troops' / 'Leave it to the others' option let an
/// enlisted soldier auto-resolve his commander's battle and still be paid. Vanilla's
/// <c>MenuHelper.EncounterOrderAttackCondition</c> counts healthy men across the whole side, so the
/// option appears even when his own party is empty. This postfix on the menu's private wrapper
/// (<c>EncounterGameMenuBehavior.game_menu_encounter_order_attack_on_condition</c>) lets
/// <see cref="IEnlistmentMenuService.EvaluateSitOut"/> decide.
///
/// It only narrows. It returns at once unless vanilla showed the option AND left it enabled, it never
/// sets the result to false (that would hide the option), never re-enables a disabled option and adds
/// no second option; it writes only <c>args.IsEnabled = false</c> and <c>args.Tooltip</c>. The verdict
/// disables the option only while vanilla's 'Attack' is available (the policy invariant), so the
/// player always keeps an enabled way out. Vanilla's other consequence path, the town assault init, is
/// untouched.
///
/// Fails open on any error and logs once. Co-op: a postfix on a local menu predicate that skips no
/// replicated mutation; a client never holds EnlistedBattle, so the gate is inert there.
/// </summary>
[HarmonyPatch(typeof(EncounterGameMenuBehavior), "game_menu_encounter_order_attack_on_condition")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class EncounterGameMenuOrderAttackConditionPatch
{
    private static IEnlistmentMenuService _menuService;
    private static bool _faultLogged;

    public static void Postfix(MenuCallbackArgs args, ref bool __result)
    {
        if (!__result || args == null || !args.IsEnabled)
            return;

        try
        {
            _menuService ??= IoC.Resolve<IEnlistmentMenuService>();
            Apply(_menuService.EvaluateSitOut(), args);
        }
        catch (Exception ex)
        {
            if (!_faultLogged)
            {
                _faultLogged = true;
                try { IoC.Resolve<IModLogger>().LogError($"[Enlistment] game_menu_encounter_order_attack_on_condition postfix failed (failing open): {ex.Message}"); }
                catch { /* fail open: never break the encounter menu */ }
            }
        }
    }

    /// <summary>
    /// Writes the verdict into the menu args: IsEnabled only ever goes to false, plus the tooltip. A
    /// refused option replaces the tooltip; an allowed one keeps vanilla's power-context tooltip
    /// under TAOM's line.
    /// </summary>
    private static void Apply(SitOutVerdict verdict, MenuCallbackArgs args)
    {
        switch (verdict)
        {
            case SitOutVerdict.WoundedFree:
                args.Tooltip = Compose(new TextObject("{=taom_enlist_sitout_wounded_tip}You are wounded. The column fights on without you, and no one will hold it against you."), args.Tooltip);
                break;
            case SitOutVerdict.OverwhelmingCost:
                args.Tooltip = Compose(new TextObject("{=taom_enlist_sitout_odds_tip}Your side holds the field by a wide margin. You may stay back, but your commander will remember it."), args.Tooltip);
                break;
            case SitOutVerdict.RefusedField:
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=taom_enlist_sitout_refused_tip}You serve in your commander's line. Take your place in the fight.");
                break;
            case SitOutVerdict.RefusedSiege:
                args.IsEnabled = false;
                args.Tooltip = new TextObject("{=taom_enlist_sitout_siege_tip}A siege needs every soldier. Take your place in the assault.");
                break;
        }
    }

    /// <summary>TAOM's line on top, vanilla's tooltip (the power-context line) below it when there is one.</summary>
    internal static TextObject Compose(TextObject taom, TextObject vanilla)
    {
        if (TextObject.IsNullOrEmpty(vanilla))
            return taom;

        return GameTexts.FindText("str_string_newline_string")
            .SetTextVariable("STR1", taom)
            .SetTextVariable("STR2", vanilla);
    }
}
