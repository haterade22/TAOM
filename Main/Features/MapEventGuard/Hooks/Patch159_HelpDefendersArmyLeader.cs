using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch159 (#1429; ported from Silas He's v1.5.6 fix package, his save019): "Help defenders" at a siege crashes when
/// the siege camp's leader party is attached to another lord's army, and a save made afterwards is broken.
///
/// v1.5.7 <c>EncounterGameMenuBehavior.game_menu_join_encounter_help_defenders_on_consequence</c> calls
/// <c>RestartPlayerEncounter(SiegeEvent.BesiegerCamp.LeaderParty.Party, PartyBase.MainParty, false)</c>. The camp
/// leader is chosen by rank, so it can be a party attached to an army it does not lead. Silas traced the throw: the
/// siege event puts the army leader on the attacker side, the side setter drags the attached party along, the
/// defender side empties, the half-built event finalizes itself and a nested removal throws.
///
/// This prefix starts the encounter against the army leader instead, which brings the attached members in with it:
/// the path the engine takes when the camp leader leads its own army. It changes only the defender argument of a
/// fresh encounter the player attacks into.
///
/// v1.5.7 callers that pass <c>PartyBase.MainParty</c> (#1429 review, 2026-11-17): only
/// <c>EncounterGameMenuBehavior</c> :956 (help defenders) meets the rule. The defender has no besieger camp at :933 (the
/// raid leader), <c>GangLeaderNeedsWeaponsIssueQuestBehavior</c> :1075, <c>DestroyRaidersConspiracyQuest</c> :642 and
/// <c>RivalGangMovingInIssueBehavior</c> (cache :778); the defender is the main party at :768, :929, :3187,
/// <c>MerchantArmyOfPoachersIssueBehavior</c> :774 and <c>SiegeAmbushCampaignBehavior</c> :135. TAOM's
/// <c>EncounterAdapter</c> passes a defender that is already in the live map event. <c>EncounterManager</c>'s private
/// <c>RestartPlayerEncounter(attacker, defender)</c> is a different method.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), nameof(PlayerEncounter.RestartPlayerEncounter),
    new[] { typeof(PartyBase), typeof(PartyBase), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory(Category)]
public static class Patch159_HelpDefendersArmyLeader
{
    internal const string Category = "Patch159_HelpDefendersArmyLeader";

    private static IModLogger? _logger;

    internal sealed record Facts(
        bool AttackerIsMainParty,
        bool DefenderIsMainParty,
        bool DefenderInMapEvent,
        bool DefenderLeadsOwnArmy,
        bool DefenderAttachedToArmyLeader,
        bool ArmyLeaderInSameCamp,
        bool ArmyLeaderIsMainParty,
        bool ArmyLeaderInMapEvent);

    internal static void Initialize(IModLogger? logger) => _logger = logger;

    internal static bool ShouldUseArmyLeader(Facts f)
        => f.AttackerIsMainParty && !f.DefenderIsMainParty && !f.DefenderInMapEvent && !f.DefenderLeadsOwnArmy
           && f.DefenderAttachedToArmyLeader && f.ArmyLeaderInSameCamp && !f.ArmyLeaderIsMainParty && !f.ArmyLeaderInMapEvent;

    [HarmonyPrefix]
    public static void Prefix(ref PartyBase defenderParty, PartyBase attackerParty)
    {
        try
        {
            var member = defenderParty?.MobileParty;
            var leader = member?.Army?.LeaderParty;
            if (member?.BesiegerCamp == null || leader == null)
                return;

            var facts = new Facts(
                AttackerIsMainParty: attackerParty == PartyBase.MainParty,
                DefenderIsMainParty: defenderParty == PartyBase.MainParty,
                DefenderInMapEvent: defenderParty!.MapEvent != null,
                DefenderLeadsOwnArmy: leader == member,
                DefenderAttachedToArmyLeader: member.AttachedTo == leader,
                ArmyLeaderInSameCamp: leader.BesiegerCamp == member.BesiegerCamp,
                ArmyLeaderIsMainParty: leader == MobileParty.MainParty,
                ArmyLeaderInMapEvent: leader.Party.MapEvent != null);
            if (!ShouldUseArmyLeader(facts))
                return;

            _logger?.LogInfo($"[MapEventGuard] help defenders: the camp leader {member.StringId} is attached to "
                             + $"{leader.StringId}'s army; the encounter starts against the army leader (Patch159)");
            defenderParty = leader.Party;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[MapEventGuard] the help-defenders guard threw and is deferring to vanilla: {ex.Message}");
        }
    }
}
