using System;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch84 — keeps the siege aftermath menus from crashing when vanilla never assigned the
/// besieging party. Crash bundle d7d9f7d3 (2026-09-07), reported from a live game.
///
/// THE CRASH. <c>SiegeAftermathCampaignBehavior.menu_settlement_taken_player_participant_on_init</c>
/// opens with <c>Settlement currentSettlement = _besiegerParty.CurrentSettlement;</c> and then reads
/// <c>currentSettlement.GetName()</c>. Neither is null-guarded, even though the very next line
/// guards the character chain (<c>LordPartyComponent?.Owner?.CharacterObject ?? PlayerCharacter</c>).
/// Its sibling <c>menu_settlement_taken_player_army_member_on_init</c> is worse still: it derefs
/// <c>_besiegerParty</c> three times unguarded, the FIRST being <c>.Army</c> at :325, a line ahead
/// of its own <c>.CurrentSettlement</c>. Gating on the party being present covers whole methods
/// rather than individual lines, which is why the verdict is taken before either body runs.
///
/// WHY _besiegerParty CAN BE NULL. It is assigned in exactly one place, inside
/// <c>OnMapEventEnded</c>'s <c>if (mapEvent.GetMapEventSide(Attacker).IsMainPartyAmongParties())</c>
/// block, and it is a saved field. When the main party is no longer among the ending event's
/// parties, that whole block is skipped: <c>_besiegerParty</c> keeps whatever the save held, and
/// <c>_wasPlayerArmyMember</c> is never set. On a campaign whose player has not yet finished a siege
/// among the parties, the saved value is null. <c>menu_settlement_taken_on_init</c> then routes on
/// that unset <c>_wasPlayerArmyMember</c> straight into the participant menu, which dereferences the
/// null on its first line.
///
/// HOW A PLAYER GETS THERE. Bundle d7d9f7d3 is an enlisted soldier who fought on the winning
/// attacker side of the siege of East Osgiliath and was not a member of the capturing army. Two
/// independent readings agree the main party had already left the event by the time
/// <c>MapEventEnded</c> was dispatched: vanilla skipped the assignment block (it must have, or
/// <c>_wasPlayerArmyMember</c> would have been true, since TAOM's battle army is always led by the
/// commander and never by the main party, so <c>MainParty.Army.LeaderParty != MainParty</c> holds),
/// and TAOM's own <c>EnlistmentBattleBehavior.OnMapEventEnded</c> reached
/// <c>OnCommanderBattleEnded</c> through the commander disjunct with <c>IsMainPartyInMapEvent</c>
/// false. <c>MapEventSide.Parties</c> is <c>_battleParties</c>, the same collection
/// <c>MapEvent.InvolvedParties</c> tracks, so the two views cannot disagree.
///
/// WHAT REMOVES IT IS TAOM's OWN DETACH, and the listener order is why. An earlier revision of this
/// comment claimed the opposite. It was wrong, and the correction is worth stating precisely because
/// the mistake is easy to repeat:
///
/// <c>MbEvent{T}.AddNonSerializedListener</c> (installed v1.4.8, :24-30) HEAD-INSERTS each new
/// listener into a singly-linked list, and <c>Invoke</c> :32-35 walks from the head. Registration is
/// therefore LIFO: the LAST listener registered is the FIRST one invoked. TAOM adds its campaign
/// behaviours in <c>SubModule.OnGameStart</c>, deliberately after SandBox has registered its own, so
/// TAOM's <c>EnlistmentBattleBehavior.OnMapEventEnded</c> sits at the head and runs BEFORE vanilla's
/// <c>SiegeAftermathCampaignBehavior.OnMapEventEnded</c> on the same dispatch.
///
/// That ordering supplies the whole chain. TAOM's listener calls
/// <c>ServiceBattleService.OnCommanderBattleEnded</c>, which calls <c>ArmyMembershipAdapter.LeaveArmy</c>,
/// which sets <c>MainParty.AttachedTo = null</c>. <c>MobileParty.SetAttachedToInternal</c> :1780-1783
/// answers that with <c>Party.MapEventSide.HandleMapEventEndForPartyInternal(Party)</c> and
/// <c>Party.MapEventSide = null</c>, whose setter runs <c>MapEventSide.RemovePartyInternal</c> and
/// drops the main party out of <c>_battleParties</c>. Vanilla's handler then reads
/// <c>IsMainPartyAmongParties()</c> against a list the player has already been removed from, skips
/// its assignment block, and leaves <c>_besiegerParty</c> null. This is the same seam as #551,
/// landing on a different victim.
///
/// THIS PATCH IS STILL THE RIGHT FLOOR. Fixing the ordering removes the path TAOM creates; it does
/// not make vanilla's two menus null-safe, and they are reachable by any player who leaves a winning
/// siege's party list for any reason. Keep both.
///
/// THE REPAIR IS VANILLA'S OWN TEXT, NOT NEW STRINGS. The prefixes rebuild the menu body from the
/// engine's own localisation keys with null-safe sources, falling back to
/// <c>TextObject.GetEmpty()</c> when no settlement resolves at all — which is exactly how vanilla
/// itself degrades the army-member menu for combinations it does not handle. The player reads a
/// sensible screen and presses Continue instead of losing the session.
/// </summary>
public static class Patch84_SiegeAftermathMenuGuard
{
    internal const string Category = "Patch84_SiegeAftermathMenuGuard";

    private static IModLogger _logger;
    private static FieldInfo _besiegerPartyField;

    /// <summary>
    /// False when the binding failed. Both prefixes then defer to vanilla untouched, so an engine
    /// rename degrades to "the guard is not installed" rather than replacing every siege aftermath
    /// menu in the game with a fallback.
    /// </summary>
    internal static bool IsReady { get; private set; }

    /// <summary>What the prefix should do once it has looked at the state vanilla left behind.</summary>
    internal enum MenuVerdict
    {
        /// <summary>Vanilla's own preconditions hold. Do nothing and let it run.</summary>
        RunVanilla,

        /// <summary>Vanilla would throw, but a settlement resolved, so the menu can still name it.</summary>
        RepairWithSettlement,

        /// <summary>Vanilla would throw and nothing names the settlement. Show an empty body.</summary>
        RepairWithoutSettlement,
    }

    /// <summary>
    /// The whole decision, pure and free of engine types so it can be tested without a campaign.
    ///
    /// Both of vanilla's dereferences have to hold for it to be safe to run: the party itself, and
    /// the settlement read off it. Guarding only the first would move the crash one line down rather
    /// than remove it.
    /// </summary>
    internal static MenuVerdict Decide(
        bool besiegerPartyPresent,
        bool besiegerSettlementPresent,
        bool fallbackSettlementPresent)
    {
        if (besiegerPartyPresent && besiegerSettlementPresent)
            return MenuVerdict.RunVanilla;

        return fallbackSettlementPresent
            ? MenuVerdict.RepairWithSettlement
            : MenuVerdict.RepairWithoutSettlement;
    }

    /// <summary>
    /// Resolved once. The menus run on a click rather than on a tick, so this is cheap either way,
    /// but the binding result is also what <see cref="IsReady"/> reports and that has to be decided
    /// before the first player ever opens one.
    /// </summary>
    internal static void Initialize(IModLogger logger)
    {
        _logger = logger;

        _besiegerPartyField = AccessTools.Field(typeof(SiegeAftermathCampaignBehavior), "_besiegerParty");
        IsReady = _besiegerPartyField != null;

        if (!IsReady)
        {
            _logger?.LogWarning(
                "[MapEventGuard] SiegeAftermathCampaignBehavior._besiegerParty did not resolve — Patch84 " +
                "is inert and the siege aftermath menu NRE (bundle d7d9f7d3) is unguarded on this engine build.");
        }
    }

    /// <summary>
    /// The state vanilla left behind, read once so that the verdict and the diagnostic see the same
    /// values. A read that throws is reported as "nothing resolved", which routes to the safest
    /// branch rather than letting the guard itself become the crash.
    /// </summary>
    internal static void ReadState(
        SiegeAftermathCampaignBehavior behaviour,
        out MobileParty besieger,
        out Settlement settlement)
    {
        besieger = null;
        settlement = null;

        try
        {
            besieger = _besiegerPartyField?.GetValue(behaviour) as MobileParty;
            settlement = besieger?.CurrentSettlement ?? Settlement.CurrentSettlement;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[MapEventGuard] could not read siege aftermath state, treating it as unresolved: {ex}");
        }
    }

    /// <summary>
    /// One line naming exactly which of vanilla's two dereferences was about to fail, plus the
    /// surrounding state. This is the diagnostic that closes the remaining question, so it carries
    /// the army and attachment identities rather than just the verdict.
    /// </summary>
    internal static void LogRepair(string menu, MobileParty besieger, Settlement settlement)
    {
        try
        {
            var main = MobileParty.MainParty;
            _logger?.LogWarning(
                $"[MapEventGuard] repaired the '{menu}' siege aftermath menu — vanilla would have thrown. " +
                $"besiegerParty={(besieger == null ? "NULL" : besieger.StringId)} " +
                $"besiegerSettlement={(besieger?.CurrentSettlement == null ? "NULL" : besieger.CurrentSettlement.StringId)} " +
                $"fallbackSettlement={(settlement == null ? "NULL" : settlement.StringId)} " +
                $"mainArmyLeader={(main?.Army?.LeaderParty == null ? "NULL" : main.Army.LeaderParty.StringId)} " +
                $"mainAttachedTo={(main?.AttachedTo == null ? "NULL" : main.AttachedTo.StringId)} " +
                $"mainInMapEvent={(main?.MapEvent != null)}. " +
                "A null besiegerParty means vanilla skipped its OnMapEventEnded assignment block, " +
                "which means the main party was not among the ending event's parties.");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[MapEventGuard] repair diagnostic failed: {ex}");
        }
    }

    /// <summary>
    /// Deferring to vanilla after the guard itself threw means letting the original crash happen, so
    /// this is an error rather than a quiet fallback.
    /// </summary>
    internal static void LogGuardFailure(string menu, Exception ex)
    {
        try
        {
            _logger?.LogError(
                $"[MapEventGuard] the '{menu}' siege aftermath guard threw and is deferring to vanilla, " +
                $"which may now crash exactly as it did in bundle d7d9f7d3: {ex}");
        }
        catch
        {
            /* never propagate out of a menu init */
        }
    }

    /// <summary>Test seam. The binding is process-global, so a test that rebinds must restore it.</summary>
    internal static void ResetForUnload()
    {
        _logger = null;
        _besiegerPartyField = null;
        IsReady = false;
    }
}

/// <summary>
/// The participant menu, the one bundle d7d9f7d3 actually crashed in. See
/// <see cref="Patch84_SiegeAftermathMenuGuard"/> for the full analysis.
/// </summary>
[HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), "menu_settlement_taken_player_participant_on_init")]
[HarmonyPatchCategory(Patch84_SiegeAftermathMenuGuard.Category)]
public static class Patch84_ParticipantMenuGuard
{
    // Vanilla's own key and English source, copied verbatim from installed v1.4.8 so the engine's
    // existing translations keep resolving. Do not reword it: the key is what carries the other
    // languages, and a changed default would only ever show on a missing-translation path.
    private const string ParticipantText =
        "{=C2KeQd0a}{ENCOUNTER_LEADER.LINK} thanks you for helping in the siege of {SETTLEMENT}. " +
        "You were able to loot your fallen foes, but you do not participate in the sack of the " +
        "{?IS_TOWN}town{?}castle{\\?} as you are not part of the army that took it.";

    [HarmonyPrefix]
    public static bool Prefix(SiegeAftermathCampaignBehavior __instance, MenuCallbackArgs args)
    {
        if (!Patch84_SiegeAftermathMenuGuard.IsReady || __instance == null || args == null)
            return true;

        try
        {
            Patch84_SiegeAftermathMenuGuard.ReadState(__instance, out var besieger, out var settlement);

            var verdict = Patch84_SiegeAftermathMenuGuard.Decide(
                besieger != null,
                besieger?.CurrentSettlement != null,
                settlement != null);

            if (verdict == Patch84_SiegeAftermathMenuGuard.MenuVerdict.RunVanilla)
                return true;

            Patch84_SiegeAftermathMenuGuard.LogRepair("player_participant", besieger, settlement);

            TextObject body;
            if (verdict == Patch84_SiegeAftermathMenuGuard.MenuVerdict.RepairWithSettlement)
            {
                body = new TextObject(ParticipantText);
                StringHelpers.SetCharacterProperties(
                    "ENCOUNTER_LEADER",
                    besieger?.LordPartyComponent?.Owner?.CharacterObject ?? CharacterObject.PlayerCharacter,
                    body);
                body.SetTextVariable("SETTLEMENT", settlement.GetName());
                body.SetTextVariable("IS_TOWN", settlement.IsTown ? 1 : 0);
            }
            else
            {
                // Vanilla's own degradation for a combination it cannot describe. The menu still
                // carries its Continue option, which is the only thing the player needs from it.
                body = TextObject.GetEmpty();
            }

            args.MenuContext.GameMenu.GetText().SetTextVariable("PLAYER_PARTICIPANT_TEXT", body);
            args.MenuContext.SetBackgroundMeshName("encounter_win");
            return false;
        }
        catch (Exception ex)
        {
            Patch84_SiegeAftermathMenuGuard.LogGuardFailure("player_participant", ex);
            return true;
        }
    }
}

/// <summary>
/// The army-member menu. It was not the one that crashed, but it is worse: THREE unguarded
/// dereferences of <c>_besiegerParty</c> — <c>.Army</c> at :325, <c>.CurrentSettlement</c> at :326,
/// and <c>.CurrentSettlement.Culture</c> again at :349 — and it is reachable from the same skipped
/// assignment block, so guarding one menu without the other would only move the crash.
/// </summary>
[HarmonyPatch(typeof(SiegeAftermathCampaignBehavior), "menu_settlement_taken_player_army_member_on_init")]
[HarmonyPatchCategory(Patch84_SiegeAftermathMenuGuard.Category)]
public static class Patch84_ArmyMemberMenuGuard
{
    // Vanilla's DEFAULT_TEXT for this menu, verbatim from installed v1.4.8. The aftermath flavour
    // sentence that normally follows it is deliberately not reproduced: it branches on
    // _playerEncounterAftermath and _wasPlayerArmyMember, and on the path this guard fires those
    // were never assigned either, so inventing a decision the game did not make would be worse than
    // omitting the sentence.
    private const string ArmyMemberText =
        "{=hvQUqRSb}{SETTLEMENT} has been taken by an army of which you are a member. ";

    [HarmonyPrefix]
    public static bool Prefix(SiegeAftermathCampaignBehavior __instance, MenuCallbackArgs args)
    {
        if (!Patch84_SiegeAftermathMenuGuard.IsReady || __instance == null || args == null)
            return true;

        try
        {
            Patch84_SiegeAftermathMenuGuard.ReadState(__instance, out var besieger, out var settlement);

            var verdict = Patch84_SiegeAftermathMenuGuard.Decide(
                besieger != null,
                besieger?.CurrentSettlement != null,
                settlement != null);

            if (verdict == Patch84_SiegeAftermathMenuGuard.MenuVerdict.RunVanilla)
                return true;

            Patch84_SiegeAftermathMenuGuard.LogRepair("player_army_member", besieger, settlement);

            TextObject body;
            if (verdict == Patch84_SiegeAftermathMenuGuard.MenuVerdict.RepairWithSettlement)
            {
                body = new TextObject(ArmyMemberText);
                body.SetTextVariable("SETTLEMENT", settlement.GetName());
            }
            else
            {
                body = TextObject.GetEmpty();
            }

            var text = args.MenuContext.GameMenu.GetText();
            text.SetTextVariable("LEADER_DECISION_TEXT", body);
            if (settlement != null)
                text.SetTextVariable("SETTLEMENT", settlement.GetName());

            args.MenuContext.SetBackgroundMeshName("encounter_win");
            return false;
        }
        catch (Exception ex)
        {
            Patch84_SiegeAftermathMenuGuard.LogGuardFailure("player_army_member", ex);
            return true;
        }
    }
}
