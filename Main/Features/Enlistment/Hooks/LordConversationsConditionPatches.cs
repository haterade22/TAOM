using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Localization;

namespace TAOM.Features.Enlistment.Hooks;

/// <summary>
/// Patch66 — suppresses four vanilla LordConversations lines while enlisted. There is no
/// public API to veto a registered native sentence's condition, so prefix-return-false on
/// the condition methods is the honest mechanism (donor-proven). All four fail open (run
/// the original) when not enlisted or on any internal error. Signatures verified against
/// installed 1.4.7: join-army pair is private, ally-thanks pair is PUBLIC, and the
/// clickable condition carries an <c>out TextObject hint</c>. Binding drift is pinned by
/// <c>Patch66EnlistmentBindingTests</c>.
/// </summary>
internal static class LordConversationsConditionGate
{
    private static IEnlistmentStateQuery _query;

    /// <summary>True when the vanilla line must be suppressed (player is enlisted). Fail-open on error.</summary>
    internal static bool ShouldSuppress()
    {
        try
        {
            _query = _query ?? IoC.Resolve<IEnlistmentStateQuery>();
            return _query.IsEnlisted;
        }
        catch (Exception)
        {
            return false; // fail open — vanilla behavior
        }
    }
}

/// <summary>While enlisted, a lord must not recruit the player into a second army — that would shatter the service state.</summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_join_army_on_condition")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class LordConversationsJoinArmyConditionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!LordConversationsConditionGate.ShouldSuppress())
            return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_lord_join_army_on_clickable_condition")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class LordConversationsJoinArmyClickableConditionPatch
{
    public static bool Prefix(ref bool __result, out TextObject hint)
    {
        hint = new TextObject(string.Empty);
        if (!LordConversationsConditionGate.ShouldSuppress())
            return true; // original assigns its own hint

        hint = new TextObject("{=taom_enlist_already_serving}You are bound to military service.");
        __result = false;
        return false;
    }
}

/// <summary>The enlisted player fights in every commander battle — vanilla's "thanks for helping" line would fire constantly.</summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_ally_thanks_meet_after_helping_in_battle_on_condition")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class LordConversationsAllyThanksMeetConditionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!LordConversationsConditionGate.ShouldSuppress())
            return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(LordConversationsCampaignBehavior), "conversation_ally_thanks_after_helping_in_battle_on_condition")]
[HarmonyPatchCategory("Patch66_Enlistment")]
public static class LordConversationsAllyThanksConditionPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!LordConversationsConditionGate.ShouldSuppress())
            return true;
        __result = false;
        return false;
    }
}
