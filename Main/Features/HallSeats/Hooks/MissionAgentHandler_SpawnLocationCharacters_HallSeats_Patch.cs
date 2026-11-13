using System;
using HarmonyLib;
using SandBox.Missions.MissionLogics;
using TAOM.Core.Logging;

namespace TAOM.Features.HallSeats.Hooks;

/// <summary>
/// Patch148 (Hall Seats, docs/features/hall-seats.md): opens and closes one spawn pass around
/// <c>MissionAgentHandler.SpawnLocationCharacters</c>, the method every indoor, town and village mission calls once to
/// spawn its location's characters. The void prefix opens the pass when the switch is on; the void finalizer closes it on
/// every exit, the throwing one included, and only when this call opened it (<c>__state</c>). A void finalizer keeps
/// Harmony on <c>rethrow</c>, so a vanilla throw keeps its stack. Notes: docs/reference/harmony-patch-registry.md
/// "Patch148_HallSeats".
/// </summary>
[HarmonyPatch(typeof(MissionAgentHandler), nameof(MissionAgentHandler.SpawnLocationCharacters))]
[HarmonyPatchCategory(HallSeatsModule.PatchCategory)]
public static class MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch
{
    private static HallSeatsService? _service;
    private static HallSeatsSettingsProvider? _settings;
    private static IModLogger? _logger;

    internal static void Initialize(HallSeatsService? service, HallSeatsSettingsProvider? settings, IModLogger? logger)
    {
        _service = service;
        _settings = settings;
        _logger = logger;
    }

    [HarmonyPrefix]
    public static void Prefix(MissionAgentHandler __instance, out bool __state)
    {
        __state = false;
        if (_service == null || _settings == null)
            return;

        try
        {
            __state = _service.BeginPass(__instance, _settings.IsEnabled);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[HallSeats] could not open the spawn pass, the hall spawns as in vanilla: {ex}");
        }
    }

    [HarmonyFinalizer]
    public static void Finalizer(bool __state)
    {
        if (__state)
            _service?.EndPass();
    }
}
