using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TAOM.Core.Logging;

namespace TAOM.Features.EncounterLines.Hooks;

/// <summary>
/// Patch149: a postfix on private <c>bandit_start_defender_condition</c>, which picks vanilla's robbery scene and returns true
/// when bandits stop the player. When it did, the service may replace all four robbery lines with one TAOM scene. A fault
/// leaves vanilla's scene and warns once. Callers, thread and co-op: docs/reference/harmony-patch-registry.md
/// "Patch149_EncounterLines".
/// </summary>
[HarmonyPatch(typeof(BanditInteractionsCampaignBehavior), "bandit_start_defender_condition")]
[HarmonyPatchCategory(EncounterLinesModule.PatchCategory)]
public static class RobberyLinesPatch
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

    [HarmonyPostfix]
    public static void Postfix(bool __result)
    {
        if (!__result)
            return;

        try
        {
            Service?.TryRobberySet();
        }
        catch (Exception ex)
        {
            if (_reported)
                return;
            _reported = true;
            _logger?.LogWarning("[EncounterLines] robbery lines failed, vanilla's scene stands: " + ex.Message);
        }
    }
}
