using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.LoreGreetings.Hooks;

/// <summary>
/// Patch177 (docs/features/lore-greetings.md): a postfix on private <c>conversation_lord_greets_over_24_hours_on_condition</c>, the
/// condition of vanilla's <c>start_default</c> line (<c>{=!}{VOICED_LINE}</c>, priority 100). A true result means that line is the one
/// shown (the NPC side takes the first true condition), and vanilla has just set <c>VOICED_LINE</c>; the service may replace it with a
/// lore line. A false result and a fault leave vanilla's text. Callers, thread and co-op: docs/reference/harmony-patch-registry.md
/// "Patch177_LoreGreetings".
/// </summary>
[HarmonyPatch(typeof(LordConversationsCampaignBehavior), TargetName)]
[HarmonyPatchCategory(LoreGreetingsModule.PatchCategory)]
public static class Patch177_LoreGreeting
{
    internal const string TargetName = "conversation_lord_greets_over_24_hours_on_condition";

    internal static LoreGreetingsService? Service { get; private set; }

    internal static void Initialize(LoreGreetingsService service) => Service = service;

    [HarmonyPostfix]
    public static void Postfix(bool __result)
    {
        if (!__result)
            return;
        try
        {
            Service?.OnGreeting();
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("LoreGreetings", TargetName, ex);
        }
    }
}
