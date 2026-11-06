using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;

namespace TAOM.Features.FactionUI.Campaign
{
    [HarmonyPatch]
    internal static class CampaignTimeHintPatch
    {
        internal static bool Enabled;

        private static MethodBase TargetMethod() => AccessTools.Method(typeof(MapTimeControlVM), "Refresh");

        private static void Postfix(MapTimeControlVM __instance)
        {
            if (!Enabled) return;
            __instance.TimeOfDayHint.SetHintCallback(() =>
            {
                int minutes = (int)Math.Floor(__instance.Time * 60.0);
                string clock = (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00");
                return clock + "\n" + CampaignUIHelper.GetTimeOfDayAndResetCameraTooltip();
            });
        }
    }
}
