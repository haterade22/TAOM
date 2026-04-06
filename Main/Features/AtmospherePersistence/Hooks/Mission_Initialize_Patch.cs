using System;
using System.Reflection;
using HarmonyLib;
using TAOM.Core.Logging;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AtmospherePersistence.Hooks;

[HarmonyPatch(typeof(Mission), "Initialize")]
[HarmonyPatchCategory("Patch16_AtmospherePersistence")]
public static class Mission_Initialize_Patch
{
    private static IModLogger? _logger;

    private static readonly PropertyInfo InitializerRecordProperty =
        AccessTools.Property(typeof(Mission), "InitializerRecord");

    public static void Initialize(IModLogger logger)
    {
        _logger = logger;
        if (InitializerRecordProperty == null)
            _logger?.LogError("[Atmosphere] STARTUP: Mission.InitializerRecord property not found — atmosphere override will be disabled for this session");
    }

    [HarmonyPrefix]
    public static void Prefix(Mission __instance)
    {
        try
        {
            if (!AtmosphereOverrideService.RequiresAtmosphereOverride(__instance.SceneName))
                return;

            if (InitializerRecordProperty == null)
            {
                _logger?.LogWarning("[Atmosphere] InitializerRecord property not found on Mission — cannot override atmosphere");
                return;
            }

            var rec = (MissionInitializerRecord)InitializerRecordProperty.GetValue(__instance);
            rec.PlayingInCampaignMode = false;
            rec.AtmosphereOnCampaign = AtmosphereInfo.GetInvalidAtmosphereInfo();
            InitializerRecordProperty.SetValue(__instance, rec);

            _logger?.LogInfo($"[Atmosphere] Overrode atmosphere for forced-atmo scene '{__instance.SceneName}'");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"[Atmosphere] Mission.Initialize patch error for scene '{__instance?.SceneName}': {ex.GetType().Name}: {ex.Message}");
        }
    }
}
