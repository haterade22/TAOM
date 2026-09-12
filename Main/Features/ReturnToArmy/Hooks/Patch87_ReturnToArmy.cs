using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;

namespace TAOM.Features.ReturnToArmy.Hooks;

/// <summary>
/// Patch87: "Return to Army" leaves the town or castle when the army is not here (#566).
///
/// THE TRAP. Vanilla's <c>PlayerTownVisitCampaignBehavior.game_menu_return_to_army_on_consequence</c>
/// (installed v1.4.8, :368-376) switches to <c>army_wait_at_settlement</c> and calls
/// <c>PlayerEncounter.LeaveSettlement()</c> + <c>Finish()</c> ONLY for a village. In a town or castle
/// it never leaves, and <c>game_menu_town_town_leave_on_condition</c> (:994-1006) hides "Leave" for
/// every army member who is not its leader. That is fine while the army is physically here, with
/// the player merged into it (<c>AttachedTo != null</c>): the army rests in the settlement and
/// <c>LeaveSettlementAction.ApplyForParty(leader)</c> (:13-26) finishes the player's encounter when
/// the leader moves. It is a dead end for a member who is NOT attached (<c>Army != null</c>,
/// <c>AttachedTo == null</c>): the wait menu's first tick (<c>PlayerArmyWaitBehavior</c> :181-196)
/// re-routes through <c>DefaultEncounterGameMenuModel.GetGenericStateMenu()</c>, so a
/// foreign-faction fortification bounces to <c>town_wait_menus</c> (:295-298, "You are waiting in
/// X", whose only button returns to the town menu) and a same-faction one stays on
/// <c>army_wait_at_settlement</c> (:291-294) for good, because <c>Army.Tick</c> (:379-398) merges
/// only a party that is marching at the leader.
///
/// HOW A PLAYER GETS THERE. A Player Switcher takeover inherits the lord's <c>Army</c> untouched,
/// and a lord marching to join an army is exactly this state. Vanilla reaches it on its own through
/// the "we will wait for other parties around X, then follow us" line
/// (<c>LordConversationsCampaignBehavior</c> :3510-3514). <c>PlayerEncounter.Init</c> (:715-718)
/// then enters any non-hostile settlement immediately, so the town menu is reachable on foot.
///
/// THE FIX replicates vanilla's own "Leave" (<c>game_menu_settlement_leave_on_consequence</c>,
/// :1054-1068) for that one row: gate position, <c>LeaveSettlement</c>, <c>Finish</c>,
/// <c>SetMoveModeHold</c>, autosave signal. Its <c>AttachedParties</c> loop is consciously dropped:
/// parties attach to the LEADER, and the leader is never offered this option. The player stays in
/// the army (no <c>Army</c> write) and is free on the map; vanilla's "follow us" already leaves the
/// march to him. Every other row runs vanilla untouched.
///
/// ERROR PATHS. Up to and including the gate-position write, a throw defers to vanilla: nothing
/// vanilla reads has changed, the old wait menu is a dead end but not a crash, and the error is
/// logged. From <c>LeaveSettlement</c> on, vanilla is NOT safe: it has nulled
/// <c>CurrentSettlement</c>, which vanilla's :371 dereferences. So a throw past that point skips
/// vanilla and is logged as an error.
///
/// TIME. <c>Finish</c> only forces <c>TimeControlMode = Stop</c> for a party with no army (:1015),
/// but it calls <c>GameMenu.ExitToLast</c> while the menu is still up (:1019-1021), and that sets
/// Stop unconditionally (<c>GameMenu.cs</c> :376-378). The player lands on the map paused, the same
/// as vanilla's own Leave.
/// </summary>
[HarmonyPatch(typeof(PlayerTownVisitCampaignBehavior), "game_menu_return_to_army_on_consequence")]
[HarmonyPatchCategory(Category)]
public static class Patch87_ReturnToArmy
{
    internal const string Category = "Patch87_ReturnToArmy";

    private static IModLogger? _logger;

    internal static void Initialize(IModLogger logger) => _logger = logger;

    /// <summary>Test seam. The logger is process-global, so a reload must not keep the old one.</summary>
    internal static void ResetForUnload() => _logger = null;

    [HarmonyPrefix]
    public static bool Prefix()
    {
        MobileParty? main;
        Settlement? settlement;
        Army? army;
        try
        {
            main = MobileParty.MainParty;
            settlement = main?.CurrentSettlement;
            army = main?.Army;

            // Vanilla's :371 dereferences CurrentSettlement unguarded, so a null here is vanilla's
            // problem on vanilla's stack, not a state this patch can improve on.
            if (main == null || settlement == null)
                return true;

            var verdict = ReturnToArmyRules.Decide(
                inArmy: army != null,
                isArmyLeader: army != null && army.LeaderParty == main,
                attachedToArmy: main.AttachedTo != null,
                inVillage: settlement.IsVillage);

            if (verdict == ReturnToArmyRules.Verdict.RunVanilla)
                return true;

            // Vanilla's Leave moves the party to the gate first. Still on the deferrable side of
            // the line: the party is inside the settlement, so its map position is not read by
            // anything vanilla's own body touches, and CurrentSettlement is intact.
            main.Position = settlement.GatePosition;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ReturnToArmy] could not read the party's army state or place it at the gate, deferring to vanilla's wait menu: {ex}");
            return true;
        }

        // Point of no return. LeaveSettlement nulls the CurrentSettlement vanilla's own body
        // dereferences, so every exit from here skips it.
        try
        {
            PlayerEncounter.LeaveSettlement();
            PlayerEncounter.Finish();
            main.SetMoveModeHold();
            Campaign.Current.SaveHandler.SignalAutoSave();

            _logger?.LogInfo(
                $"[ReturnToArmy] left {settlement.StringId} as an unattached member of " +
                $"{army?.LeaderParty?.StringId ?? "?"}'s army; vanilla would have opened the wait menu");
        }
        catch (Exception ex)
        {
            _logger?.LogError(
                "[ReturnToArmy] the leave sequence threw after mutating; vanilla is skipped because its " +
                $"own body would now dereference a null CurrentSettlement: {ex}");
        }

        return false;
    }
}
