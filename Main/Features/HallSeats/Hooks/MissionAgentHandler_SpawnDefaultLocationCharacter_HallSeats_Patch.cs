using System;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.HallSeats.Hooks;

/// <summary>
/// Patch148 (Hall Seats, docs/features/hall-seats.md): inside a spawn pass opened by
/// <see cref="MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch"/>, a lord the lord's hall has no seat for is not
/// spawned: the prefix returns false with a null agent. That is the value vanilla itself returns when it finds no spawn
/// frame, and both engine callers accept it (<c>SpawnLocationCharacters</c> uses <c>?.</c>; <c>SpawnEnteringLocationCharacter</c>
/// drops the result, and runs outside a pass anyway). Skipping the original skips only the agent build: the location
/// character stays in the hall's list, so the hero's campaign location, the bribe check and the overlay are unchanged.
/// Outside a pass, with the switch off, or for any character the plan seats, vanilla runs. Any exception lets vanilla run.
/// </summary>
[HarmonyPatch(typeof(MissionAgentHandler), nameof(MissionAgentHandler.SpawnDefaultLocationCharacter))]
[HarmonyPatchCategory(HallSeatsModule.PatchCategory)]
public static class MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch
{
    private static HallSeatsService? _service;
    private static IModLogger? _logger;

    internal static void Initialize(HallSeatsService? service, IModLogger? logger)
    {
        _service = service;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static bool Prefix(LocationCharacter locationCharacter, ref Agent? __result)
    {
        var service = _service;
        if (service == null || !service.InPass)
            return true;

        try
        {
            if (!service.ShouldHoldBack(locationCharacter))
                return true;

            __result = null;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[HallSeats] seat check failed, the character spawns as in vanilla: {ex}");
            return true;
        }
    }
}
