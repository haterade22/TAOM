using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch170: prefix on <c>PlayerEncounter.FinalizeBattle()</c> that finishes a half-finished, already finalized map
/// event before vanilla runs (a save after "Continue (risky)" on a battle-end crash). <see cref="HalfFinishedEncounterService"/>
/// holds the rule and the repair; the prefix always returns true, so vanilla completes the finish. Details:
/// docs/features/map-event-guard.md.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "FinalizeBattle")]
[HarmonyPatchCategory(Category)]
public static class Patch170_HalfFinishedEncounterRepair
{
    internal const string Category = "Patch170_HalfFinishedEncounterRepair";

    private static HalfFinishedEncounterService? _service;
    private static IModLogger? _logger;
    private static bool _loggedFault;

    internal static void Initialize(HalfFinishedEncounterService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
        _loggedFault = false;
    }

    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (_service == null)
            return true;

        try
        {
            _service.RepairIfNeeded();
        }
        catch (Exception ex)
        {
            if (_loggedFault)
                return true;
            _loggedFault = true;
            _logger?.LogError($"[MapEventGuard] the half-finished encounter repair threw and is deferring to vanilla: {ex}");
        }

        return true;
    }
}
