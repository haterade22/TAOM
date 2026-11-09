using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encounters;
using TAOM.Core.Logging;

namespace TAOM.Features.MapEventGuard.Hooks;

/// <summary>
/// Patch143 (#1099 follow-up): no route opens the player's meeting with a party nobody can speak for.
/// <see cref="EmptyMeetingGuardService"/> holds the rule. Patch142 stops the one sally-out route that is known; this
/// covers ordinary encounters and any sally route Patch142 does not stop.
///
/// Runs last, so Supply Lines and Refuge handle their own parties' meetings first.
/// </summary>
[HarmonyPatch(typeof(PlayerEncounter), "DoMeeting")]
[HarmonyPatchCategory(Category)]
public static class Patch143_EmptyMeetingGuard
{
    internal const string Category = "Patch143_EmptyMeetingGuard";

    private static EmptyMeetingGuardService? _service;
    private static IModLogger? _logger;

    internal static void Initialize(EmptyMeetingGuardService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static bool Prefix()
    {
        if (_service == null)
            return true;

        try
        {
            return !_service.ShouldDivertMeeting();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[MapEventGuard] the empty-meeting guard threw and is deferring to vanilla: {ex}");
            return true;
        }
    }
}
