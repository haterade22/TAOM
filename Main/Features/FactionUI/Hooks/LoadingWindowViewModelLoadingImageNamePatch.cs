using System;
using HarmonyLib;
using TaleWorlds.MountAndBlade.GauntletUI;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: Kysaro's loading screens in place of the image vanilla picks (#704), from his
/// <c>LoadingScreen_ImageName_Patch</c>. Target: the <c>LoadingWindowViewModel.LoadingImageName</c>
/// setter, assigned once by the constructor at startup and once per loading screen by
/// <c>SetNextGenericImage</c> (v1.5.3 LoadingWindowViewModel.cs:187, :287), and per multiplayer mission
/// by <c>SetForMultiplayer</c> (:262); not per frame. The constructor's assignment happens while the
/// window is disabled and is replaced before it ever shows (enabling sets <c>Enabled</c> first, then picks
/// a new image, :49-55 and :271), so a hidden window's image is left alone instead of being loaded for
/// nothing (up to 31.6 MB).
/// </summary>
[HarmonyPatch(typeof(LoadingWindowViewModel), nameof(LoadingWindowViewModel.LoadingImageName), MethodType.Setter)]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class LoadingWindowViewModelLoadingImageNamePatch
{
    private static bool _reported;

    static void Prefix(LoadingWindowViewModel __instance, ref string value)
    {
        if (!__instance.Enabled)
            return;

        try
        {
            var replacement = FactionUIPatchContext.LoadingImages?.ReplaceImageName(value);
            if (replacement != null)
                value = replacement;
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(LoadingWindowViewModelLoadingImageNamePatch), ex);
        }
    }
}
