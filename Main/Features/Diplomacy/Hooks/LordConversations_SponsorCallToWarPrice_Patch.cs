using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;

namespace TAOM.Features.Diplomacy.Hooks;

// Vanilla's sponsor-a-call-to-war list sets each row's price as the global text variable GOLD. The rows are drawn only
// after all of them are built, so every row showed the last row's price (#1381). This postfix also sets GOLD on the
// row's own text, which the text processor reads before the global variable (TextProcessingContext.GetVariableValue).
// Display only: the clickable condition and the consequence compute each row's own price. The target is private and
// patched by name: DiplomacyWarPrefixBindingTests pins it.
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_player_wants_to_sponsor_call_to_war_list_on_condition")]
[HarmonyPatchCategory("Patch11_Diplomacy")]
public static class LordConversations_SponsorCallToWarPrice_Patch
{
    public static void Postfix()
    {
        var target = ConversationSentence.CurrentProcessedRepeatObject as Kingdom;
        var caller = Hero.MainHero?.MapFaction as Kingdom;
        var called = Hero.OneToOneConversationHero?.MapFaction as Kingdom;
        var line = ConversationSentence.SelectedRepeatLine;
        if (target == null || caller == null || called == null || line == null)
            return;

        line.SetTextVariable("GOLD", Campaign.Current.Models.AllianceModel.GetCallToWarCost(caller, called, target));
    }
}
