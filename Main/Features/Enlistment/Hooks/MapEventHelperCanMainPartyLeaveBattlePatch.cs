using System;
using HarmonyLib;
using Helpers;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch66 (#827): hides vanilla 'Leave' and 'Abandon army' from the encounter menu while an
/// enlisted soldier serves in his commander's battle. The soldier joins inside the commander's
/// army, or without one when the merge fails (#495); 'Abandon army' leaves a stray army and the
/// latched EnlistedBattle state, 'Leave' leaves the latched state.
///
/// <c>MapEventHelper.CanMainPartyLeaveBattleCommonCondition()</c> is read by exactly three
/// <c>EncounterGameMenuBehavior</c> conditions: leave, abandon army and surrender (pinned by
/// <c>Patch66EnlistmentBindingTests</c>). The postfix changes the result only on the attacker
/// side (the predicate's defender branch needs a party with a siege event that is not on the
/// besieger side, and <c>MobileParty.SiegeEvent</c> is <c>BesiegerCamp?.SiegeEvent</c>, so that
/// branch is unreachable in v1.5.5). Surrender's predicate clause also needs the defender side, so
/// the postfix never changes surrender. Its other clauses (naval auto-travel; a wounded hero at
/// zero party morale) do not read the predicate, so a wounded soldier at zero morale still sees
/// Surrender, as in vanilla. 'Send troops' does not read the predicate; when vanilla's honour check
/// (a safe-passage truce) greys out both 'Attack' and 'Send troops', the gate releases the exits so
/// the menu never loses its last option.
///
/// Not closed here: the <c>join_encounter</c> menu's abandon option (TAOM joins directly and does
/// not reach that menu), the <c>raiding_village</c> menu's 'Leave Army', 'Abandon Army' and 'End
/// Raiding' options (#831: a raid the commander runs is no longer joined, so they are reachable only
/// in the army-attached edge case, where 'Abandon Army' ends the commander's whole raid, when the
/// raid read fails open, and possibly after a relief battle (#954, docs/features/enlistment.md,
/// Open risks 1)), and the encounter menu's sally-out 'Return to {SETTLEMENT}'
/// option (likely unreachable: attacker joins leave a besieged settlement first).
///
/// The postfix only turns true into false. The verdict lives in
/// <see cref="IEnlistmentMenuService.ShouldBlockBattleExit"/>, which starts from the same
/// <c>BattleCommandPolicy</c> predicate as the command strip and then applies the truce release.
/// Fails open on any error. Co-op: a postfix on a local menu predicate that skips no replicated
/// mutation, so it is not a veto prefix; a client never holds EnlistedBattle, so the gate is inert there.
/// </summary>
[HarmonyPatch(typeof(MapEventHelper), nameof(MapEventHelper.CanMainPartyLeaveBattleCommonCondition))]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class MapEventHelperCanMainPartyLeaveBattlePatch
{
    private static IEnlistmentMenuService _menuService;
    private static bool _faultLogged;

    public static void Postfix(ref bool __result)
    {
        if (!__result)
            return;

        try
        {
            _menuService ??= IoC.Resolve<IEnlistmentMenuService>();
            if (_menuService.ShouldBlockBattleExit())
                __result = false;
        }
        catch (Exception ex)
        {
            if (!_faultLogged)
            {
                _faultLogged = true;
                try { IoC.Resolve<IModLogger>().LogError($"[Enlistment] CanMainPartyLeaveBattleCommonCondition postfix failed (failing open): {ex.Message}"); }
                catch { /* swallow-ok: the logger itself threw; the postfix still fails open */ }
            }
        }
    }
}
