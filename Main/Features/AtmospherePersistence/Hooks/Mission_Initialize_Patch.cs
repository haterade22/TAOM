using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AtmospherePersistence.Hooks;

[HarmonyPatch(typeof(Mission), "Initialize")]
[HarmonyPatchCategory("Patch16_AtmospherePersistence")]
public static class Mission_Initialize_Patch
{
    private static readonly PropertyInfo InitializerRecordProperty =
        AccessTools.Property(typeof(Mission), "InitializerRecord");

    [HarmonyPrefix]
    public static void Prefix(Mission __instance)
    {
        try
        {
            if (!AtmosphereOverrideService.RequiresAtmosphereOverride(__instance.SceneName))
                return;

            if (InitializerRecordProperty == null)
                return;

            var rec = (MissionInitializerRecord)InitializerRecordProperty.GetValue(__instance);
            rec.PlayingInCampaignMode = false;
            rec.AtmosphereOnCampaign = AtmosphereInfo.GetInvalidAtmosphereInfo();
            InitializerRecordProperty.SetValue(__instance, rec);
        }
        catch (Exception)
        {
            // Silently fail — atmosphere override is non-critical
        }
    }
}
