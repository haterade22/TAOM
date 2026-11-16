using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.Localization;

namespace TAOM.Features.Diplomacy.Hooks;

// The lord dialogue's "I can offer your realm {GOLD} to declare war on the {KINGDOM}" option lists every enemy kingdom
// of the player's realm that the lord's kingdom is not already fighting, and checks only the price. Its consequence calls
// StartCallToWarAgreement, whose prefix refuses a call the rule forbids, so without this patch the option is live and
// does nothing (#1350). This prefix greys it out with the refusal text instead, which replaces vanilla's "not enough
// money" hint for any refused pair (a war veto or a callToWarRefusals row). The displayed cost stays vanilla's.
// The target is private and patched by name: DiplomacyWarPrefixBindingTests pins it. Initialize is wired in
// DiplomacyIoC.InitializeHooks.
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_player_wants_to_sponsor_call_to_war_list_on_clickable_condition")]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class LordConversations_SponsorCallToWarClickable_Patch
{
    private static IOnAllianceAction _hook;

    public static void Initialize(IOnAllianceAction hook)
    {
        _hook = hook;
    }

    public static bool Prefix(ref bool __result, out TextObject hint)
    {
        hint = null;
        var target = ConversationSentence.CurrentProcessedRepeatObject as Kingdom;
        var called = Hero.OneToOneConversationHero?.MapFaction as Kingdom;
        if (_hook == null || target == null || called == null)
            return true; // the original assigns its own hint

        var refusal = _hook.GetCallToWarRefusalText(called.StringId, target.StringId);
        if (refusal == null)
            return true;

        hint = new TextObject(refusal);
        hint.SetTextVariable("CALLED_KINGDOM", called.Name);
        hint.SetTextVariable("KINGDOM_TO_CALL_TO_WAR_AGAINST", target.Name);
        __result = false;
        return false;
    }
}
