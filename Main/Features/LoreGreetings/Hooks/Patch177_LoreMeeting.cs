using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.LoreGreetings.Hooks;

/// <summary>
/// Patch177 (docs/features/lore-greetings.md): a void prefix on private <c>ConversationUseMeetingDialogs</c>, the check behind every
/// stranger's first line (<c>start_lord_unmet</c>, <c>unmet_in_main_mobile_party</c>, <c>start_wanderer_unmet</c>,
/// <c>start_attacking_unmet</c>). When the speaker knows the player's person, the service marks him met first, so the original returns
/// false and the conversation goes on to the met greeting, as vanilla does for close relatives. Never skips the original; a fault leaves
/// vanilla's meeting. Callers, thread and co-op: docs/reference/harmony-patch-registry.md "Patch177_LoreGreetings".
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), TargetName)]
[HarmonyPatchCategory(LoreGreetingsModule.PatchCategory)]
public static class Patch177_LoreMeeting
{
    internal const string TargetName = "ConversationUseMeetingDialogs";

    internal static LoreGreetingsService? Service { get; private set; }

    internal static void Initialize(LoreGreetingsService service) => Service = service;

    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            Service?.OnMeetingCheck();
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("LoreGreetings", TargetName, ex);
        }
    }
}
