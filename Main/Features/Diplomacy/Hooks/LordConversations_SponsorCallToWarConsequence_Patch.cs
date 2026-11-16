using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;

namespace TAOM.Features.Diplomacy.Hooks;

// Vanilla's sponsor-a-call-to-war consequence reads ConversationSentence.CurrentProcessedRepeatObject. When the player
// clicks, ConversationManager has reset that index to 0 for the two lines listed after the repeat line, so vanilla calls
// the war against the first kingdom on the page, not the clicked one (#1350). This prefix makes vanilla's two calls with
// SelectedRepeatObject, the clicked row, and skips the original. It goes through the same StartCallToWarAgreement, so
// TAOM's refusal prefix and Coop's prefix there still run. The target is private and patched by name:
// DiplomacyWarPrefixBindingTests pins it.
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_player_wants_to_sponsor_call_to_war_list_on_consequence")]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class LordConversations_SponsorCallToWarConsequence_Patch
{
    public static bool Prefix()
    {
        var target = ConversationSentence.SelectedRepeatObject as Kingdom;
        var caller = Hero.MainHero?.MapFaction as Kingdom;
        var called = Hero.OneToOneConversationHero?.MapFaction as Kingdom;
        if (target == null || caller == null || called == null)
            return true;

        var cost = Campaign.Current.Models.AllianceModel.GetCallToWarCost(caller, called, target);
        Campaign.Current.GetCampaignBehavior<IAllianceCampaignBehavior>()?
            .StartCallToWarAgreement(caller, called, target, cost, isPlayerPaying: true);
        return false;
    }
}
