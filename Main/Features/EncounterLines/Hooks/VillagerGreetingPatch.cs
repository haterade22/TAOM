using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.EncounterLines.Hooks;

/// <summary>
/// Patch149: a prefix and postfix on private <c>village_farmer_talk_start_on_condition</c>. The prefix reads whether vanilla
/// has met the villagers before the original records it, and hands that to the postfix through <c>__state</c>; a stripped
/// prefix leaves <c>__state</c> false, so the postfix does nothing. A fault leaves vanilla's greeting and warns once.
/// Callers, thread and co-op: docs/reference/harmony-patch-registry.md "Patch149_EncounterLines".
/// </summary>
[HarmonyPatch(typeof(VillagerCampaignBehavior), "village_farmer_talk_start_on_condition")]
[HarmonyPatchCategory(EncounterLinesModule.PatchCategory)]
public static class VillagerGreetingPatch
{
    private static bool _reported;
    private static IModLogger? _logger;

    internal static EncounterLinesService? Service { get; private set; }

    internal static void Initialize(EncounterLinesService service, IModLogger logger)
    {
        Service = service;
        _logger = logger;
        _reported = false;
    }

    [HarmonyPrefix]
    public static void Prefix(out bool __state)
    {
        __state = false;
        try
        {
            __state = Service?.IsFirstVillagerMeeting() ?? false;
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    [HarmonyPostfix]
    public static void Postfix(bool __result, bool __state)
    {
        if (!__result || !__state)
            return;

        try
        {
            Service?.TryVillagerGreeting(__state);
        }
        catch (Exception ex)
        {
            Report(ex);
        }
    }

    private static void Report(Exception ex)
    {
        if (_reported)
            return;
        _reported = true;
        _logger?.LogWarning("[EncounterLines] villager greeting failed, vanilla's text stands: " + ex.Message);
    }
}
