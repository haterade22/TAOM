using HarmonyLib;
using TaleWorlds.Core;

namespace TAOM.Features.BannerColorPersistence.Hooks;

[HarmonyPatch(typeof(Banner), nameof(Banner.GetFirstIconColor))]
[HarmonyPatchCategory("Patch23_BannerColorPersistence")]
public static class Banner_GetFirstIconColor_Patch
{
    private static IBannerColorService? _service;

    public static void Initialize(IBannerColorService service) => _service = service;

    [HarmonyPrefix]
    public static bool Prefix(Banner __instance, ref uint __result)
    {
        if (!(_service?.IsUniqueSecondaryColorEnabled() ?? false)) return true;
        if (__instance.BannerDataList.Count <= 1) return true;

        uint iconColor = BannerManager.GetColor(__instance.BannerDataList[1].ColorId);
        uint primaryColor = __instance.GetPrimaryColor();
        __result = _service.GetUniqueIconColor(primaryColor, iconColor);
        return false;
    }
}
