using System;
using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.FactionUI.Hooks;

/// <summary>
/// Patch95_FactionUI: the main-menu background video (#704), from Kysaro's <c>MenuVideo_PlayVideo_Patch</c>.
/// Target: <c>VideoPlayerView.PlayVideo(string videoFileName, string soundFileName, float framerate,
/// bool looping)</c> (v1.5.3 VideoPlayerView.cs:19), called by <c>MBInitialScreenBase.RefreshScene</c>
/// (:161) with a video it picked from every active module's <c>Videos/initial_menu</c>.
/// </summary>
[HarmonyPatch(typeof(VideoPlayerView), nameof(VideoPlayerView.PlayVideo),
    new[] { typeof(string), typeof(string), typeof(float), typeof(bool) })]
[HarmonyPatchCategory(FactionUIPatchContext.Category)]
public static class VideoPlayerViewPlayVideoPatch
{
    private static bool _reported;

    static void Prefix(ref string videoFileName, ref string soundFileName)
    {
        try
        {
            if (FactionUIPatchContext.Media?.ChooseMenuVideo(videoFileName) is not { } choice)
                return;
            videoFileName = choice.Video;
            soundFileName = choice.Audio;
        }
        catch (Exception ex)
        {
            FactionUIPatchContext.ReportOnce(ref _reported, nameof(VideoPlayerViewPlayVideoPatch), ex);
        }
    }
}
