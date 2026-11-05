using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace TAOM.Features.Messengers.Hooks;

/// <summary>
/// Patch117, #898: while a messenger conversation is open, six vanilla lines are hidden. "I want to join your
/// army" and "I want you to join my army" call <c>Army.AddPartyToMergedParties</c>, which attaches the party and
/// snaps it across the map; the board game starts after the window closes and the messenger then ends the mission;
/// the clan member troop and equipment inspections and the companion recall need the player to be there.
/// Postfixes, not prefixes, so the rule runs after Enlistment's Patch66 prefix on the join-army condition. The filter
/// only ever turns true into false (the state reads one volatile bool). Callers, thread, PatchShield and
/// co-op notes: docs/reference/harmony-patch-registry.md "Patch117_MessengerSessionRules".
/// </summary>
[HarmonyPatch]
[HarmonyPatchCategory(MessengerSessionRulesModule.PatchCategory)]
public static class MessengerSessionRulesPatch
{
    private static IMessengerConversationState? _state;

    internal static void Initialize(IMessengerConversationState state) => _state = state;

    [HarmonyTargetMethods]
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(LordConversationsCampaignBehavior), "conversation_lord_join_army_on_condition");
        yield return AccessTools.Method(typeof(LordConversationsCampaignBehavior), "player_ask_to_join_players_army_on_condition");
        yield return AccessTools.Method(typeof(LordConversationsCampaignBehavior), "conversation_clan_member_manage_troops_on_condition");
        yield return AccessTools.Method(typeof(LordConversationsCampaignBehavior), "conversation_clan_member_manage_inventory_on_condition");
        yield return AccessTools.Method(typeof(CompanionRolesCampaignBehavior), "companion_rejoin_after_emprisonment_role_on_condition");
        yield return AccessTools.Method(typeof(BoardGameCampaignBehavior), "conversation_lord_talk_game_on_condition");
    }

    [HarmonyPostfix]
    public static void Postfix(ref bool __result)
    {
        __result = Filter(__result, _state);
    }

    internal static bool Filter(bool result, IMessengerConversationState? state) => result && !(state?.IsOpen ?? false);
}
