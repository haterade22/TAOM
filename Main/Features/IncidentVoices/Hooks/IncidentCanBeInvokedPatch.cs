using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Incidents;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.IncidentVoices.Hooks;

/// <summary>
/// Patch175's seam: a postfix on public, non-virtual <c>Incident.CanIncidentBeInvoked()</c> (Incident.cs:120). Vanilla runs it for
/// every incident of a trigger (<c>IncidentManager.GetOccurableEventsForTrigger</c>) and once more right before the popup
/// (<c>Campaign.cs:1006</c>), so the last call before the player sees an incident passes through here, after vanilla's condition has
/// set its text variables. When vanilla said false the postfix returns at once. For a true it lets
/// <see cref="IncidentVoiceService"/> close the incident, swap in the places of the player's side and write TAOM's texts; any
/// fault leaves vanilla's answer and warns once. Callers, thread and co-op: docs/reference/harmony-patch-registry.md
/// "Patch175_IncidentVoices".
/// </summary>
[HarmonyPatch(typeof(Incident), nameof(Incident.CanIncidentBeInvoked))]
[HarmonyPatchCategory(IncidentVoicesModule.PatchCategory)]
public static class IncidentCanBeInvokedPatch
{
    private static bool _reported;
    private static IModLogger? _logger;

    internal static IncidentVoiceService? Service { get; private set; }

    internal static void Initialize(IncidentVoiceService service, IModLogger logger)
    {
        Service = service;
        _logger = logger;
        _reported = false;
    }

    [HarmonyPostfix]
    public static void Postfix(Incident __instance, ref bool __result)
    {
        var service = Service;
        if (service == null || __instance == null || !__result)
            return;

        try
        {
            __result = service.Review(new IncidentTextHandle(__instance), true);
        }
        catch (Exception ex)
        {
            if (_reported)
                return;
            _reported = true;
            _logger?.LogWarning("[IncidentVoices] an incident check failed, vanilla's answer and text stand: " + ex.Message);
        }
    }
}
