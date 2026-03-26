using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.BannerInjection.Hooks;

[HarmonyPatchCategory("Patch15_BannerLayerLimit")]
public static class Banner_TryGetBannerDataFromCode_Patch
{
    public static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Banner), "TryGetBannerDataFromCode",
            new[] { typeof(string), typeof(List<BannerData>).MakeByRefType() });
    }

    [HarmonyPostfix]
    public static void Postfix(bool __result, string bannerCode, ref List<BannerData> bannerDataList)
    {
        if (!__result || bannerDataList == null || bannerDataList.Count < 32)
            return;

        var allLayers = BannerLayerExpander.ParseAllLayers(bannerCode);
        if (allLayers.Count <= bannerDataList.Count)
            return;

        bannerDataList.Clear();
        foreach (var layer in allLayers)
        {
            bannerDataList.Add(new BannerData(
                layer[0], layer[1], layer[2],
                new Vec2(layer[3], layer[4]),
                new Vec2(layer[5], layer[6]),
                layer[7] == 1,
                layer[8] == 1,
                layer[9] * 0.0027777778f));
        }
    }
}
